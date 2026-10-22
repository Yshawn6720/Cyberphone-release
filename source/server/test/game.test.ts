import { abortAllDurableObjects, reset, runInDurableObject } from "cloudflare:test";
import { afterEach, describe, expect, it } from "vitest";
import type { GameRoom } from "../src/game";
import { CODE_ALPHABET, CloseCode, GAME_RATE } from "../src/protocol";
import { type Message, type Peer, connect, env, identity, realtimeRequest, self } from "./support";

const CONNECT_FOUR = "games.connectfour";
const STORAGE_WRITES = ["put", "delete", "deleteAll", "setAlarm", "deleteAlarm"] as const;

interface Table {
  host: Peer;
  guest: Peer;
  code: string;
}

function gameOf(message: Message): Message {
  return message.game as Message;
}

function boardOf(message: Message): Message {
  const game = gameOf(message);
  const carrier = (game.snapshot ?? game.event) as Message;
  return JSON.parse(carrier.gameState as string) as Message;
}

function act(peer: Peer, code: string, actionCount: number, fields: Message): void {
  peer.send({ type: "game.act", game: { roomId: code, act: { actionCount, clientActionId: "id", ...fields } } });
}

async function openRoom(kind = CONNECT_FOUR, name = "Host"): Promise<{ host: Peer; code: string; attached: Message }> {
  const host = await connect(`kind=game&create=1&${identity(name)}`);
  host.send({ type: "game.create", game: { gameKind: kind } });
  const attached = await host.next("game.attached");
  return { host, code: attached.code as string, attached };
}

async function joinRoom(host: Peer, code: string, name: string): Promise<{ guest: Peer; attached: Message }> {
  const guest = await connect(`kind=game&code=${code}&${identity(name)}`);
  guest.send({ type: "game.join", code });
  const attached = await guest.next("game.attached");
  await host.next("game.event");
  return { guest, attached };
}

async function startedTable(): Promise<Table & { board: Message }> {
  const { host, code } = await openRoom();
  const { guest } = await joinRoom(host, code, "Guest");
  act(host, code, 0, { action: "start", card: 0 });
  const event = await host.next("game.event");
  await host.next("game.acted");
  await guest.next("game.event");
  return { host, guest, code, board: boardOf(event) };
}

function roomStub(code: string): DurableObjectStub<GameRoom> {
  const slot = CODE_ALPHABET.indexOf(code[0]);
  return env.GAME_ROOMS.get(env.GAME_ROOMS.idFromName(`slot:${slot}`)) as DurableObjectStub<GameRoom>;
}

async function countStorageWrites(code: string): Promise<string[]> {
  const writes: string[] = [];
  await runInDurableObject(roomStub(code), (_instance, state) => {
    const storage = state.storage as unknown as Record<string, (...args: unknown[]) => unknown>;
    for (const name of STORAGE_WRITES) {
      const original = storage[name].bind(state.storage);
      storage[name] = (...args: unknown[]) => {
        writes.push(name);
        return original(...args);
      };
    }
  });
  return writes;
}

afterEach(async () => {
  await abortAllDurableObjects();
  await reset();
});

