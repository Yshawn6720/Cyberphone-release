import { DurableObject } from "cloudflare:workers";
import type { Env } from "./index";
import {
  ALONE_TIMEOUT_MS,
  CONTROL_ACTIONS,
  CloseCode,
  DROP_LIMIT,
  DROP_WINDOW_MS,
  FEATURE_PARTY,
  HOST_RATE,
  MAX_MEMBERS,
  MAX_MESSAGE_BYTES,
  MAX_SHARED_QUEUE,
  PERMISSION_CAN_HOST,
  PERMISSION_GRANT_MASK,
  ROOM_LIFETIME_MS,
  SignalType,
  VIEWER_RATE,
  normalizeCode,
} from "./protocol";

export const JOIN_HEADER = "X-Relay-Join";
const OPEN = 1;

export interface JoinTicket {
  role: "host" | "viewer";
  code: string;
  id: string;
  name: string;
  world: string;
}

interface Grant {
  userId: string;
  flags: number;
}

interface RoomState {
  code: string;
  createdAt: number;
  aloneSince: number | null;
  grants: Grant[];
  pendingTransfer: string | null;
  nextSequence: number;
}

interface Bucket {
  tokens: number;
  at: number;
  windowStart: number;
  dropped: number;
}

interface Member {
  id: string;
  name: string;
  world: string;
  role: "host" | "viewer";
  joined: boolean;
  pendingJoin: boolean;
  gone: boolean;
  sequence: number;
  bucket: Bucket;
  room: RoomState | null;
}

interface Connection {
  socket: WebSocket;
  member: Member;
}

type Message = Record<string, unknown>;

const forwardedStateFields: Readonly<Record<string, "string" | "number" | "boolean">> = {
  url: "string",
  positionSeconds: "number",
  paused: "boolean",
  territoryId: "number",
  worldId: "number",
  approvalRequired: "boolean",
  discoverable: "boolean",
  codeEnabled: "boolean",
  guestPermissions: "number",
  screenX: "number",
  screenY: "number",
  screenZ: "number",
  screenYaw: "number",
  screenPitch: "number",
  screenRoll: "number",
  screenScale: "number",
  screenCurve: "number",
};

function memberOf(socket: WebSocket): Member {
  return socket.deserializeAttachment() as Member;
}

function send(socket: WebSocket, message: Message): void {
  if (socket.readyState === OPEN) {
    socket.send(JSON.stringify(message));
  }
}

function parseMessage(raw: string): Message | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch (error) {
    if (!(error instanceof SyntaxError)) {
      throw error;
    }

    // quality-allow: a malformed client frame changes no room state; the protocol ignores it and its rate token was already charged.
    return null;
  }

  return typeof parsed === "object" && parsed !== null && !Array.isArray(parsed) ? parsed as Message : null;
}

function exceedsUtf8Bytes(text: string, limit: number): boolean {
  if (text.length > limit) {
    return true;
  }

  if (text.length * 3 <= limit) {
    return false;
  }

  let bytes = 0;
  for (let index = 0; index < text.length; index++) {
    const unit = text.charCodeAt(index);
    bytes += unit < 0x80 ? 1 : unit < 0x800 || (unit >= 0xd800 && unit <= 0xdfff) ? 2 : 3;
    if (bytes > limit) {
      return true;
    }
  }

  return false;
}

function participant(member: Member, slot: number): Message {
  return {
    userId: member.id,
    name: member.name,
    world: member.world,
    displayName: member.name,
    slot,
    state: "active",
    muted: false,
  };
}

function sharedQueue(value: unknown): Message[] | null {
  if (!Array.isArray(value)) {
    return null;
  }

  const entries: Message[] = [];
  for (const entry of value.slice(0, MAX_SHARED_QUEUE)) {
    if (typeof entry === "object" && entry !== null) {
      const fields = entry as Message;
      entries.push({
        url: typeof fields.url === "string" ? fields.url : null,
        title: typeof fields.title === "string" ? fields.title : null,
      });
    }
  }

  return entries;
}

function readGrants(value: unknown): Grant[] {
  if (!Array.isArray(value)) {
    return [];
  }

  const grants: Grant[] = [];
  for (const entry of value.slice(0, MAX_MEMBERS)) {
    if (typeof entry === "object" && entry !== null) {
      const fields = entry as Message;
      if (typeof fields.userId === "string" && typeof fields.flags === "number") {
        grants.push({ userId: fields.userId, flags: fields.flags & PERMISSION_GRANT_MASK });
      }
    }
  }

  return grants;
}

function roomDeadline(room: RoomState): number {
  const lifetimeEnd = room.createdAt + ROOM_LIFETIME_MS;
  return room.aloneSince === null ? lifetimeEnd : Math.min(lifetimeEnd, room.aloneSince + ALONE_TIMEOUT_MS);
}

