import { DurableObject } from "cloudflare:workers";
import type { Env } from "./index";
import {
  ALONE_TIMEOUT_MS,
  CloseCode,
  DECLINE_DENIED,
  HOST_RATE,
  JAM_CONTROL_ACTIONS,
  JAM_GUEST_PERMISSIONS,
  JAM_PERMISSION_ADD,
  JAM_PERMISSION_CONTROL,
  JAM_REACTION_KINDS,
  JamSignal,
  MAX_ADMITTED,
  MAX_JAM_QUEUE,
  MAX_JAM_TITLE,
  MAX_MEMBERS,
  MAX_MESSAGE_BYTES,
  MAX_THUMBNAIL_URL,
  MAX_TRACK_TEXT,
  REACTION_RATE,
  ROOM_LIFETIME_MS,
  THUMBNAIL_HOSTS,
  VIEWER_RATE,
  locationId,
  nearbyCode,
} from "./protocol";
import {
  JOIN_HEADER,
  type JoinTicket,
  type RoomListing,
  type SlotOccupant,
  VACANT,
  isProbe,
  occupantResponse,
  ticketMatches,
} from "./room";
import { type Bucket, type Message, OPEN, cappedText, charge, exceedsUtf8Bytes, freshBucket, parseMessage, send } from "./wire";

const ROOM_KEY = "room";
const VIDEO_ID = /^[A-Za-z0-9_-]{1,64}$/;
const MAX_DURATION_SECONDS = 24 * 60 * 60;
const POSITION_JUMP_SECONDS = 2;

export interface JamTrack {
  videoId: string;
  title: string | null;
  author: string | null;
  thumbnailUrl: string | null;
  durationSeconds: number | null;
}

interface JamEntry {
  entryId: number;
  track: JamTrack;
  addedByUserId: string;
  addedByName: string;
}

interface PlayState {
  track: JamTrack | null;
  positionSeconds: number;
  paused: boolean;
  stateAtUnixMs: number;
}

// nearbyCode is set only while the host lets nearby players find the Jam; admitted holds the last characters the
// host let in, so they come back after a dropped connection without asking again.
export interface JamRoomState {
  code: string;
  address: string;
  jamId: string;
  title: string;
  createdAt: number;
  aloneSince: number | null;
  entries: JamEntry[];
  nextEntryId: number;
  queueVersion: number;
  play: PlayState;
  guestPermissions: number;
  approvalRequired: boolean;
  discoverable: boolean;
  nearbyCode: string | null;
  admitted: string[];
}

type JamSettings = "guestPermissions" | "approvalRequired" | "discoverable" | "nearbyCode" | "admitted";
type StoredJamRoom = Omit<JamRoomState, JamSettings> & Partial<Pick<JamRoomState, JamSettings>>;

interface JamLocation {
  territoryId: number;
  worldId: number;
}

// location is the host's own zone, reported with jam.nearby only while the Jam is discoverable; it lives in the
// attachment so a host walking between zones writes no storage.
interface Member {
  id: string;
  name: string;
  world: string;
  role: "host" | "guest";
  joined: boolean;
  awaiting: boolean;
  nearby: boolean;
  gone: boolean;
  sequence: number;
  bucket: Bucket;
  reactions: Bucket;
  location: JamLocation | null;
}

interface Connection {
  socket: WebSocket;
  member: Member;
}

function memberOf(socket: WebSocket): Member {
  return socket.deserializeAttachment() as Member;
}

function thumbnailUrl(value: unknown): string | null {
  if (typeof value !== "string" || value.length > MAX_THUMBNAIL_URL || !URL.canParse(value)) {
    return null;
  }

  const url = new URL(value);
  const host = url.hostname;
  const allowed = THUMBNAIL_HOSTS.some((suffix) => host === suffix || host.endsWith(`.${suffix}`));
  return url.protocol === "https:" && allowed ? url.href : null;
}

export function parseTrack(value: unknown): JamTrack | null {
  if (typeof value !== "object" || value === null) {
    return null;
  }

  const fields = value as Message;
  if (typeof fields.videoId !== "string" || !VIDEO_ID.test(fields.videoId)) {
    return null;
  }

  const duration = fields.durationSeconds;
  return {
    videoId: fields.videoId,
    title: cappedText(fields.title, MAX_TRACK_TEXT),
    author: cappedText(fields.author, MAX_TRACK_TEXT),
    thumbnailUrl: thumbnailUrl(fields.thumbnailUrl),
    durationSeconds: typeof duration === "number" && Number.isFinite(duration) && duration > 0
      && duration <= MAX_DURATION_SECONDS ? duration : null,
  };
}

