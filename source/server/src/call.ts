import { DurableObject } from "cloudflare:workers";
import type { Env } from "./index";
import {
  CALL_CONTROL_RATE,
  CALL_FRAME_RATE,
  CallSignal,
  CloseCode,
  DECLINE_DENIED,
  MAX_ADMITTED,
  MAX_CALL_FRAME_BYTES,
  MAX_CALL_MEMBERS,
  MAX_CALL_TEXT_BYTES,
  MEDIA_HEADER_BYTES,
  MEDIA_SLOT_OFFSET,
  MEDIA_VERSION,
  ROOM_LIFETIME_MS,
} from "./protocol";
import { JOIN_HEADER, type JoinTicket, type SlotOccupant, VACANT, isProbe, occupantResponse } from "./room";
import { type Bucket, type Message, OPEN, charge, exceedsUtf8Bytes, freshBucket, parseMessage, send } from "./wire";

export const DECLINE_CODE = "code";
export const DECLINE_FULL = "full";
export const DECLINE_HOST_AWAY = "host_away";
const ACTIVE = "active";
const PENDING = "pending";

// hostId is the creator, who keeps approving after a reconnect even though that rejoin arrives as a viewer ticket.
// approval and admitted are copied into every member's attachment so they outlive the host's own connection.
export interface CallRoomState {
  code: string;
  address: string;
  hostId: string;
  callId: string;
  createdAt: number;
  approval: boolean;
  admitted: string[];
}

interface Member {
  id: string;
  name: string;
  world: string;
  role: "host" | "viewer";
  room: CallRoomState | null;
  slot: number;
  joined: boolean;
  pending: boolean;
  gone: boolean;
  frames: Bucket;
  controls: Bucket;
}

interface Connection {
  socket: WebSocket;
  member: Member;
}

function expired(room: CallRoomState, now: number): boolean {
  return now >= room.createdAt + ROOM_LIFETIME_MS;
}

function admit(room: CallRoomState, id: string): void {
  room.admitted = room.admitted.filter((entry) => entry !== id);
  room.admitted.push(id);
  if (room.admitted.length > MAX_ADMITTED) {
    room.admitted.shift();
  }
}

function participant(member: Member, state: string): Message {
  return {
    userId: member.id,
    name: member.name,
    world: member.world,
    displayName: member.name,
    slot: member.slot,
    state,
    muted: false,
  };
}

function joinRequest(member: Member): Message {
  return { type: CallSignal.joinRequest, userId: member.id, from: participant(member, PENDING) };
}

// One voice call per room slot. The relay never decodes audio: it checks a frame's size and header, stamps the
// sender's slot into the header so nobody can speak as someone else, and forwards the bytes to the other joined
// members. Members and their rate buckets live in WebSocket attachments (buckets are cached in memory between
// roster changes), so a call writes nothing to storage and schedules no alarm; the six hour lifetime is enforced
// whenever a frame, a message or a new connection arrives. When the creator's first call.join carries
// approvalRequired, every later newcomer waits as pending (no slot, no frames, no roster) until the creator approves.
export class CallRoom extends DurableObject<Env> {
  private readonly cache = new Map<WebSocket, Member>();

  async fetch(request: Request): Promise<Response> {
    if (isProbe(request)) {
      return occupantResponse(this.occupant());
    }

    const header = request.headers.get(JOIN_HEADER);
    if (header === null || request.headers.get("Upgrade") !== "websocket") {
      return new Response("bad request", { status: 400 });
    }

    const ticket = JSON.parse(decodeURIComponent(header)) as JoinTicket;
    const now = Date.now();
    let connections = this.connections();
    let room = this.room(connections);
    if (room !== null && expired(room, now)) {
      this.endRoom("expired");
      connections = [];
      room = null;
    }

    if (ticket.role === "host") {
      const heldByOthers = connections.some((connection) => connection.member.room !== null && connection.member.id !== ticket.id);
      const creatorPresent = room !== null && room.hostId === ticket.id
        && connections.some((connection) => connection.member.id === ticket.id && connection.member.room !== null);
      if (heldByOthers && !creatorPresent) {
        return new Response("slot occupied", { status: 409 });
      }

      // The relay sends a character's new call to the slot it already runs, and one character runs one call.
      if (heldByOthers) {
        this.endRoom("ended");
        connections = [];
      }
    }

    let replacedJoined = false;
    for (const existing of connections) {
      if (existing.member.id === ticket.id) {
        replacedJoined ||= existing.member.joined;
        this.retire(existing, CloseCode.replaced, "replaced");
      }
    }

    if (ticket.role === "host") {
      const created: CallRoomState = {
        code: ticket.code,
        address: ticket.address,
        hostId: ticket.id,
        callId: crypto.randomUUID(),
        createdAt: now,
        approval: false,
        admitted: [],
      };
      return this.accept(this.newMember(ticket, created, now));
    }

    if (replacedJoined) {
      this.broadcastRoster(this.connections());
    }

    return this.accept(this.newMember(ticket, room !== null && room.code === ticket.code ? room : null, now));
  }

