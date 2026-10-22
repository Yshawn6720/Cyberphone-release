import { DurableObject } from "cloudflare:workers";
import type { Env } from "./index";
import {
  ALONE_TIMEOUT_MS,
  CloseCode,
  HOST_RATE,
  JAM_CONTROL_ACTIONS,
  JAM_GUEST_PERMISSIONS,
  JamSignal,
  MAX_JAM_QUEUE,
  MAX_JAM_TITLE,
  MAX_MEMBERS,
  MAX_MESSAGE_BYTES,
  MAX_THUMBNAIL_URL,
  MAX_TRACK_TEXT,
  ROOM_LIFETIME_MS,
  THUMBNAIL_HOSTS,
  VIEWER_RATE,
} from "./protocol";
import { JOIN_HEADER, type JoinTicket, type SlotOccupant, VACANT, isProbe, occupantResponse } from "./room";
import { type Bucket, type Message, OPEN, charge, exceedsUtf8Bytes, freshBucket, parseMessage, send } from "./wire";

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
}

interface Member {
  id: string;
  name: string;
  role: "host" | "guest";
  joined: boolean;
  gone: boolean;
  sequence: number;
  bucket: Bucket;
}

interface Connection {
  socket: WebSocket;
  member: Member;
}

function memberOf(socket: WebSocket): Member {
  return socket.deserializeAttachment() as Member;
}

function cappedText(value: unknown, limit: number): string | null {
  if (typeof value !== "string") {
    return null;
  }

  const text = value.trim();
  return text.length === 0 ? null : text.slice(0, limit);
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

// One Jam per room slot. Membership and rate buckets live in WebSocket attachments; the queue and the last
// playback state outgrow an attachment, so they sit under one storage key that is written only when they change
// (a heartbeat that merely confirms the expected position stays in memory).
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
      };
      await this.save(room);
      await this.ctx.storage.setAlarm(roomDeadline(room));
      return this.accept(this.newMember(ticket, "host", now));
    }

    const room = await this.room();
    if (host === undefined || room === null || room.code !== ticket.code) {
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

    const verdict = charge(member.bucket, member.role === "host" ? HOST_RATE : VIEWER_RATE, Date.now());
    socket.serializeAttachment(member);
    if (verdict === "close") {
      socket.close(CloseCode.rate, "rate limited");
      await this.depart(socket, member);
      return;
    }

    const message = verdict === "drop" ? null : parseMessage(raw);
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
        await this.depart(socket, member);
        return;
      case JamSignal.state:
        await this.onState(member, room, message);
        return;
      case JamSignal.control:
        this.onControl(member, message);
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

  private async room(): Promise<JamRoomState | null> {
    if (this.cached === undefined) {
      this.cached = await this.ctx.storage.get<JamRoomState>(ROOM_KEY) ?? null;
    }

    return this.cached;
  }

  private async save(room: JamRoomState): Promise<void> {
    this.cached = room;
    await this.ctx.storage.put(ROOM_KEY, room);
  }

  private async occupant(): Promise<SlotOccupant> {
    const host = this.connections().find((connection) => connection.member.role === "host");
    if (host === undefined) {
      return VACANT;
    }

    const room = await this.room();
    return { occupied: true, hostId: host.member.id, address: room?.address ?? null };
  }

  private newMember(ticket: JoinTicket, role: "host" | "guest", now: number): Member {
    return {
      id: ticket.id,
      name: ticket.name,
      role,
      joined: false,
      gone: false,
      sequence: now,
      bucket: freshBucket(role === "host" ? HOST_RATE : VIEWER_RATE, now),
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

  private members(connections: Connection[]): Message[] {
    return this.joined(connections)
      .sort((left, right) => (left.member.role === "host" ? -1 : right.member.role === "host" ? 1 : 0)
        || left.member.sequence - right.member.sequence)
      .map((connection) => ({
        userId: connection.member.id,
        displayName: connection.member.name,
        handle: "",
        avatarUrl: null,
        isHost: connection.member.role === "host",
        permissions: JAM_GUEST_PERMISSIONS,
      }));
  }

  private hostId(connections: Connection[]): string | null {
    return connections.find((connection) => connection.member.role === "host")?.member.id ?? null;
  }

  private roster(room: JamRoomState, connections: Connection[]): Message {
    return {
      type: JamSignal.roster,
      jamId: room.jamId,
      title: room.title,
      guestPermissions: JAM_GUEST_PERMISSIONS,
      approvalRequired: false,
      discoverable: false,
      jamMembers: this.members(connections),
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
      hostId: this.hostId(connections),
      stale: false,
      entries: room.entries,
      queueVersion: room.queueVersion,
      track: room.play.track,
      positionSeconds: room.play.positionSeconds,
      paused: room.play.paused,
      stateAtUnixMs: room.play.stateAtUnixMs,
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

  private async onJoin(socket: WebSocket, member: Member, room: JamRoomState, message: Message): Promise<void> {
    if (member.role !== "guest") {
      return;
    }

    if (message.code !== room.code) {
      send(socket, { type: JamSignal.declined, reason: "code", code: message.code ?? null });
      socket.close(CloseCode.ended, "declined");
      return;
    }

    const rejoin = member.joined;
    member.joined = true;
    socket.serializeAttachment(member);
    const connections = this.connections();
    send(socket, this.snapshot(JamSignal.joined, room, connections));
    if (!rejoin) {
      this.broadcast(connections, this.roster(room, connections), socket);
      await this.updateAlone(room, connections);
    }
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

  private onControl(member: Member, message: Message): void {
    if (member.role !== "guest" || !member.joined || typeof message.action !== "string"
      || JAM_CONTROL_ACTIONS[message.action] !== true) {
      return;
    }

    const host = this.connections().find((connection) => connection.member.role === "host");
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
    target.member.role = "host";
    target.member.bucket = freshBucket(HOST_RATE, Date.now());
    socket.serializeAttachment(member);
    target.socket.serializeAttachment(target.member);
    const updated = this.connections();
    this.broadcast(updated, this.snapshot(JamSignal.hostChanged, room, updated));
  }

  private async onQueueAdd(member: Member, room: JamRoomState, message: Message): Promise<void> {
    const track = parseTrack(message.track);
    if (!member.joined || track === null || room.entries.length >= MAX_JAM_QUEUE) {
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

  private async onQueueMove(member: Member, room: JamRoomState, message: Message): Promise<void> {
    const index = member.joined ? room.entries.findIndex((entry) => entry.entryId === message.entryId) : -1;
    const target = message.toIndex;
    if (index < 0 || typeof target !== "number" || !Number.isInteger(target)) {
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

  private async updateAlone(room: JamRoomState, connections: Connection[]): Promise<void> {
    const hasCompany = connections.some((connection) => connection.member.role === "guest" && connection.member.joined);
    const aloneSince = hasCompany ? null : room.aloneSince ?? Date.now();
    if (aloneSince === room.aloneSince) {
      return;
    }

    room.aloneSince = aloneSince;
    await this.save(room);
    await this.ctx.storage.setAlarm(roomDeadline(room));
  }

  private async depart(socket: WebSocket, member: Member): Promise<void> {
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
    if (room === null || !member.joined) {
      return;
    }

    const connections = this.connections(socket);
    this.broadcast(connections, this.roster(room, connections));
    await this.updateAlone(room, connections);
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