function roomDeadline(room: JamRoomState): number {
  const lifetimeEnd = room.createdAt + ROOM_LIFETIME_MS;
  return room.aloneSince === null ? lifetimeEnd : Math.min(lifetimeEnd, room.aloneSince + ALONE_TIMEOUT_MS);
}

function guestMay(member: Member, room: JamRoomState, permission: number): boolean {
  return member.role === "host" || (room.guestPermissions & permission) !== 0;
}

// One Jam per room slot. Membership and rate buckets live in WebSocket attachments; the queue, the last playback
// state, the host's settings and the admitted list outgrow an attachment, so they sit under one storage key that is
// written only when one of them changes (a heartbeat that merely confirms the expected position stays in memory).
export class JamRoom extends DurableObject<Env> {
  private cached: JamRoomState | null | undefined;

  async fetch(request: Request): Promise<Response> {
    if (isProbe(request)) {
      return occupantResponse(await this.occupant());
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

      await this.endRoom(null, "ended");
      const room: JamRoomState = {
        code: ticket.code,
        address: ticket.address,
        jamId: crypto.randomUUID(),
        title: "",
        createdAt: now,
        aloneSince: now,
        entries: [],
        nextEntryId: 1,
        queueVersion: 0,
        play: { track: null, positionSeconds: 0, paused: true, stateAtUnixMs: now },
        guestPermissions: JAM_GUEST_PERMISSIONS,
        approvalRequired: false,
        discoverable: false,
        nearbyCode: null,
        admitted: [],
      };
      await this.save(room);
      await this.ctx.storage.setAlarm(roomDeadline(room));
      return this.accept(this.newMember(ticket, "host", now));
    }

    const room = await this.room();
    if (host === undefined || room === null || !ticketMatches(ticket, room.code, room.nearbyCode)) {
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
      return new Response("jam full", { status: 409 });
    }

    return this.accept(this.newMember(ticket, "guest", now));
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

    const parsed = parseMessage(raw);
    const now = Date.now();
    const verdict = parsed?.type === JamSignal.react
      ? charge(member.reactions, REACTION_RATE, now)
      : charge(member.bucket, member.role === "host" ? HOST_RATE : VIEWER_RATE, now);
    socket.serializeAttachment(member);
    if (verdict === "close") {
      socket.close(CloseCode.rate, "rate limited");
      await this.depart(socket, member);
      return;
    }

    const message = verdict === "drop" ? null : parsed;
    const room = message === null ? null : await this.room();
    if (message === null || room === null) {
      return;
    }

    switch (message.type) {
      case JamSignal.start:
        await this.onStart(socket, member, room, message);
        return;
      case JamSignal.join:
        await this.onJoin(socket, member, room, message);
        return;
      case JamSignal.leave:
      case JamSignal.end:
        socket.close(CloseCode.ended, "left");
        await this.depart(socket, member, true);
        return;
      case JamSignal.state:
        await this.onState(member, room, message);
        return;
      case JamSignal.control:
        this.onControl(member, room, message);
        return;
      case JamSignal.transfer:
        this.onTransfer(socket, member, room, message);
        return;
      case JamSignal.queueAdd:
        await this.onQueueAdd(member, room, message);
        return;
      case JamSignal.queueAdvance:
        await this.onQueueAdvance(member, room, message);
        return;
      case JamSignal.queueRemove:
        await this.onQueueRemove(member, room, message);
        return;
      case JamSignal.queueMove:
        await this.onQueueMove(member, room, message);
        return;
      case JamSignal.approve:
        await this.onApprove(member, room, message);
        return;
      case JamSignal.deny:
        await this.onDeny(member, room, message);
        return;
      case JamSignal.kick:
        await this.onKick(member, room, message);
        return;
      case JamSignal.react:
        this.onReact(socket, member, room, message);
        return;
      case JamSignal.settings:
        await this.onSettings(member, room, message);
        return;
      case JamSignal.nearby:
        this.onNearby(socket, member, message);
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
    const room = await this.room();
    const host = this.connections().find((connection) => connection.member.role === "host");
    if (host === undefined || room === null) {
      await this.endRoom(null, "ended");
      return;
    }

    const deadline = roomDeadline(room);
    if (deadline <= Date.now()) {
      await this.endRoom(null, room.aloneSince === null ? "expired" : "idle");
      return;
    }

    await this.ctx.storage.setAlarm(deadline);
  }

  // Rooms stored before the host settings existed load with the defaults a new Jam starts with.
  private async room(): Promise<JamRoomState | null> {
    if (this.cached === undefined) {
      const stored = await this.ctx.storage.get<StoredJamRoom>(ROOM_KEY);
      this.cached = stored === undefined
        ? null
        : {
          ...stored,
          guestPermissions: stored.guestPermissions ?? JAM_GUEST_PERMISSIONS,
          approvalRequired: stored.approvalRequired ?? false,
          discoverable: stored.discoverable ?? false,
          nearbyCode: stored.nearbyCode ?? null,
          admitted: stored.admitted ?? [],
        };
    }

    return this.cached;
  }

  private async save(room: JamRoomState): Promise<void> {
    this.cached = room;
    await this.ctx.storage.put(ROOM_KEY, room);
  }

  private async occupant(): Promise<SlotOccupant> {
    const connections = this.connections();
    const host = connections.find((connection) => connection.member.role === "host");
    if (host === undefined) {
      return VACANT;
    }

    const room = await this.room();
    return {
      occupied: true,
      hostId: host.member.id,
      address: room?.address ?? null,
      listing: room === null ? null : this.listing(host.member, room, connections),
    };
  }

  private listing(host: Member, room: JamRoomState, connections: Connection[]): RoomListing | null {
    const location = host.location;
    if (!room.discoverable || room.nearbyCode === null || location === null || !host.joined) {
      return null;
    }

    return {
      code: room.nearbyCode,
      territoryId: location.territoryId,
      worldId: location.worldId,
      hostName: host.name,
      memberCount: this.joined(connections).length,
      title: room.title.length > 0 ? room.title : null,
      jamId: room.jamId,
      track: room.play.track,
    };
  }

  private newMember(ticket: JoinTicket, role: "host" | "guest", now: number): Member {
    return {
      id: ticket.id,
      name: ticket.name,
      world: ticket.world,
      role,
      joined: false,
      awaiting: false,
      nearby: role === "guest" && ticket.nearby === true,
      gone: false,
      sequence: now,
      bucket: freshBucket(role === "host" ? HOST_RATE : VIEWER_RATE, now),
      reactions: freshBucket(REACTION_RATE, now),
      location: null,
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

  private joined(connections: Connection[]): Connection[] {
    return connections.filter((connection) => connection.member.joined);
  }

  private members(room: JamRoomState, connections: Connection[]): Message[] {
    return this.joined(connections)
      .sort((left, right) => (left.member.role === "host" ? -1 : right.member.role === "host" ? 1 : 0)
        || left.member.sequence - right.member.sequence)
      .map((connection) => ({
        userId: connection.member.id,
        displayName: connection.member.name,
        handle: "",
        avatarUrl: null,
        isHost: connection.member.role === "host",
        permissions: connection.member.role === "host" ? JAM_GUEST_PERMISSIONS : room.guestPermissions,
      }));
  }

  private hostOf(connections: Connection[]): Connection | undefined {
    return connections.find((connection) => connection.member.role === "host");
  }

  private roster(room: JamRoomState, connections: Connection[]): Message {
    return {
      type: JamSignal.roster,
      jamId: room.jamId,
      title: room.title,
      guestPermissions: room.guestPermissions,
      approvalRequired: room.approvalRequired,
      discoverable: room.discoverable,
      jamMembers: this.members(room, connections),
    };
  }

  private queue(room: JamRoomState): Message {
    return { type: JamSignal.queue, jamId: room.jamId, entries: room.entries, queueVersion: room.queueVersion };
  }

  private snapshot(type: string, room: JamRoomState, connections: Connection[]): Message {
    return {
      ...this.roster(room, connections),
      type,
      code: room.code,
      hostId: this.hostOf(connections)?.member.id ?? null,
      stale: false,
      entries: room.entries,
      queueVersion: room.queueVersion,
      track: room.play.track,
      positionSeconds: room.play.positionSeconds,
      paused: room.play.paused,
      stateAtUnixMs: room.play.stateAtUnixMs,
    };
  }

  private joinRequest(room: JamRoomState, member: Member, hostId: string): Message {
    return {
      type: JamSignal.joinRequest,
      jamId: room.jamId,
      userId: member.id,
      hostId,
      from: { userId: member.id, name: member.name, world: member.world, displayName: member.name, handle: "", avatarUrl: null },
    };
  }

  private broadcast(connections: Connection[], message: Message, exclude: WebSocket | null = null): void {
    for (const connection of this.joined(connections)) {
      if (connection.socket !== exclude) {
        send(connection.socket, message);
      }
    }
  }

  private async onStart(socket: WebSocket, member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "host") {
      return;
    }

    const title = cappedText(message.title, MAX_JAM_TITLE) ?? "";
    if (title !== room.title) {
      room.title = title;
      await this.save(room);
    }

    member.joined = true;
    socket.serializeAttachment(member);
    send(socket, this.snapshot(JamSignal.joined, room, this.connections()));
  }

  // A guest who came from a nearby list holds the nearby code instead of the room code and always waits for the
  // host, whatever the approval setting, because nobody invited them.
  private async onJoin(socket: WebSocket, member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "guest") {
      return;
    }

    const viaNearby = member.nearby && room.discoverable && room.nearbyCode !== null && message.code === room.nearbyCode;
    if (message.code !== room.code && !viaNearby) {
      send(socket, {
        type: JamSignal.declined,
        reason: member.nearby ? DECLINE_DENIED : "code",
        code: message.code ?? null,
      });
      socket.close(CloseCode.ended, "declined");
      return;
    }

    if (member.joined) {
      send(socket, this.snapshot(JamSignal.joined, room, this.connections()));
      return;
    }

    const host = this.hostOf(this.connections());
    if ((room.approvalRequired || member.nearby) && !room.admitted.includes(member.id)) {
      member.awaiting = true;
      socket.serializeAttachment(member);
      send(socket, { type: JamSignal.joinPending, jamId: room.jamId, hostId: host?.member.id ?? null });
      if (host !== undefined) {
        send(host.socket, this.joinRequest(room, member, host.member.id));
      }

      return;
    }

    await this.admit({ socket, member }, room);
  }

  // Reads the connections only after the entrant's attachment says joined, so the snapshot and the roster list them.
  private async admit(entrant: Connection, room: JamRoomState): Promise<void> {
    entrant.member.awaiting = false;
    entrant.member.joined = true;
    entrant.socket.serializeAttachment(entrant.member);
    const connections = this.connections();
    const known = room.admitted.includes(entrant.member.id);
    room.admitted = room.admitted.filter((entry) => entry !== entrant.member.id);
    room.admitted.push(entrant.member.id);
    if (room.admitted.length > MAX_ADMITTED) {
      room.admitted.shift();
    }

    send(entrant.socket, this.snapshot(JamSignal.joined, room, connections));
    this.broadcast(connections, this.roster(room, connections), entrant.socket);
    await this.updateAlone(room, connections, !known);
  }

  private async onApprove(member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "host") {
      return;
    }

    const target = this.connections().find((connection) => connection.member.awaiting
      && connection.member.id === message.userId);
    if (target !== undefined) {
      await this.admit(target, room);
    }
  }