describe("game room", () => {
  it("answers the host's create with a lobby snapshot naming the code and seating the host", async () => {
    const { code, attached } = await openRoom();
    const snapshot = gameOf(attached).snapshot as Message;
    const board = boardOf(attached);

    expect(code).toMatch(/^[A-Z2-9]{6}$/);
    expect(gameOf(attached)).toMatchObject({ roomId: code, epoch: 1 });
    expect(snapshot).toMatchObject({ roomId: code, gameKind: CONNECT_FOUR, phase: 0, attached: true, occupancy: 1 });
    expect(board).toMatchObject({ hostUserId: "Host@伊弗利特", winnerSeat: -1 });
    expect(board.players).toEqual([
      { userId: "Host@伊弗利特", displayName: "Host", seat: 0, away: false, wins: 0 },
    ]);
  });

  it("seats a guest who joins with the code and tells the host", async () => {
    const { host, code } = await openRoom();
    const guest = await connect(`kind=game&code=${code}&${identity("Guest")}`);
    guest.send({ type: "game.join", code });
    const attached = await guest.next("game.attached");
    const event = await host.next("game.event");

    expect(boardOf(attached).players).toHaveLength(2);
    expect((gameOf(event).event as Message).occupancy).toBe(2);
    expect(boardOf(event).players).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特", seat: 0 }),
      expect.objectContaining({ userId: "Guest@伊弗利特", seat: 1, away: false }),
    ]);
  });

  it("declines a wrong code, a full table and an unknown game kind", async () => {
    const { host, code } = await openRoom();
    await joinRoom(host, code, "Second");

    const third = await connect(`kind=game&code=${code}&${identity("Third")}`);
    third.send({ type: "game.join", code });
    const full = await third.next("game.declined");
    const stranger = await connect(`kind=game&code=${code}&${identity("Fourth")}`);
    stranger.send({ type: "game.join", code: "AAAAAA" });
    const wrong = await stranger.next("game.declined");
    const other = await connect(`kind=game&create=1&${identity("Other")}`);
    other.send({ type: "game.create", game: { gameKind: "games.pool" } });
    const unsupported = await other.next("game.declined");

    expect(full.reason).toBe("full");
    expect(wrong.reason).toBe("unavailable");
    expect(unsupported.reason).toBe("unavailable");
    expect(await third.closed()).toBe(CloseCode.ended);
  });

  it("starts on the host's word, forwards each move to both players and answers the mover", async () => {
    const { host, guest, code, board } = await startedTable();
    const mover = board.turnSeat === 0 ? host : guest;
    const waiter = board.turnSeat === 0 ? guest : host;

    act(mover, code, 1, { action: "drop", column: 3 });
    const acted = await mover.next("game.acted");
    const seen = await waiter.next("game.event");
    const echoed = await mover.next("game.event");

    expect(board).toMatchObject({ roundIndex: 1, actionCount: 1, lastColumn: -1 });
    expect(acted).toMatchObject({ reason: "", game: { roomId: code, clientActionId: "id" } });
    expect(gameOf(seen).seq).toBe(gameOf(echoed).seq);
    expect(boardOf(seen)).toMatchObject({ lastColumn: 3, lastRow: 5, lastSeat: board.turnSeat, actionCount: 2 });
    expect((gameOf(seen).event as Message).phase).toBe(1);
    expect((gameOf(seen).event as Message).phaseEndsAtUnixMs).toBeGreaterThan(Date.now());
  });

  it("refuses a move out of turn, a stale action count and a start from a guest", async () => {
    const { host, guest, code, board } = await startedTable();
    const waiter = board.turnSeat === 0 ? guest : host;

    act(waiter, code, 1, { action: "drop", column: 0 });
    const outOfTurn = await waiter.next("game.acted");
    act(waiter, code, 0, { action: "drop", column: 0 });
    const stale = await waiter.next("game.acted");
    act(guest, code, 1, { action: "start", card: 0 });
    const guestStart = await guest.next("game.acted");

    expect(outOfTurn.reason).toBe("not_your_turn");
    expect(stale.reason).toBe("stale_action");
    expect(guestStart.reason).toBe("invalid");
    expect(waiter.inbox.some((message) => message.type === "game.event")).toBe(false);
  });

  it("ignores a timeout claim before the turn deadline", async () => {
    const { host, guest, code } = await startedTable();

    guest.send({ type: "game.claim", game: { roomId: code } });
    host.send({ type: "game.resync", game: { roomId: code } });
    const snapshot = await host.next("game.snapshot");

    expect(boardOf(snapshot)).toMatchObject({ actionCount: 1, endKind: "" });
    expect(host.inbox.some((message) => message.type === "game.event")).toBe(false);
  });

  it("lets only the host remove a player, and the kicked player hears why", async () => {
    const { host, code } = await openRoom();
    const { guest } = await joinRoom(host, code, "Guest");

    guest.send({ type: "game.kick", userId: "Host@伊弗利特" });
    host.send({ type: "game.kick", userId: "Guest@伊弗利特" });
    const kicked = await guest.next("game.ended");
    const event = await host.next("game.event");

    expect(kicked.reason).toBe("kicked");
    expect(await guest.closed()).toBe(CloseCode.kicked);
    expect(boardOf(event).players).toHaveLength(1);
  });

  it("ends the round in favour of the player who stays when the other leaves mid-game", async () => {
    const { host, guest, code } = await startedTable();

    guest.send({ type: "game.leave" });
    const event = await host.next("game.event");

    expect((gameOf(event).event as Message).phase).toBe(2);
    expect(boardOf(event)).toMatchObject({ endKind: "desertion", winnerSeat: 0 });
    expect(boardOf(event).players).toEqual([
      expect.objectContaining({ seat: 0, wins: 1 }),
      expect.objectContaining({ seat: 1, away: true }),
    ]);
  });

  it("keeps a dropped player's seat mid-game and gives it back when they reconnect", async () => {
    const { host, guest, code } = await startedTable();

    guest.socket.close(1001, "network");
    const away = await host.next("game.event");
    const { attached } = await joinRoom(host, code, "Guest");

    expect(boardOf(away).players).toEqual([
      expect.objectContaining({ seat: 0, away: false }),
      expect.objectContaining({ seat: 1, away: true }),
    ]);
    expect((gameOf(attached).snapshot as Message).phase).toBe(1);
    expect(boardOf(attached).players).toEqual([
      expect.objectContaining({ seat: 0, away: false }),
      expect.objectContaining({ seat: 1, away: false }),
    ]);
  });

  it("refuses a newcomer while a round is being played", async () => {
    const { host, code } = await openRoom("games.uno");
    await joinRoom(host, code, "Second");
    act(host, code, 0, { action: "start", card: 0 });
    await host.next("game.acted");

    const late = await connect(`kind=game&code=${code}&${identity("Late")}`);
    late.send({ type: "game.join", code });
    const declined = await late.next("game.declined");

    expect(declined.reason).toBe("in_progress");
  });

  it("ends the room for everyone when the host leaves and leaves no storage or alarm behind", async () => {
    const { host, guest, code } = await startedTable();

    host.send({ type: "game.close" });
    const ended = await guest.next("game.ended");
    const after = await runInDurableObject(roomStub(code), async (_instance, state) => ({
      rows: (await state.storage.list()).size,
      alarm: await state.storage.getAlarm(),
    }));

    expect(ended).toMatchObject({ reason: "ended", game: { roomId: code } });
    expect(await guest.closed()).toBe(CloseCode.ended);
    expect(after).toEqual({ rows: 0, alarm: null });
  });

  it("drops messages past the rate budget and disconnects a client that keeps flooding", async () => {
    const { host, guest, code, board } = await startedTable();
    const waiter = board.turnSeat === 0 ? guest : host;
    const flood = GAME_RATE.capacity + 45;

    for (let index = 0; index < flood; index++) {
      act(waiter, code, 1, { action: "drop", column: 0 });
    }

    const closeCode = await waiter.closed();
    const answers = waiter.inbox.filter((message) => message.type === "game.acted");

    expect(closeCode).toBe(CloseCode.rate);
    expect(answers.length).toBeGreaterThan(0);
    expect(answers.length).toBeLessThanOrEqual(GAME_RATE.capacity);
    expect(answers.every((answer) => answer.reason === "not_your_turn")).toBe(true);
    expect(host.inbox.concat(guest.inbox).filter((message) => message.type === "game.event")
      .every((message) => boardOf(message).actionCount === 1)).toBe(true);
  });

  it("writes storage only for the closing alarm, never for a move", async () => {
    const { host, code } = await openRoom();
    const writes = await countStorageWrites(code);
    const { guest } = await joinRoom(host, code, "Guest");
    act(host, code, 0, { action: "start", card: 0 });
    const started = boardOf(await host.next("game.event"));
    await guest.next("game.event");
    const afterJoin = writes.length;

    let actionCount = 1;
    let turnSeat = started.turnSeat as number;
    for (const column of [0, 1, 0, 1, 0, 1, 0]) {
      const mover = turnSeat === 0 ? host : guest;
      act(mover, code, actionCount, { action: "drop", column });
      const board = boardOf(await mover.next("game.event"));
      await (mover === host ? guest : host).next("game.event");
      actionCount = board.actionCount as number;
      turnSeat = board.turnSeat as number;
    }

    const afterGame = writes.length;
    host.send({ type: "game.close" });
    await guest.next("game.ended");

    expect(afterJoin).toBe(1);
    expect(afterGame).toBe(afterJoin);
    expect(writes).toEqual(["setAlarm", "deleteAlarm"]);
  });

  it("keeps game rooms apart from watch party and Jam rooms", async () => {
    const { code } = await openRoom();

    const asParty = await self.fetch(realtimeRequest(`code=${code}&${identity("Guest")}`));
    const asJam = await self.fetch(realtimeRequest(`kind=jam&code=${code}&${identity("Guest")}`));

    expect(asParty.status).toBe(404);
    expect(asJam.status).toBe(404);
  });
});