export class PartyRoom extends DurableObject<Env> {
  async fetch(request: Request): Promise<Response> {
    const header = request.headers.get(JOIN_HEADER);
    if (header === null || request.headers.get("Upgrade") !== "websocket") {
      return new Response("bad request", { status: 400 });
    }

    const ticket = JSON.parse(decodeURIComponent(header)) as JoinTicket;
    const now = Date.now();
    const connections = this.connections();
    const host = connections.find((connection) => connection.member.role === "host");

    if (ticket.role === "host") {
      if (host !== undefined && host.member.id !== ticket.id) {
        return new Response("slot occupied", { status: 409 });
      }

      if (host !== undefined) {
        this.endRoom(null, "ended");
      } else {
        for (const stale of connections) {
          stale.socket.close(CloseCode.ended, "ended");
        }
      }

      const room: RoomState = {
        code: ticket.code,
        createdAt: now,
        aloneSince: now,
        grants: [],
        pendingTransfer: null,
        nextSequence: 1,
      };
      const response = this.accept(this.newMember(ticket, "host", 0, now, room));
      await this.ctx.storage.setAlarm(roomDeadline(room));
      return response;
    }

    if (host === undefined || host.member.room === null || host.member.room.code !== ticket.code) {
      return new Response("room not found", { status: 404 });
    }

    if (host.member.id === ticket.id) {
      return new Response("already hosting", { status: 409 });
    }

    let count = connections.length;
    for (const existing of connections) {
      if (existing.member.id === ticket.id) {
        existing.member.gone = true;
        existing.socket.serializeAttachment(existing.member);
        existing.socket.close(CloseCode.replaced, "replaced");
        count--;
      }
    }

    if (count >= MAX_MEMBERS) {
      return new Response("party full", { status: 409 });
    }

    const room = host.member.room;
    const sequence = room.nextSequence;
    room.nextSequence++;
    host.socket.serializeAttachment(host.member);
    return this.accept(this.newMember(ticket, "viewer", sequence, now, null));
  }

  async webSocketMessage(socket: WebSocket, raw: string | ArrayBuffer): Promise<void> {
    const member = memberOf(socket);
    if (member.gone) {
      return;
    }

    if (typeof raw !== "string") {
      socket.close(CloseCode.unsupported, "text frames only");
      await this.depart(socket, member);
      return;
    }

    if (exceedsUtf8Bytes(raw, MAX_MESSAGE_BYTES)) {
      socket.close(CloseCode.tooBig, "message too big");
      await this.depart(socket, member);
      return;
    }

    const verdict = this.charge(member, Date.now());
    socket.serializeAttachment(member);
    if (verdict === "close") {
      socket.close(CloseCode.rate, "rate limited");
      await this.depart(socket, member);
      return;
    }

    if (verdict === "drop") {
      return;
    }

    const message = parseMessage(raw);
    if (message === null) {
      return;
    }

    switch (message.type) {
      case SignalType.state:
        await this.onState(socket, message);
        return;
      case SignalType.join:
        this.onJoin(socket, member);
        return;
      case SignalType.leave:
        socket.close(CloseCode.ended, "left");
        await this.depart(socket, member);
        return;
      case SignalType.control:
        this.onControl(member, message);
        return;
      case SignalType.transfer:
        this.onTransfer(socket, member, message);
        return;
    }
  }

  async webSocketClose(socket: WebSocket): Promise<void> {
    await this.depart(socket, memberOf(socket));
  }

  async webSocketError(socket: WebSocket): Promise<void> {
    await this.depart(socket, memberOf(socket));
  }

  async alarm(): Promise<void> {
    const host = this.connections().find((connection) => connection.member.role === "host");
    if (host === undefined || host.member.room === null) {
      this.endRoom(null, "ended");
      return;
    }

    const deadline = roomDeadline(host.member.room);
    if (deadline <= Date.now()) {
      this.endRoom(null, host.member.room.aloneSince === null ? "expired" : "idle");
      return;
    }

    await this.ctx.storage.setAlarm(deadline);
  }

  private newMember(ticket: JoinTicket, role: "host" | "viewer", sequence: number, now: number, room: RoomState | null): Member {
    const rate = role === "host" ? HOST_RATE : VIEWER_RATE;
    return {
      id: ticket.id,
      name: ticket.name,
      world: ticket.world,
      role,
      joined: role === "host",
      pendingJoin: false,
      gone: false,
      sequence,
      bucket: { tokens: rate.capacity, at: now, windowStart: now, dropped: 0 },
      room,
    };
  }

