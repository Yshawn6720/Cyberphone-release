import { DurableObject } from "cloudflare:workers";
import { GAME_ENGINES } from "./games";
import type { FleetShip, GameAction, GameEngine, Random, RoomView } from "./games/engine";
import type { Env } from "./index";
import {
  ALONE_TIMEOUT_MS,
  CloseCode,
  GAME_RATE,
  GameSignal,
  MAX_MEMBERS,
  MAX_MESSAGE_BYTES,
  ROOM_LIFETIME_MS,
} from "./protocol";
import { JOIN_HEADER, type JoinTicket, type SlotOccupant, VACANT, isProbe, occupantResponse } from "./room";
import { type Bucket, type Message, OPEN, charge, exceedsUtf8Bytes, freshBucket, parseMessage, send } from "./wire";

export const PHASE_LOBBY = 0;
export const PHASE_PLAYING = 1;
export const PHASE_FINISHED = 2;
const EPOCH = 1;
const MIN_SEATS = 2;
const MAX_ACTION_NAME = 16;
const MAX_CLIENT_ACTION_ID = 64;
const MAX_FLEET = 16;
const START_ACTION = "start";

interface Seat {
  id: string;
  name: string;
  wins: number;
  left: boolean;
}

export interface GameRoomState {
  code: string;
  address: string;
  kind: string;
  createdAt: number;
  aloneSince: number | null;
  seq: number;
  round: number;
  phase: number;
  actionCount: number;
  seats: Seat[];
  roundSeats: number;
  game: unknown;
}

interface Member {
  id: string;
  name: string;
  role: "host" | "guest";
  joined: boolean;
  gone: boolean;
  bucket: Bucket;
  room: GameRoomState | null;
}

interface Connection {
  socket: WebSocket;
  member: Member;
}

interface ParsedAction {
  action: GameAction;
  actionCount: number;
  clientActionId: string;
}

function memberOf(socket: WebSocket): Member {
  return socket.deserializeAttachment() as Member;
}

function integer(value: unknown): number {
  return typeof value === "number" && Number.isInteger(value) ? value : -1;
}

function parseFleet(value: unknown): FleetShip[] | null {
  if (!Array.isArray(value) || value.length > MAX_FLEET) {
    return null;
  }

  const fleet: FleetShip[] = [];
  for (const entry of value) {
    if (typeof entry !== "object" || entry === null) {
      return null;
    }

    const fields = entry as Message;
    fleet.push({ ship: integer(fields.ship), cell: integer(fields.cell), across: fields.across === true });
  }

  return fleet;
}

function parseAction(game: unknown): ParsedAction | null {
  if (typeof game !== "object" || game === null) {
    return null;
  }

  const act = (game as Message).act;
  if (typeof act !== "object" || act === null) {
    return null;
  }

  const fields = act as Message;
  if (typeof fields.action !== "string" || fields.action.length > MAX_ACTION_NAME) {
    return null;
  }

  const clientActionId = typeof fields.clientActionId === "string"
    ? fields.clientActionId.slice(0, MAX_CLIENT_ACTION_ID)
    : "";
  return {
    action: {
      action: fields.action,
      card: integer(fields.card),
      color: integer(fields.color),
      from: integer(fields.from),
      to: integer(fields.to),
      column: integer(fields.column),
      cell: integer(fields.cell),
      target: integer(fields.target),
      fleet: parseFleet(fields.fleet),
    },
    actionCount: integer(fields.actionCount),
    clientActionId,
  };
}

function roomDeadline(room: GameRoomState): number {
  const lifetimeEnd = room.createdAt + ROOM_LIFETIME_MS;
  return room.aloneSince === null ? lifetimeEnd : Math.min(lifetimeEnd, room.aloneSince + ALONE_TIMEOUT_MS);
}

const random: Random = () => crypto.getRandomValues(new Uint32Array(1))[0] / 0x100000000;