  private async onDeny(member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "host") {
      return;
    }

    const target = this.connections().find((connection) => connection.member.awaiting
      && connection.member.id === message.userId);
    if (target === undefined) {
      return;
    }

    send(target.socket, { type: JamSignal.declined, jamId: room.jamId, reason: DECLINE_DENIED, hostId: member.id });
    target.socket.close(CloseCode.ended, "declined");
    await this.depart(target.socket, target.member, true);
  }

  // Identities are self-declared, so removing someone only closes their connection: the same name may come back
  // with the code, and while approval is on it has to be let in again.
  private async onKick(member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "host") {
      return;
    }

    const target = this.connections().find((connection) => connection.member.role === "guest"
      && connection.member.id === message.userId);
    if (target === undefined) {
      return;
    }

    send(target.socket, { type: JamSignal.kicked, jamId: room.jamId, userId: target.member.id, hostId: member.id });
    target.socket.close(CloseCode.kicked, "kicked");
    await this.depart(target.socket, target.member, true);
  }

  private onReact(socket: WebSocket, member: Member, room: JamRoomState, message: Message): void {
    const reaction = message.reaction;
    if (!member.joined || typeof reaction !== "number" || !Number.isInteger(reaction) || reaction < 0
      || reaction >= JAM_REACTION_KINDS) {
      return;
    }

    this.broadcast(this.connections(), { type: JamSignal.reaction, jamId: room.jamId, reaction, userId: member.id },
      socket);
  }

  private async onSettings(member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "host" || !member.joined) {
      return;
    }

    let changed = false;
    const permissions = message.guestPermissions;
    if (typeof permissions === "number" && Number.isInteger(permissions)
      && (permissions & JAM_GUEST_PERMISSIONS) !== room.guestPermissions) {
      room.guestPermissions = permissions & JAM_GUEST_PERMISSIONS;
      changed = true;
    }

    if (typeof message.approvalRequired === "boolean" && message.approvalRequired !== room.approvalRequired) {
      room.approvalRequired = message.approvalRequired;
      changed = true;
    }

    if (typeof message.title === "string") {
      const title = cappedText(message.title, MAX_JAM_TITLE) ?? "";
      changed ||= title !== room.title;
      room.title = title;
    }

    if (typeof message.discoverable === "boolean" && message.discoverable !== room.discoverable) {
      room.discoverable = message.discoverable;
      room.nearbyCode = message.discoverable ? nearbyCode(room.code) : null;
      changed = true;
    }

    if (changed) {
      await this.save(room);
    }

    const connections = this.connections();
    this.broadcast(connections, this.roster(room, connections));
    if (room.approvalRequired) {
      return;
    }

    for (const waiting of connections) {
      if (waiting.member.awaiting && !waiting.member.nearby) {
        await this.admit(waiting, room);
      }
    }
  }

  private onNearby(socket: WebSocket, member: Member, message: Message): void {
    if (member.role !== "host") {
      return;
    }

    const territoryId = locationId(message.territoryId);
    const worldId = locationId(message.worldId);
    member.location = territoryId !== null && worldId !== null ? { territoryId, worldId } : null;
    socket.serializeAttachment(member);
  }

  private async onState(member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "host" || !member.joined) {
      return;
    }

    const now = Date.now();
    const position = typeof message.positionSeconds === "number" && Number.isFinite(message.positionSeconds)
      ? Math.max(0, message.positionSeconds)
      : 0;
    const next: PlayState = {
      track: parseTrack(message.track),
      positionSeconds: position,
      paused: message.paused === true,
      stateAtUnixMs: now,
    };
    const previous = room.play;
    const expected = previous.positionSeconds + (previous.paused ? 0 : (now - previous.stateAtUnixMs) / 1000);
    const meaningful = next.track?.videoId !== previous.track?.videoId || next.paused !== previous.paused
      || Math.abs(next.positionSeconds - expected) > POSITION_JUMP_SECONDS;
    room.play = next;
    if (meaningful) {
      await this.save(room);
    }

    this.broadcast(this.connections(), {
      type: JamSignal.state,
      jamId: room.jamId,
      hostId: member.id,
      stale: false,
      track: next.track,
      positionSeconds: next.positionSeconds,
      paused: next.paused,
      stateAtUnixMs: now,
    });
  }

  private onControl(member: Member, room: JamRoomState, message: Message): void {
    if (member.role !== "guest" || !member.joined || typeof message.action !== "string"
      || JAM_CONTROL_ACTIONS[message.action] !== true || !guestMay(member, room, JAM_PERMISSION_CONTROL)) {
      return;
    }

    const host = this.hostOf(this.connections());
    if (host === undefined) {
      return;
    }

    const position = message.positionSeconds;
    send(host.socket, {
      type: JamSignal.controlRequest,
      userId: member.id,
      action: message.action,
      positionSeconds: typeof position === "number" && Number.isFinite(position) ? position : null,
    });
  }

  private onTransfer(socket: WebSocket, member: Member, room: JamRoomState, message: Message): void {
    if (member.role !== "host" || typeof message.userId !== "string") {
      return;
    }

    const connections = this.connections();
    const target = connections.find((connection) => connection.member.id === message.userId
      && connection.member.role === "guest" && connection.member.joined);
    if (target === undefined) {
      return;
    }

    member.role = "guest";
    member.bucket = freshBucket(VIEWER_RATE, Date.now());
    member.location = null;
    target.member.role = "host";
    target.member.bucket = freshBucket(HOST_RATE, Date.now());
    target.member.location = null;
    socket.serializeAttachment(member);
    target.socket.serializeAttachment(target.member);
    const updated = this.connections();
    this.broadcast(updated, this.snapshot(JamSignal.hostChanged, room, updated));
    for (const waiting of updated) {
      if (waiting.member.awaiting) {
        send(target.socket, this.joinRequest(room, waiting.member, target.member.id));
      }
    }
  }

  private async onQueueAdd(member: Member, room: JamRoomState, message: Message): Promise<void> {
    const track = parseTrack(message.track);
    if (!member.joined || track === null || room.entries.length >= MAX_JAM_QUEUE
      || !guestMay(member, room, JAM_PERMISSION_ADD)) {
      return;
    }

    const entry: JamEntry = { entryId: room.nextEntryId, track, addedByUserId: member.id, addedByName: member.name };
    room.nextEntryId++;
    if (message.mode === "next") {
      room.entries.unshift(entry);
    } else {
      room.entries.push(entry);
    }

    room.queueVersion++;
    await this.save(room);
    this.broadcast(this.connections(), this.queue(room));
  }

  private async onQueueAdvance(member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "host" || !member.joined) {
      return;
    }

    const index = room.entries.findIndex((entry) => entry.entryId === message.entryId);
    if (index >= 0) {
      room.entries.splice(index, 1);
      room.queueVersion++;
      await this.save(room);
    }

    this.broadcast(this.connections(), this.queue(room));
  }

  // Mirrors the client rules: the host removes anything, a guest only what they added.
  private async onQueueRemove(member: Member, room: JamRoomState, message: Message): Promise<void> {
    const index = member.joined ? room.entries.findIndex((entry) => entry.entryId === message.entryId) : -1;
    if (index < 0 || (member.role !== "host" && room.entries[index].addedByUserId !== member.id)) {
      return;
    }

    room.entries.splice(index, 1);
    room.queueVersion++;
    await this.save(room);
    this.broadcast(this.connections(), this.queue(room));
  }

  // Reordering is a playback decision in the client, so a guest needs the control permission for it.
  private async onQueueMove(member: Member, room: JamRoomState, message: Message): Promise<void> {
    const index = member.joined ? room.entries.findIndex((entry) => entry.entryId === message.entryId) : -1;
    const target = message.toIndex;
    if (index < 0 || typeof target !== "number" || !Number.isInteger(target)
      || !guestMay(member, room, JAM_PERMISSION_CONTROL)) {
      return;
    }

    const destination = Math.min(Math.max(0, target), room.entries.length - 1);
    if (destination !== index) {
      const [entry] = room.entries.splice(index, 1);
      room.entries.splice(destination, 0, entry);
      room.queueVersion++;
      await this.save(room);
    }

    this.broadcast(this.connections(), this.queue(room));
  }

  // dirty: the room already changed in memory, so it is saved even when the company did not change.
  private async updateAlone(room: JamRoomState, connections: Connection[], dirty = false): Promise<void> {
    const hasCompany = connections.some((connection) => connection.member.role === "guest" && connection.member.joined);
    const aloneSince = hasCompany ? null : room.aloneSince ?? Date.now();
    if (aloneSince === room.aloneSince) {
      if (dirty) {
        await this.save(room);
      }

      return;
    }

    room.aloneSince = aloneSince;
    await this.save(room);
    await this.ctx.storage.setAlarm(roomDeadline(room));
  }

  // forget: the guest left on purpose or was removed, so a later connection under that name asks again.
  private async depart(socket: WebSocket, member: Member, forget = false): Promise<void> {
    if (member.gone) {
      return;
    }

    member.gone = true;
    socket.serializeAttachment(member);
    if (member.role === "host") {
      await this.endRoom(socket, "host_left");
      return;
    }

    const room = await this.room();
    if (room === null) {
      return;
    }

    const connections = this.connections(socket);
    if (member.awaiting) {
      const host = this.hostOf(connections);
      if (host !== undefined) {
        send(host.socket, { type: JamSignal.joinCancelled, jamId: room.jamId, userId: member.id });
      }

      return;
    }

    if (!member.joined) {
      return;
    }

    const forgotten = forget && room.admitted.includes(member.id);
    if (forgotten) {
      room.admitted = room.admitted.filter((entry) => entry !== member.id);
    }

    this.broadcast(connections, this.roster(room, connections));
    await this.updateAlone(room, connections, forgotten);
  }

  private async endRoom(exclude: WebSocket | null, reason: string): Promise<void> {
    const room = await this.room();
    for (const connection of this.connections(exclude)) {
      send(connection.socket, { type: JamSignal.ended, jamId: room?.jamId ?? null, reason });
      connection.member.gone = true;
      connection.socket.serializeAttachment(connection.member);
      connection.socket.close(CloseCode.ended, "ended");
    }

    if (room === null) {
      return;
    }

    this.cached = null;
    await this.ctx.storage.deleteAlarm();
    await this.ctx.storage.deleteAll();
  }
}