  private accept(member: Member): Response {
    const pair = new WebSocketPair();
    this.ctx.acceptWebSocket(pair[1]);
    pair[1].serializeAttachment(member);
    return new Response(null, { status: 101, webSocket: pair[0] });
  }

  private connections(exclude: WebSocket | null = null): Connection[] {
    const result: Connection[] = [];
    for (const socket of this.ctx.getWebSockets()) {
      if (socket === exclude || socket.readyState !== OPEN) {
        continue;
      }

      const member = memberOf(socket);
      if (!member.gone) {
        result.push({ socket, member });
      }
    }

    return result;
  }

  private charge(member: Member, now: number): "accept" | "drop" | "close" {
    const rate = member.role === "host" ? HOST_RATE : VIEWER_RATE;
    const bucket = member.bucket;
    bucket.tokens = Math.min(rate.capacity, bucket.tokens + (now - bucket.at) / rate.refillMs);
    bucket.at = now;
    if (bucket.tokens >= 1) {
      bucket.tokens -= 1;
      return "accept";
    }

    if (now - bucket.windowStart > DROP_WINDOW_MS) {
      bucket.windowStart = now;
      bucket.dropped = 0;
    }

    bucket.dropped++;
    return bucket.dropped > DROP_LIMIT ? "close" : "drop";
  }

  private roster(connections: Connection[]): Message[] {
    const host = connections.find((connection) => connection.member.role === "host");
    const viewers = connections
      .filter((connection) => connection.member.role === "viewer" && (connection.member.joined || connection.member.pendingJoin))
      .sort((left, right) => left.member.sequence - right.member.sequence);
    const result: Message[] = host === undefined ? [] : [participant(host.member, 0)];
    viewers.forEach((viewer, index) => result.push(participant(viewer.member, index + 1)));
    return result;
  }

  private members(room: RoomState, connections: Connection[]): Grant[] {
    const result: Grant[] = [];
    for (const connection of connections) {
      if (connection.member.role !== "viewer" || !connection.member.joined) {
        continue;
      }

      const grant = room.grants.find((entry) => entry.userId === connection.member.id);
      result.push({ userId: connection.member.id, flags: (grant?.flags ?? 0) | PERMISSION_CAN_HOST });
    }

    return result;
  }

  private rosterMessage(host: Member, room: RoomState, connections: Connection[], guestPermissions: number | null): Message {
    return {
      type: SignalType.roster,
      hostId: host.id,
      code: room.code,
      features: FEATURE_PARTY,
      participants: this.roster(connections),
      members: this.members(room, connections),
      guestPermissions,
    };
  }

  private snapshot(host: Member, room: RoomState, message: Message, connections: Connection[]): Message {
    const state: Message = {};
    for (const [field, kind] of Object.entries(forwardedStateFields)) {
      if (typeof message[field] === kind) {
        state[field] = message[field];
      }
    }

    state.upcomingQueue = sharedQueue(message.upcomingQueue);
    state.hostId = host.id;
    state.code = room.code;
    state.features = FEATURE_PARTY;
    state.stateAtUnixMs = Date.now();
    state.members = this.members(room, connections);
    state.participants = this.roster(connections);
    return state;
  }

  private async onState(socket: WebSocket, message: Message): Promise<void> {
    const connections = this.connections();
    const member = connections.find((connection) => connection.socket === socket)?.member;
    const room = member?.room ?? null;
    if (member === undefined || member.role !== "host" || room === null) {
      return;
    }

    room.grants = readGrants(message.members);
    const transferTo = room.pendingTransfer;
    if (transferTo !== null) {
      const target = connections.find((connection) => connection.member.id === transferTo
        && connection.member.role === "viewer" && connection.member.joined);
      if (target !== undefined) {
        room.pendingTransfer = null;
        await this.transfer({ socket, member }, target, message, connections);
        return;
      }

      if (!connections.some((connection) => connection.member.id === transferTo)) {
        room.pendingTransfer = null;
      }
    }

    const newcomers: Connection[] = [];
    for (const connection of connections) {
      if (connection.member.pendingJoin) {
        connection.member.pendingJoin = false;
        connection.member.joined = true;
        connection.socket.serializeAttachment(connection.member);
        newcomers.push(connection);
      }
    }

    await this.updateAlone(room, connections, Date.now());
    socket.serializeAttachment(member);
    const state = this.snapshot(member, room, message, connections);
    send(socket, { ...state, type: SignalType.state });
    for (const connection of connections) {
      if (connection.member.role !== "viewer" || !connection.member.joined) {
        continue;
      }

      if (newcomers.includes(connection)) {
        send(connection.socket, { ...state, type: SignalType.joined });
        continue;
      }

      send(connection.socket, { ...state, type: SignalType.state });
      if (newcomers.length > 0) {
        send(connection.socket, this.rosterMessage(member, room, connections, (state.guestPermissions as number | undefined) ?? null));
      }
    }
  }