  async webSocketMessage(socket: WebSocket, raw: string | ArrayBuffer): Promise<void> {
    const member = this.memberOf(socket);
    if (member.gone) {
      return;
    }

    const now = Date.now();
    if (member.room !== null && expired(member.room, now)) {
      this.endRoom("expired");
      return;
    }

    if (typeof raw !== "string") {
      this.onFrame(socket, member, raw, now);
      return;
    }

    if (exceedsUtf8Bytes(raw, MAX_CALL_TEXT_BYTES)) {
      this.drop(socket, member, CloseCode.tooBig, "message too big");
      return;
    }

    const verdict = charge(member.controls, CALL_CONTROL_RATE, now);
    if (verdict === "close") {
      this.drop(socket, member, CloseCode.rate, "rate limited");
      return;
    }

    const message = verdict === "drop" ? null : parseMessage(raw);
    if (message === null) {
      return;
    }

    switch (message.type) {
      case CallSignal.join:
        this.onJoin(socket, member, message);
        return;
      case CallSignal.approve:
        this.onApprove(member, message);
        return;
      case CallSignal.deny:
        this.onDeny(member, message);
        return;
      case CallSignal.leave:
        this.drop(socket, member, CloseCode.ended, "left");
        return;
    }
  }

  async webSocketClose(socket: WebSocket): Promise<void> {
    this.depart(socket, this.memberOf(socket));
    this.cache.delete(socket);
  }

  async webSocketError(socket: WebSocket): Promise<void> {
    this.depart(socket, this.memberOf(socket));
    this.cache.delete(socket);
  }

  private newMember(ticket: JoinTicket, room: CallRoomState | null, now: number): Member {
    return {
      id: ticket.id,
      name: ticket.name,
      world: ticket.world,
      role: ticket.role,
      room,
      slot: -1,
      joined: false,
      pending: false,
      gone: false,
      frames: freshBucket(CALL_FRAME_RATE, now),
      controls: freshBucket(CALL_CONTROL_RATE, now),
    };
  }

  private accept(member: Member): Response {
    const pair = new WebSocketPair();
    this.ctx.acceptWebSocket(pair[1]);
    this.cache.set(pair[1], member);
    pair[1].serializeAttachment(member);
    return new Response(null, { status: 101, webSocket: pair[0] });
  }

  private memberOf(socket: WebSocket): Member {
    const cached = this.cache.get(socket);
    if (cached !== undefined) {
      return cached;
    }

    const member = socket.deserializeAttachment() as Member;
    this.cache.set(socket, member);
    return member;
  }

  private connections(exclude: WebSocket | null = null): Connection[] {
    const result: Connection[] = [];
    for (const socket of this.ctx.getWebSockets()) {
      if (socket === exclude || socket.readyState !== OPEN) {
        continue;
      }

      const member = this.memberOf(socket);
      if (!member.gone) {
        result.push({ socket, member });
      }
    }

    return result;
  }

  private room(connections: Connection[]): CallRoomState | null {
    for (const connection of connections) {
      if (connection.member.room !== null && (connection.member.joined || connection.member.role === "host")) {
        return connection.member.room;
      }
    }

    return null;
  }

  private occupant(): SlotOccupant {
    const connections = this.connections();
    let room: CallRoomState | null = null;
    for (const connection of connections) {
      if (connection.member.room !== null) {
        room = connection.member.room;
      }
    }

    if (room === null) {
      return VACANT;
    }

    const creatorId = room.hostId;
    const creatorPresent = connections.some((connection) => connection.member.id === creatorId && connection.member.room !== null);
    return { occupied: true, hostId: creatorPresent ? creatorId : null, address: room.address ?? null };
  }

  private onFrame(socket: WebSocket, member: Member, frame: ArrayBuffer, now: number): void {
    if (frame.byteLength > MAX_CALL_FRAME_BYTES) {
      this.drop(socket, member, CloseCode.tooBig, "frame too big");
      return;
    }

    const verdict = charge(member.frames, CALL_FRAME_RATE, now);
    if (verdict === "close") {
      this.drop(socket, member, CloseCode.rate, "rate limited");
      return;
    }

    if (verdict === "drop" || !member.joined || frame.byteLength < MEDIA_HEADER_BYTES) {
      return;
    }

    const bytes = new Uint8Array(frame);
    if (bytes[0] !== MEDIA_VERSION) {
      return;
    }

    bytes[MEDIA_SLOT_OFFSET] = member.slot;
    for (const connection of this.connections(socket)) {
      if (connection.member.joined) {
        connection.socket.send(frame);
      }
    }
  }

