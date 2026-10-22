import { DurableObject } from "cloudflare:workers";
import type { Env } from "./index";
import type { JamTrack } from "./jam";
import {
  ALONE_TIMEOUT_MS,
  CONTROL_ACTIONS,
  CloseCode,
  DECLINE_DENIED,
  FEATURE_PARTY,
  HOST_RATE,
  MAX_ADMITTED,
  MAX_FAILURE_REASON,
  MAX_MEMBERS,
  MAX_MESSAGE_BYTES,
  MAX_PENDING_SUGGESTIONS,
  MAX_SHARED_QUEUE,
  MAX_SUGGESTION_URL,
  PERMISSION_CAN_HOST,
  PERMISSION_GRANT_MASK,
  REACTION_KINDS,
  REACTION_RATE,
  ROOM_LIFETIME_MS,
  SignalType,
  VIEWER_RATE,
  locationId,
  nearbyCode,
  normalizeCode,
} from "./protocol";
import { type Bucket, type Message, OPEN, cappedText, charge, exceedsUtf8Bytes, freshBucket, parseMessage, send } from "./wire";

export const JOIN_HEADER = "X-Relay-Join";
// The slot registry asks a room who holds its slot on this path; client requests reach rooms only as /rt.
export const PROBE_PATH = "/probe";

// nearby: the ticket came from a nearby list, so code is the room's nearby code and the join always needs approval.
export interface JoinTicket {
  role: "host" | "viewer";
  code: string;
  id: string;
  name: string;
  world: string;
  address: string;
  nearby?: boolean;
}

// What a discoverable room shows in a nearby list: the code here is its nearby code, never the room code, and the
// only name is the host's character name, which the host agreed to show by turning discovery on.
export interface RoomListing {
  code: string;
  territoryId: number;
  worldId: number;
  hostName: string;
  memberCount: number;
  title: string | null;
  jamId: string | null;
  track: JamTrack | null;
}

// occupied: a host ticket from another character would be refused. hostId: the connected host. address: the
// creator's client address, which keeps counting against that address for the room's whole life. listing: set only
// while the host lets nearby players find the room.
export interface SlotOccupant {
  occupied: boolean;
  hostId: string | null;
  address: string | null;
  listing?: RoomListing | null;
}

export const VACANT: SlotOccupant = { occupied: false, hostId: null, address: null, listing: null };

export function isProbe(request: Request): boolean {
  return new URL(request.url).pathname === PROBE_PATH;
}

export function occupantResponse(occupant: SlotOccupant): Response {
  return Response.json(occupant);
}

export function ticketMatches(ticket: JoinTicket, roomCode: string, nearby: string | null): boolean {
  return ticket.nearby === true ? nearby !== null && ticket.code === nearby : ticket.code === roomCode;
}

interface Grant {
  userId: string;
  flags: number;
}

interface NearbySpot {
  code: string;
  territoryId: number;
  worldId: number;
}

// nearby stays null until the host's state turns discovery on, so a private party keeps its attachment as small as
// before.
interface RoomState {
  code: string;
  address: string;
  createdAt: number;
  aloneSince: number | null;
  grants: Grant[];
  pendingTransfer: string | null;
  nextSequence: number;
  approvalRequired: boolean;
  admitted: string[];
  nearby: NearbySpot | null;
}

interface Member {
  id: string;
  name: string;
  world: string;
  role: "host" | "viewer";
  joined: boolean;
  pendingJoin: boolean;
  awaiting: boolean;
  nearby: boolean;
  gone: boolean;
  sequence: number;
  bucket: Bucket;
  reactions: Bucket;
  suggestions: string[];
  room: RoomState | null;
}

interface Connection {
  socket: WebSocket;
  member: Member;
}

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

const SUGGESTION_ID = /^[A-Za-z0-9-]{1,64}$/;