// One game table per room slot. The whole room (roster, round, the engine's board and every hidden hand) lives in
// the host's WebSocket attachment, because the room ends when the host leaves anyway; moves therefore cost no
// storage writes at all, and the only storage use is the closing alarm. The server is the referee: clients send
// intents, the engine checks them, and everyone receives the resulting public state plus their own private state.
export class GameRoom extends DurableObject<Env> {
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

      const room: GameRoomState = {
        code: ticket.code,
        address: ticket.address,
        kind: "",
        createdAt: now,
        aloneSince: now,
        seq: 0,
        round: 0,
        phase: PHASE_LOBBY,
        actionCount: 0,
        seats: [],
        roundSeats: 0,
        game: null,
      };
      const response = this.accept(this.newMember(ticket, "host", now, room));
      await this.ctx.storage.setAlarm(roomDeadline(room));
      return response;
    }

    const room = host?.member.room ?? null;
    if (host === undefined || room === null || room.code !== ticket.code || room.kind.length === 0) {
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
      return new Response("room full", { status: 409 });
    }

    return this.accept(this.newMember(ticket, "guest", now, null));
  }

  async webSocketMessage(socket: WebSocket, raw: string | ArrayBuffer): Promise<void> {
    const member = memberOf(socket);
    if (member.gone) {
      return;
    }

    if (typeof raw !== "string") {
      socket.close(CloseCode.unsupported, "text frames only");
      await this.depart(socket, member, false);
      return;
    }

    if (exceedsUtf8Bytes(raw, MAX_MESSAGE_BYTES)) {
      socket.close(CloseCode.tooBig, "message too big");
      await this.depart(socket, member, false);
      return;
    }

    const verdict = charge(member.bucket, GAME_RATE, Date.now());
    socket.serializeAttachment(member);
    if (verdict === "close") {
      socket.close(CloseCode.rate, "rate limited");
      await this.depart(socket, member, false);
      return;
    }

    const message = verdict === "drop" ? null : parseMessage(raw);
    if (message === null) {
      return;
    }

    switch (message.type) {
      case GameSignal.create:
        this.onCreate(socket, member, message);
        return;
      case GameSignal.join:
        await this.onJoin(socket, member, message);
        return;
      case GameSignal.attach:
        this.onAttach(socket, member, GameSignal.attached);
        return;
      case GameSignal.resync:
        this.onAttach(socket, member, GameSignal.snapshot);
        return;
      case GameSignal.act:
        this.onAct(socket, member, message);
        return;
      case GameSignal.claim:
        this.onClaim(member);
        return;
      case GameSignal.leave:
      case GameSignal.close:
        socket.close(CloseCode.ended, "left");
        await this.depart(socket, member, true);
        return;
      case GameSignal.kick:
        await this.onKick(member, message);
        return;
    }
  }

  async webSocketClose(socket: WebSocket): Promise<void> {
    await this.depart(socket, memberOf(socket), false);
  }

  async webSocketError(socket: WebSocket): Promise<void> {
    await this.depart(socket, memberOf(socket), false);
  }

  async alarm(): Promise<void> {
    const host = this.host(this.connections());
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
    const host = this.host(this.connections());
    if (host === undefined) {
      return VACANT;
    }

    return { occupied: true, hostId: host.member.id, address: host.member.room?.address ?? null };
  }

  private newMember(ticket: JoinTicket, role: "host" | "guest", now: number, room: GameRoomState | null): Member {
    return {
      id: ticket.id,
      name: ticket.name,
      role,
      joined: false,
      gone: false,
      bucket: freshBucket(GAME_RATE, now),
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

  private host(connections: Connection[]): Connection | undefined {
    return connections.find((connection) => connection.member.role === "host");
  }

  private engineOf(room: GameRoomState): GameEngine<unknown> | undefined {
    return GAME_ENGINES.get(room.kind);
  }

  private view(room: GameRoomState, connections: Connection[]): RoomView {
    const present = new Set<string>();
    for (const connection of connections) {
      if (connection.member.joined) {
        present.add(connection.member.id);
      }
    }

    return {
      round: room.round,
      hostId: this.host(connections)?.member.id ?? "",
      actionCount: room.actionCount,
      seats: room.seats.map((seat) => ({
        userId: seat.id,
        displayName: seat.name,
        away: seat.left || !present.has(seat.id),
        wins: seat.wins,
      })),
    };
  }

  private table(room: GameRoomState, engine: GameEngine<unknown>, connections: Connection[]): Message {
    const playing = room.phase === PHASE_PLAYING && room.game !== null;
    return {
      state: 0,
      phase: room.phase,
      phaseEndsAtUnixMs: playing ? engine.deadline(room.game) : 0,
      roundIndex: room.round,
      gameState: JSON.stringify(engine.view(room.game, this.view(room, connections))),
      occupancy: connections.filter((connection) => connection.member.joined).length,
    };
  }

  private envelope(room: GameRoomState, now: number, fields: Message): Message {
    return { roomId: room.code, epoch: EPOCH, seq: room.seq, pairSeq: room.seq, serverNowUnixMs: now, ...fields };
  }

  private sendSnapshot(socket: WebSocket, member: Member, room: GameRoomState, type: string,
    connections: Connection[]): void {
    const engine = this.engineOf(room);
    if (engine === undefined) {
      return;
    }

    const now = Date.now();
    const snapshot = {
      roomId: room.code,
      gameKind: room.kind,
      ...this.table(room, engine, connections),
      attached: true,
      epoch: EPOCH,
      seq: room.seq,
      serverNowUnixMs: now,
    };
    send(socket, { type, code: room.code, game: this.envelope(room, now, { snapshot }) });
    this.sendSecret(socket, member, room, engine, connections, now);
  }

  private sendSecret(socket: WebSocket, member: Member, room: GameRoomState, engine: GameEngine<unknown>,
    connections: Connection[], now: number): void {
    const seat = room.seats.findIndex((entry) => entry.id === member.id);
    if (room.game === null || seat < 0 || seat >= room.roundSeats) {
      return;
    }

    const secret = engine.secret(room.game, seat, this.view(room, connections));
    if (secret !== null) {
      send(socket, {
        type: GameSignal.private,
        game: this.envelope(room, now, {
          private: { eventKind: secret.eventKind, payload: JSON.stringify(secret.payload) },
        }),
      });
    }
  }

  private publish(host: Connection, room: GameRoomState, connections: Connection[],
    exclude: WebSocket | null = null): void {
    const engine = this.engineOf(room);
    if (engine === undefined) {
      return;
    }

    room.seq++;
    host.socket.serializeAttachment(host.member);
    const now = Date.now();
    const event = { type: GameSignal.event, game: this.envelope(room, now, { event: this.table(room, engine, connections) }) };
    for (const connection of connections) {
      if (connection.member.joined && connection.socket !== exclude) {
        send(connection.socket, event);
        this.sendSecret(connection.socket, connection.member, room, engine, connections, now);
      }
    }
  }

  private onCreate(socket: WebSocket, member: Member, message: Message): void {
    const room = member.room;
    const game = message.game;
    const kind = typeof game === "object" && game !== null ? (game as Message).gameKind : undefined;
    if (member.role !== "host" || room === null || room.kind.length > 0) {
      return;
    }

    if (typeof kind !== "string" || !GAME_ENGINES.has(kind)) {
      send(socket, { type: GameSignal.declined, reason: "unavailable", game: { roomId: "" } });
      socket.close(CloseCode.ended, "declined");
      return;
    }

    room.kind = kind;
    room.seats = [{ id: member.id, name: member.name, wins: 0, left: false }];
    member.joined = true;
    socket.serializeAttachment(member);
    this.sendSnapshot(socket, member, room, GameSignal.attached, this.connections());
  }

  private decline(socket: WebSocket, reason: string, code: unknown): void {
    send(socket, { type: GameSignal.declined, reason, code: typeof code === "string" ? code : null, game: { roomId: "" } });
    socket.close(CloseCode.ended, "declined");
  }

  private async onJoin(socket: WebSocket, member: Member, message: Message): Promise<void> {
    const connections = this.connections();
    const host = this.host(connections);
    const room = host?.member.room ?? null;
    const engine = room === null ? undefined : this.engineOf(room);
    if (member.role !== "guest" || host === undefined || room === null || engine === undefined) {
      return;
    }

    if (message.code !== room.code) {
      this.decline(socket, "unavailable", message.code);
      return;
    }

    const seated = room.seats.find((seat) => seat.id === member.id);
    const rejoining = seated !== undefined && !seated.left;
    if (!rejoining) {
      if (room.phase === PHASE_PLAYING) {
        this.decline(socket, "in_progress", message.code);
        return;
      }

      if (room.seats.filter((seat) => !seat.left).length >= engine.maxSeats) {
        this.decline(socket, "full", message.code);
        return;
      }

      if (seated === undefined) {
        room.seats.push({ id: member.id, name: member.name, wins: 0, left: false });
      } else {
        seated.left = false;
      }
    }

    const wasJoined = member.joined;
    member.joined = true;
    socket.serializeAttachment(member);
    const updated = this.connections();
    if (!wasJoined) {
      await this.updateAlone(room, updated);
      this.publish(host, room, updated, socket);
    }

    this.sendSnapshot(socket, member, room, GameSignal.attached, updated);
  }

  private onAttach(socket: WebSocket, member: Member, type: string): void {
    const connections = this.connections();
    const room = this.host(connections)?.member.room ?? null;
    if (member.joined && room !== null) {
      this.sendSnapshot(socket, member, room, type, connections);
    }
  }

  private finishIfOver(room: GameRoomState, engine: GameEngine<unknown>): void {
    if (room.game === null || !engine.finished(room.game)) {
      return;
    }

    room.phase = PHASE_FINISHED;
    const winner = engine.winner(room.game);
    if (winner >= 0 && winner < room.seats.length) {
      room.seats[winner].wins++;
    }
  }

  private start(room: GameRoomState, engine: GameEngine<unknown>, option: number, connections: Connection[]): string {
    if (room.phase === PHASE_PLAYING) {
      return "invalid";
    }

    const normalized = engine.startOption(option);
    const present = new Set(connections.filter((connection) => connection.member.joined)
      .map((connection) => connection.member.id));
    const seats = room.seats.filter((seat) => !seat.left && present.has(seat.id));
    if (normalized === null || seats.length < MIN_SEATS || seats.length > engine.maxSeats) {
      return "invalid";
    }

    room.seats = seats;
    room.round++;
    room.roundSeats = seats.length;
    room.game = engine.start(seats.length, normalized, random, Date.now());
    room.phase = PHASE_PLAYING;
    return "";
  }

  private onAct(socket: WebSocket, member: Member, message: Message): void {
    const parsed = parseAction(message.game);
    const connections = this.connections();
    const host = this.host(connections);
    const room = host?.member.room ?? null;
    const engine = room === null ? undefined : this.engineOf(room);
    if (parsed === null || !member.joined || host === undefined || room === null || engine === undefined) {
      return;
    }

    const seat = room.seats.findIndex((entry) => entry.id === member.id);
    let reason: string;
    if (parsed.actionCount !== room.actionCount) {
      reason = "stale_action";
    } else if (parsed.action.action === START_ACTION) {
      reason = member.role === "host" ? this.start(room, engine, parsed.action.card, connections) : "invalid";
    } else if (room.phase !== PHASE_PLAYING || room.game === null || seat < 0 || seat >= room.roundSeats
      || room.seats[seat].left) {
      reason = "invalid";
    } else {
      reason = engine.act(room.game, seat, parsed.action, random, Date.now());
    }

    if (reason.length === 0) {
      room.actionCount++;
      this.finishIfOver(room, engine);
      this.publish(host, room, connections);
    }

    send(socket, { type: GameSignal.acted, reason, game: { roomId: room.code, clientActionId: parsed.clientActionId } });
  }

  private onClaim(member: Member): void {
    const connections = this.connections();
    const host = this.host(connections);
    const room = host?.member.room ?? null;
    const engine = room === null ? undefined : this.engineOf(room);
    if (!member.joined || host === undefined || room === null || engine === undefined
      || room.phase !== PHASE_PLAYING || room.game === null
      || !room.seats.some((seat, index) => seat.id === member.id && index < room.roundSeats)) {
      return;
    }

    if (engine.claim(room.game, random, Date.now())) {
      room.actionCount++;
      this.finishIfOver(room, engine);
      this.publish(host, room, connections);
    }
  }

  private async onKick(member: Member, message: Message): Promise<void> {
    const connections = this.connections();
    const host = this.host(connections);
    const room = host?.member.room ?? null;
    if (member.role !== "host" || host === undefined || room === null || typeof message.userId !== "string"
      || message.userId === member.id) {
      return;
    }

    const remaining: Connection[] = [];
    for (const connection of connections) {
      if (connection.member.id !== message.userId) {
        remaining.push(connection);
        continue;
      }

      send(connection.socket, { type: GameSignal.ended, reason: "kicked", game: { roomId: room.code } });
      connection.member.gone = true;
      connection.socket.serializeAttachment(connection.member);
      connection.socket.close(CloseCode.kicked, "kicked");
    }

    if (this.release(room, message.userId)) {
      await this.updateAlone(room, remaining);
      this.publish(host, room, remaining);
    }
  }

  // A seat that walks out mid-round stays listed (away) until the round is over so seat numbers, and with them the
  // winner shown on the finished screen, never shift under a live table; the engine decides whether the round goes on.
  private release(room: GameRoomState, userId: string): boolean {
    const seat = room.seats.findIndex((entry) => entry.id === userId);
    const engine = this.engineOf(room);
    if (seat < 0 || room.seats[seat].left || engine === undefined) {
      return false;
    }

    if (room.phase === PHASE_LOBBY) {
      room.seats.splice(seat, 1);
      return true;
    }

    room.seats[seat].left = true;
    if (room.phase === PHASE_PLAYING && room.game !== null && seat < room.roundSeats) {
      engine.forfeit(room.game, seat, Date.now());
      room.actionCount++;
      this.finishIfOver(room, engine);
    }

    return true;
  }

  private async updateAlone(room: GameRoomState, connections: Connection[]): Promise<void> {
    const hasCompany = connections.some((connection) => connection.member.role === "guest" && connection.member.joined);
    const aloneSince = hasCompany ? null : room.aloneSince ?? Date.now();
    if (aloneSince === room.aloneSince) {
      return;
    }

    room.aloneSince = aloneSince;
    await this.ctx.storage.setAlarm(roomDeadline(room));
  }

  private async depart(socket: WebSocket, member: Member, deliberate: boolean): Promise<void> {
    if (member.gone) {
      return;
    }

    member.gone = true;
    socket.serializeAttachment(member);
    if (member.role === "host") {
      this.endRoom(socket, "ended");
      return;
    }

    const connections = this.connections(socket);
    const host = this.host(connections);
    const room = host?.member.room ?? null;
    if (host === undefined || room === null || !member.joined) {
      return;
    }

    if (deliberate || room.phase === PHASE_LOBBY) {
      this.release(room, member.id);
    }

    await this.updateAlone(room, connections);
    this.publish(host, room, connections);
  }

  private endRoom(exclude: WebSocket | null, reason: string): void {
    const connections = this.connections(exclude);
    const room = this.host(connections)?.member.room
      ?? (exclude === null ? null : memberOf(exclude).room);
    for (const connection of connections) {
      send(connection.socket, { type: GameSignal.ended, reason, game: { roomId: room?.code ?? "" } });
      connection.member.gone = true;
      connection.socket.serializeAttachment(connection.member);
      connection.socket.close(CloseCode.ended, "ended");
    }

    this.ctx.waitUntil(this.ctx.storage.deleteAlarm());
  }
}