  private onJoin(socket: WebSocket, member: Member, message: Message): void {
    const connections = this.connections();
    if (member.joined) {
      this.sendRoster(socket, member.room, connections);
      return;
    }

    if (member.pending) {
      send(socket, { type: CallSignal.joinPending });
      return;
    }

    const room = this.room(connections) ?? member.room;
    if (room === null || (member.role === "viewer" && message.code !== room.code)) {
      this.decline(socket, member, DECLINE_CODE);
      return;
    }

    if (this.takenSlots(connections).size >= MAX_CALL_MEMBERS) {
      this.decline(socket, member, DECLINE_FULL);
      return;
    }

    if (member.role === "host") {
      room.approval = message.approvalRequired === true;
    }

    if (!room.approval || member.id === room.hostId || room.admitted.includes(member.id)) {
      this.seat(socket, member, room);
      return;
    }

    // Only the creator decides. A newcomer who finds the creator away is declined at once rather than left waiting
    // on a decision that may never come, and no other member can let someone in on the creator's behalf.
    const host = connections.find((connection) => connection.member.joined && connection.member.id === room.hostId);
    if (host === undefined) {
      this.decline(socket, member, DECLINE_HOST_AWAY);
      return;
    }

    member.room = room;
    member.pending = true;
    socket.serializeAttachment(member);
    send(socket, { type: CallSignal.joinPending });
    send(host.socket, joinRequest(member));
  }

  private onApprove(member: Member, message: Message): void {
    const room = member.room;
    if (!member.joined || room === null || member.id !== room.hostId) {
      return;
    }

    const connections = this.connections();
    const target = connections.find((connection) => connection.member.pending && connection.member.id === message.userId);
    if (target === undefined) {
      return;
    }

    if (this.takenSlots(connections).size >= MAX_CALL_MEMBERS) {
      this.decline(target.socket, target.member, DECLINE_FULL);
      return;
    }

    this.seat(target.socket, target.member, room);
  }

  private onDeny(member: Member, message: Message): void {
    const room = member.room;
    if (!member.joined || room === null || member.id !== room.hostId) {
      return;
    }

    const target = this.connections().find((connection) => connection.member.pending && connection.member.id === message.userId);
    if (target !== undefined) {
      this.decline(target.socket, target.member, DECLINE_DENIED);
    }
  }

  private seat(socket: WebSocket, member: Member, room: CallRoomState): void {
    const connections = this.connections();
    const taken = this.takenSlots(connections);
    let slot = 0;
    while (taken.has(slot)) {
      slot++;
    }

    if (room.approval && member.id !== room.hostId) {
      admit(room, member.id);
    }

    member.room = room;
    member.slot = slot;
    member.joined = true;
    member.pending = false;
    for (const connection of connections) {
      if (connection.member.room !== null) {
        connection.member.room = room;
        connection.socket.serializeAttachment(connection.member);
      }
    }

    this.broadcastRoster(connections);
    if (member.id !== room.hostId) {
      return;
    }

    for (const connection of connections) {
      if (connection.member.pending) {
        send(socket, joinRequest(connection.member));
      }
    }
  }

  private takenSlots(connections: Connection[]): Set<number> {
    const taken = new Set<number>();
    for (const connection of connections) {
      if (connection.member.joined) {
        taken.add(connection.member.slot);
      }
    }

    return taken;
  }

  private participants(connections: Connection[]): Message[] {
    const joined = connections.filter((connection) => connection.member.joined);
    joined.sort((left, right) => left.member.slot - right.member.slot);
    return joined.map(({ member }) => participant(member, ACTIVE));
  }

  private sendRoster(socket: WebSocket, room: CallRoomState | null, connections: Connection[]): void {
    if (room !== null) {
      send(socket, { type: CallSignal.roster, callId: room.callId, code: room.code, participants: this.participants(connections) });
    }
  }

  private broadcastRoster(connections: Connection[]): void {
    const room = this.room(connections);
    if (room === null) {
      return;
    }

    const message = { type: CallSignal.roster, callId: room.callId, code: room.code, participants: this.participants(connections) };
    for (const connection of connections) {
      if (connection.member.joined) {
        send(connection.socket, message);
      }
    }
  }

  private decline(socket: WebSocket, member: Member, reason: string): void {
    send(socket, { type: CallSignal.declined, reason });
    this.retire({ socket, member }, CloseCode.ended, "declined");
  }

  private retire(connection: Connection, code: number, reason: string): void {
    connection.member.gone = true;
    connection.socket.serializeAttachment(connection.member);
    connection.socket.close(code, reason);
  }

  private drop(socket: WebSocket, member: Member, code: number, reason: string): void {
    socket.close(code, reason);
    this.depart(socket, member);
  }

  private depart(socket: WebSocket, member: Member): void {
    if (member.gone) {
      return;
    }

    member.gone = true;
    socket.serializeAttachment(member);
    const room = member.room;
    if (room === null || (!member.joined && !member.pending)) {
      return;
    }

    const connections = this.connections(socket);
    if (member.pending) {
      const host = connections.find((connection) => connection.member.joined && connection.member.id === room.hostId);
      if (host !== undefined) {
        send(host.socket, { type: CallSignal.joinCancelled, userId: member.id });
      }

      return;
    }

    this.broadcastRoster(connections);
    if (member.id !== room.hostId) {
      return;
    }

    for (const connection of connections) {
      if (connection.member.pending) {
        this.decline(connection.socket, connection.member, DECLINE_HOST_AWAY);
      }
    }
  }

  private endRoom(reason: string): void {
    for (const connection of this.connections()) {
      send(connection.socket, { type: CallSignal.ended, reason });
      this.retire(connection, CloseCode.ended, "ended");
    }
  }
}