  private onJoin(socket: WebSocket, member: Member): void {
    if (member.role !== "viewer" || member.joined) {
      return;
    }

    const connections = this.connections();
    const host = connections.find((connection) => connection.member.role === "host");
    const room = host?.member.room ?? null;
    if (host === undefined || room === null) {
      return;
    }

    member.pendingJoin = true;
    socket.serializeAttachment(member);
    const updated = this.connections();
    send(host.socket, this.rosterMessage(host.member, room, updated, null));
  }

  private onControl(member: Member, message: Message): void {
    if (member.role !== "viewer" || !member.joined || typeof message.action !== "string"
      || CONTROL_ACTIONS[message.action] !== true) {
      return;
    }

    const host = this.connections().find((connection) => connection.member.role === "host");
    if (host === undefined) {
      return;
    }

    send(host.socket, {
      type: SignalType.controlRequest,
      userId: member.id,
      action: message.action,
      positionSeconds: typeof message.positionSeconds === "number" ? message.positionSeconds : null,
      from: participant(member, member.sequence),
    });
  }

  private onTransfer(socket: WebSocket, member: Member, message: Message): void {
    const room = member.room;
    if (member.role !== "host" || room === null || typeof message.userId !== "string") {
      return;
    }

    const connections = this.connections();
    const target = connections.find((connection) => connection.member.id === message.userId
      && connection.member.role === "viewer" && connection.member.joined);
    if (target === undefined) {
      return;
    }

    room.pendingTransfer = target.member.id;
    socket.serializeAttachment(member);
    send(socket, this.rosterMessage(member, room, connections, null));
  }

  private async transfer(previous: Connection, next: Connection, message: Message, connections: Connection[]): Promise<void> {
    const room = previous.member.room;
    if (room === null) {
      return;
    }

    previous.member.role = "viewer";
    previous.member.room = null;
    previous.member.sequence = room.nextSequence;
    room.nextSequence++;
    room.grants = [];
    next.member.role = "host";
    next.member.room = room;
    await this.updateAlone(room, connections, Date.now());
    previous.socket.serializeAttachment(previous.member);
    next.socket.serializeAttachment(next.member);

    const state = this.snapshot(next.member, room, message, connections);
    const changed = { ...state, type: SignalType.hostChanged, hostId: next.member.id, userId: previous.member.id };
    for (const connection of connections) {
      if (connection.member.joined) {
        send(connection.socket, changed);
      }
    }
  }

  private async updateAlone(room: RoomState, connections: Connection[], now: number): Promise<void> {
    const hasCompany = connections.some((connection) => connection.member.role === "viewer" && connection.member.joined);
    const aloneSince = hasCompany ? null : room.aloneSince ?? now;
    if (aloneSince === room.aloneSince) {
      return;
    }

    room.aloneSince = aloneSince;
    await this.ctx.storage.setAlarm(roomDeadline(room));
  }

  private async depart(socket: WebSocket, member: Member): Promise<void> {
    if (member.gone) {
      return;
    }

    member.gone = true;
    socket.serializeAttachment(member);

    if (member.role === "host") {
      this.endRoom(socket, "host_left");
      return;
    }

    const connections = this.connections(socket);
    const host = connections.find((connection) => connection.member.role === "host");
    const room = host?.member.room ?? null;
    if (host === undefined || room === null || (!member.joined && !member.pendingJoin)) {
      return;
    }

    room.grants = room.grants.filter((grant) => grant.userId !== member.id);
    if (room.pendingTransfer === member.id) {
      room.pendingTransfer = null;
    }

    await this.updateAlone(room, connections, Date.now());
    host.socket.serializeAttachment(host.member);
    const left = { type: SignalType.left, userId: member.id, hostId: host.member.id };
    const roster = this.rosterMessage(host.member, room, connections, null);
    for (const connection of connections) {
      if (connection.member.joined) {
        send(connection.socket, left);
        send(connection.socket, roster);
      }
    }
  }

  private endRoom(exclude: WebSocket | null, reason: string): void {
    const connections = this.connections(exclude);
    const host = connections.find((connection) => connection.member.role === "host")
      ?? (exclude === null ? undefined : { socket: exclude, member: memberOf(exclude) });
    for (const connection of connections) {
      send(connection.socket, { type: SignalType.ended, hostId: host?.member.id ?? null, reason });
      connection.member.gone = true;
      connection.socket.serializeAttachment(connection.member);
      connection.socket.close(CloseCode.ended, "ended");
    }

    this.ctx.waitUntil(this.ctx.storage.deleteAlarm());
  }
}