function memberOf(socket: WebSocket): Member {
  return socket.deserializeAttachment() as Member;
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

function admit(room: RoomState, id: string): void {
  room.admitted = room.admitted.filter((entry) => entry !== id);
  room.admitted.push(id);
  if (room.admitted.length > MAX_ADMITTED) {
    room.admitted.shift();
  }
}

function suggestionUrl(value: unknown): string | null {
  if (typeof value !== "string" || value.length > MAX_SUGGESTION_URL || !URL.canParse(value)) {
    return null;
  }

  const protocol = new URL(value).protocol;
  return protocol === "https:" || protocol === "http:" ? value : null;
}

// The host's state decides discovery: true keeps (or mints) the nearby code, false drops the listing, and a state
// without the flag keeps a listing that already exists. A state without a location keeps the last one reported.
// discoverable counts only beside nearbyOptIn: 1.3.4.0 and earlier send discoverable: true by default and hide the
// switch on a relay, so their hosts never chose to be listed.
function nearbySpot(room: RoomState, message: Message): NearbySpot | null {
  const discoverable = message.discoverable === true ? message.nearbyOptIn === true : message.discoverable;
  if (discoverable === false || (discoverable !== true && room.nearby === null)) {
    return null;
  }

  return {
    code: room.nearby?.code ?? nearbyCode(room.code),
    territoryId: locationId(message.territoryId) ?? room.nearby?.territoryId ?? 0,
    worldId: locationId(message.worldId) ?? room.nearby?.worldId ?? 0,
  };
}

export class PartyRoom extends DurableObject<Env> {
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
        address: ticket.address,
        createdAt: now,
        aloneSince: now,
        grants: [],
        pendingTransfer: null,
        nextSequence: 1,
        approvalRequired: false,
        admitted: [],
        nearby: null,
      };
      const response = this.accept(this.newMember(ticket, "host", 0, now, room));
      await this.ctx.storage.setAlarm(roomDeadline(room));
      return response;
    }

    const joining = host?.member.room ?? null;
    if (host === undefined || joining === null || !ticketMatches(ticket, joining.code, joining.nearby?.code ?? null)) {
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

    const room = joining;
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

    const message = parseMessage(raw);
    const now = Date.now();
    const verdict = message?.type === SignalType.react
      ? charge(member.reactions, REACTION_RATE, now)
      : charge(member.bucket, member.role === "host" ? HOST_RATE : VIEWER_RATE, now);
    socket.serializeAttachment(member);
    if (verdict === "close") {
      socket.close(CloseCode.rate, "rate limited");
      await this.depart(socket, member);
      return;
    }

    if (verdict === "drop" || message === null) {
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
        await this.depart(socket, member, true);
        return;
      case SignalType.control:
        this.onControl(member, message);
        return;
      case SignalType.transfer:
        this.onTransfer(socket, member, message);
        return;
      case SignalType.approve:
        this.onApprove(socket, member, message);
        return;
      case SignalType.deny:
        await this.onDeny(member, message);
        return;
      case SignalType.kick:
        await this.onKick(member, message);
        return;
      case SignalType.react:
        this.onReact(socket, member, message);
        return;
      case SignalType.queueSuggest:
        this.onQueueSuggest(socket, member, message);
        return;
      case SignalType.queueApprove:
        this.onQueueDecision(member, message, "accepted");
        return;
      case SignalType.queueDeny:
        this.onQueueDecision(member, message, "denied");
        return;
      case SignalType.playbackFailed:
        this.onPlaybackFailed(member, message);
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

  private occupant(): SlotOccupant {
    const connections = this.connections();
    const host = connections.find((connection) => connection.member.role === "host");
    if (host === undefined) {
      return VACANT;
    }

    const room = host.member.room;
    return {
      occupied: true,
      hostId: host.member.id,
      address: room?.address ?? null,
      listing: room === null ? null : this.listing(host.member, room, connections),
    };
  }

  private listing(host: Member, room: RoomState, connections: Connection[]): RoomListing | null {
    const nearby = room.nearby;
    if (nearby === null || nearby.territoryId === 0 || nearby.worldId === 0) {
      return null;
    }

    let memberCount = 1;
    for (const connection of connections) {
      if (connection.member.role === "viewer" && connection.member.joined) {
        memberCount++;
      }
    }

    return {
      code: nearby.code,
      territoryId: nearby.territoryId,
      worldId: nearby.worldId,
      hostName: host.name,
      memberCount,
      title: null,
      jamId: null,
      track: null,
    };
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
      awaiting: false,
      nearby: role === "viewer" && ticket.nearby === true,
      gone: false,
      sequence,
      bucket: freshBucket(rate, now),
      reactions: freshBucket(REACTION_RATE, now),
      suggestions: [],
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
    if (typeof message.approvalRequired === "boolean") {
      room.approvalRequired = message.approvalRequired;
    }

    room.nearby = nearbySpot(room, message);

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
    let approvalLifted = false;
    for (const connection of connections) {
      const candidate = connection.member;
      const lifted = candidate.awaiting && !candidate.nearby && !room.approvalRequired;
      if (candidate.pendingJoin || lifted) {
        approvalLifted ||= lifted;
        candidate.pendingJoin = false;
        candidate.awaiting = false;
        candidate.joined = true;
        connection.socket.serializeAttachment(candidate);
        admit(room, candidate.id);
        newcomers.push(connection);
      }
    }

    await this.updateAlone(room, connections, Date.now());
    socket.serializeAttachment(member);
    const state = this.snapshot(member, room, message, connections);
    send(socket, { ...state, type: SignalType.state });
    if (approvalLifted) {
      send(socket, this.rosterMessage(member, room, connections, null));
    }

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

    if (!member.pendingJoin && (room.approvalRequired || member.nearby) && !room.admitted.includes(member.id)) {
      member.awaiting = true;
      socket.serializeAttachment(member);
      send(socket, { type: SignalType.joinPending, hostId: host.member.id });
      send(host.socket, {
        type: SignalType.joinRequest,
        userId: member.id,
        hostId: host.member.id,
        from: participant(member, member.sequence),
      });
      return;
    }

    member.awaiting = false;
    member.pendingJoin = true;
    socket.serializeAttachment(member);
    const updated = this.connections();
    send(host.socket, this.rosterMessage(host.member, room, updated, null));
  }

  private onApprove(socket: WebSocket, member: Member, message: Message): void {
    const room = member.room;
    if (member.role !== "host" || room === null) {
      return;
    }

    const target = this.connections().find((connection) => connection.member.awaiting
      && connection.member.id === message.userId);
    if (target === undefined) {
      return;
    }

    target.member.awaiting = false;
    target.member.pendingJoin = true;
    target.socket.serializeAttachment(target.member);
    admit(room, target.member.id);
    socket.serializeAttachment(member);
    send(socket, this.rosterMessage(member, room, this.connections(), null));
  }

  private async onDeny(member: Member, message: Message): Promise<void> {
    if (member.role !== "host") {
      return;
    }

    const target = this.connections().find((connection) => connection.member.awaiting
      && connection.member.id === message.userId);
    if (target === undefined) {
      return;
    }

    send(target.socket, { type: SignalType.declined, reason: DECLINE_DENIED, hostId: member.id });
    target.socket.close(CloseCode.ended, "declined");
    await this.depart(target.socket, target.member);
  }

  // Identities are self-declared, so a kick only closes the current connection; the same name may come back, and
  // with approval on it has to be let in again.
  private async onKick(member: Member, message: Message): Promise<void> {
    if (member.role !== "host") {
      return;
    }

    const target = this.connections().find((connection) => connection.member.role === "viewer"
      && connection.member.id === message.userId);
    if (target === undefined) {
      return;
    }

    send(target.socket, { type: SignalType.kicked, hostId: member.id, userId: target.member.id });
    target.socket.close(CloseCode.kicked, "kicked");
    await this.depart(target.socket, target.member, true);
  }

  private onReact(socket: WebSocket, member: Member, message: Message): void {
    const reaction = message.reaction;
    if (!member.joined || typeof reaction !== "number" || !Number.isInteger(reaction) || reaction < 0
      || reaction >= REACTION_KINDS) {
      return;
    }

    const forwarded = { type: SignalType.reaction, reaction, userId: member.id };
    for (const connection of this.connections(socket)) {
      if (connection.member.joined) {
        send(connection.socket, forwarded);
      }
    }
  }

  private onQueueSuggest(socket: WebSocket, member: Member, message: Message): void {
    const suggestionId = message.suggestionId;
    const url = suggestionUrl(message.url);
    if (member.role !== "viewer" || !member.joined || typeof suggestionId !== "string"
      || !SUGGESTION_ID.test(suggestionId) || url === null) {
      return;
    }

    const host = this.connections().find((connection) => connection.member.role === "host");
    if (host === undefined) {
      return;
    }

    member.suggestions = member.suggestions.filter((entry) => entry !== suggestionId);
    member.suggestions.push(suggestionId);
    if (member.suggestions.length > MAX_PENDING_SUGGESTIONS) {
      member.suggestions.shift();
    }

    socket.serializeAttachment(member);
    send(host.socket, {
      type: SignalType.queueSuggestion,
      suggestionId,
      url,
      userId: member.id,
      from: participant(member, member.sequence),
    });
  }

  private onQueueDecision(member: Member, message: Message, reason: "accepted" | "denied"): void {
    const suggestionId = message.suggestionId;
    if (member.role !== "host" || typeof suggestionId !== "string") {
      return;
    }

    for (const connection of this.connections()) {
      const index = connection.member.suggestions.indexOf(suggestionId);
      if (index >= 0) {
        connection.member.suggestions.splice(index, 1);
        connection.socket.serializeAttachment(connection.member);
        send(connection.socket, { type: SignalType.queueSuggestionResult, suggestionId, reason });
        return;
      }
    }
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

  // Only the host learns who could not play the video, so it can pick another link; the rest of the room hears
  // nothing.
  private onPlaybackFailed(member: Member, message: Message): void {
    const url = message.url;
    if (member.role !== "viewer" || !member.joined || typeof url !== "string" || url.length === 0
      || url.length > MAX_SUGGESTION_URL) {
      return;
    }

    const host = this.connections().find((connection) => connection.member.role === "host");
    if (host === undefined) {
      return;
    }

    send(host.socket, {
      type: SignalType.viewerFailed,
      hostId: host.member.id,
      userId: member.id,
      url,
      reason: cappedText(message.reason, MAX_FAILURE_REASON),
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
    admit(room, previous.member.id);
    next.member.role = "host";
    next.member.room = room;
    next.member.suggestions = [];
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

    for (const connection of connections) {
      if (connection.member.awaiting) {
        send(next.socket, {
          type: SignalType.joinRequest,
          userId: connection.member.id,
          hostId: next.member.id,
          from: participant(connection.member, connection.member.sequence),
        });
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

  private async depart(socket: WebSocket, member: Member, forget = false): Promise<void> {
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
    if (host === undefined || room === null) {
      return;
    }

    if (member.awaiting) {
      send(host.socket, { type: SignalType.left, userId: member.id, hostId: host.member.id });
      return;
    }

    if (!member.joined && !member.pendingJoin) {
      return;
    }

    room.grants = room.grants.filter((grant) => grant.userId !== member.id);
    if (room.pendingTransfer === member.id) {
      room.pendingTransfer = null;
    }

    if (forget) {
      room.admitted = room.admitted.filter((entry) => entry !== member.id);
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
