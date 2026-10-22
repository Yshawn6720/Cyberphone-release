import { abortAllDurableObjects, listDurableObjectIds, reset, runInDurableObject } from "cloudflare:test";
import { afterEach, describe, expect, it } from "vitest";
import { DECLINE_CODE, DECLINE_FULL, DECLINE_HOST_AWAY } from "../src/call";
import worker, { type Env } from "../src/index";
import {
  CALL_FRAME_RATE,
  CODE_ALPHABET,
  CloseCode,
  DECLINE_DENIED,
  MAX_CALL_FRAME_BYTES,
  MAX_CALL_MEMBERS,
  MEDIA_HEADER_BYTES,
  MEDIA_SLOT_OFFSET,
  RefusalClose,
  maxRooms,
} from "../src/protocol";
import { type Message, Peer, env, identity, realtimeRequest, refusedWith, self } from "./support";

const STORAGE_WRITES = ["put", "delete", "deleteAll", "setAlarm", "deleteAlarm", "transaction", "transactionSync"];

const enabledEnv: Env = { ...env, CALLS_ENABLED: "1" };
const APPROVING_JOIN: Message = { type: "call.join", approvalRequired: true };

async function connectCall(query: string): Promise<Peer> {
  const response = await worker.fetch(realtimeRequest(`kind=call&${query}`), enabledEnv);
  expect(response.status).toBe(101);
  if (response.webSocket === null) {
    throw new Error("missing webSocket");
  }

  return new Peer(response.webSocket);
}

async function startCall(name = "Host", join: Message = { type: "call.join" }): Promise<{ host: Peer; code: string; roster: Message }> {
  const host = await connectCall(`create=1&${identity(name)}`);
  host.send(join);
  const roster = await host.next("call.roster");
  return { host, code: roster.code as string, roster };
}

async function knock(code: string, name: string): Promise<Peer> {
  const peer = await connectCall(`code=${code}&${identity(name)}`);
  peer.send({ type: "call.join", code });
  return peer;
}

async function joinCall(code: string, name: string): Promise<{ peer: Peer; roster: Message }> {
  const peer = await knock(code, name);
  return { peer, roster: await peer.next("call.roster") };
}

function userId(name: string): string {
  return `${name}@伊弗利特`;
}

async function settle(peer: Peer): Promise<void> {
  peer.send({ type: "call.join" });
  await peer.next("call.roster");
}

function frame(forgedSlot: number, payloadBytes = 60): Uint8Array {
  const bytes = new Uint8Array(MEDIA_HEADER_BYTES + payloadBytes);
  bytes[0] = 1;
  bytes[MEDIA_SLOT_OFFSET] = forgedSlot;
  for (let index = MEDIA_HEADER_BYTES; index < bytes.length; index++) {
    bytes[index] = index & 0xff;
  }

  return bytes;
}

function callStub(slot: number): DurableObjectStub {
  return env.CALL_ROOMS.get(env.CALL_ROOMS.idFromName(`slot:${slot}`));
}

afterEach(async () => {
  await abortAllDurableObjects();
  await reset();
});

describe("call", () => {
  it("refuses every call request with calls_disabled while CALLS_ENABLED is off, as wrangler.toml ships it, and creates no room", async () => {
    const created = await refusedWith(await self.fetch(realtimeRequest(`kind=call&create=1&${identity("Host")}`)));
    const joined = await refusedWith(await self.fetch(realtimeRequest(`kind=call&code=ABCDEF&${identity("Guest")}`)));
    const unauthorized = await self.fetch(realtimeRequest(`kind=call&create=1&${identity("Host")}`, "wrong"));

    expect(created).toEqual({ message: { type: "relay.refused", reason: "calls_disabled" }, closeCode: RefusalClose.calls_disabled });
    expect(joined.message.reason).toBe("calls_disabled");
    expect(unauthorized.status).toBe(401);
    expect(await listDurableObjectIds(env.CALL_ROOMS)).toEqual([]);
  });

  it("gives the creator a code and admits a guest who brings it, with one call id and distinct slots", async () => {
    const { host, code, roster: first } = await startCall();
    expect(code).toMatch(/^[A-Z2-9]{6}$/);
    expect(first.participants).toEqual([expect.objectContaining({ userId: "Host@伊弗利特", slot: 0, state: "active" })]);

    const { roster } = await joinCall(code, "Guest");
    const hostView = await host.next("call.roster");

    const participants = [
      expect.objectContaining({ userId: "Host@伊弗利特", name: "Host", world: "伊弗利特", slot: 0, state: "active" }),
      expect.objectContaining({ userId: "Guest@伊弗利特", name: "Guest", slot: 1, state: "active" }),
    ];
    expect(roster).toMatchObject({ callId: first.callId, code, participants });
    expect(hostView).toMatchObject({ callId: first.callId, code, participants });
  });

  it("forwards a binary frame only to the other joined members, stamped with the sender's own slot", async () => {
    const { host, code } = await startCall();
    const { peer: guest } = await joinCall(code, "Guest");
    await host.next("call.roster");
    const waiting = await connectCall(`code=${code}&${identity("Waiting")}`);
    const sent = frame(3);

    guest.sendFrame(sent);
    const [received] = await host.framesAtLeast(1);
    await settle(guest);
    waiting.send({ type: "call.join", code });
    await waiting.next("call.roster");

    expect(received[MEDIA_SLOT_OFFSET]).toBe(1);
    expect(received.slice(MEDIA_HEADER_BYTES)).toEqual(sent.slice(MEDIA_HEADER_BYTES));
    expect(guest.frames).toEqual([]);
    expect(waiting.frames).toEqual([]);
  });

  it(`holds at most ${MAX_CALL_MEMBERS} members and declines the next one as full`, async () => {
    const { code } = await startCall();
    for (let index = 1; index < MAX_CALL_MEMBERS; index++) {
      await joinCall(code, `Guest${index}`);
    }

    const extra = await connectCall(`code=${code}&${identity("Extra")}`);
    extra.send({ type: "call.join", code });

    expect(await extra.next("call.declined")).toMatchObject({ reason: DECLINE_FULL });
    expect(await extra.closed()).toBe(CloseCode.ended);
  });

  it("declines a code that does not match the call in that slot", async () => {
    const { code } = await startCall();
    const wrong = code[0] + (code.slice(1) === "ZZZZZ" ? "YYYYY" : "ZZZZZ");
    const stranger = await connectCall(`code=${wrong}&${identity("Stranger")}`);
    stranger.send({ type: "call.join", code: wrong });

    expect(await stranger.next("call.declined")).toMatchObject({ reason: DECLINE_CODE });
    expect(await stranger.closed()).toBe(CloseCode.ended);
  });

  it("drops frames beyond the per member rate and disconnects a member that keeps flooding", async () => {
    const { host, code } = await startCall();
    const { peer: guest } = await joinCall(code, "Guest");
    await host.next("call.roster");

    const burst = 150;
    const startedAt = Date.now();
    for (let index = 0; index < burst; index++) {
      guest.sendFrame(frame(1));
    }

    await settle(guest);
    await settle(host);
    const refilled = Math.ceil((Date.now() - startedAt) / CALL_FRAME_RATE.refillMs);
    expect(host.frames.length).toBeGreaterThanOrEqual(CALL_FRAME_RATE.capacity);
    expect(host.frames.length).toBeLessThanOrEqual(CALL_FRAME_RATE.capacity + refilled);
    expect(host.frames.length).toBeLessThan(burst);

    for (let index = 0; index < 400; index++) {
      guest.sendFrame(frame(1));
    }

    expect(await guest.closed()).toBe(CloseCode.rate);
    expect((await host.next("call.roster")).participants).toEqual([expect.objectContaining({ userId: "Host@伊弗利特" })]);
  });

  it("ignores frames shorter than the header and disconnects a member that sends one larger than an Opus packet", async () => {
    const { host, code } = await startCall();
    const { peer: guest } = await joinCall(code, "Guest");
    await host.next("call.roster");

    guest.sendFrame(new Uint8Array(MEDIA_HEADER_BYTES - 1));
    guest.sendFrame(new Uint8Array(MAX_CALL_FRAME_BYTES + 1));

    expect(await guest.closed()).toBe(CloseCode.tooBig);
    await host.next("call.roster");
    await settle(host);
    expect(host.frames).toEqual([]);
  });

  it("removes a member who leaves from everyone's roster and closes only that connection", async () => {
    const { host, code } = await startCall();
    const { peer: guest } = await joinCall(code, "Guest");
    await host.next("call.roster");

    guest.send({ type: "call.leave" });

    expect(await guest.closed()).toBe(CloseCode.ended);
    expect((await host.next("call.roster")).participants).toEqual([expect.objectContaining({ userId: "Host@伊弗利特" })]);
    expect(host.closeCode).toBeNull();
  });

  it("lets the same character reconnect with the code, replacing the stale connection and keeping the call", async () => {
    const { host, code, roster } = await startCall();

    const { roster: rejoined } = await joinCall(code, "Host");

    expect(await host.closed()).toBe(CloseCode.replaced);
    expect(rejoined).toMatchObject({
      callId: roster.callId,
      code,
      participants: [expect.objectContaining({ userId: "Host@伊弗利特", slot: 0 })],
    });
  });

  it("writes nothing to storage and schedules no alarm for a whole call, approvals included", async () => {
    const writes: string[] = [];
    const slots = maxRooms(env.MAX_ROOMS_CALL, env.MAX_ROOMS);
    for (let slot = 0; slot < slots; slot++) {
      await runInDurableObject(callStub(slot), (_instance, state) => {
        const storage = state.storage as unknown as Record<string, unknown>;
        for (const name of STORAGE_WRITES) {
          const original = storage[name] as (...args: unknown[]) => unknown;
          storage[name] = (...args: unknown[]) => {
            writes.push(name);
            return original.apply(state.storage, args);
          };
        }

        const sql = state.storage.sql as unknown as Record<string, unknown>;
        const exec = sql.exec as (...args: unknown[]) => unknown;
        sql.exec = (...args: unknown[]) => {
          writes.push("sql");
          return exec.apply(state.storage.sql, args);
        };
      });
    }

    const { host, code } = await startCall("Host", APPROVING_JOIN);
    const guest = await knock(code, "Guest");
    await host.next("call.joinRequest");
    host.send({ type: "call.approve", userId: userId("Guest") });
    await guest.next("call.roster");
    await host.next("call.roster");
    const turnedAway = await knock(code, "Stranger");
    await host.next("call.joinRequest");
    host.send({ type: "call.deny", userId: userId("Stranger") });
    await turnedAway.closed();
    for (let index = 0; index < 20; index++) {
      guest.sendFrame(frame(1));
      host.sendFrame(frame(0));
    }

    await host.framesAtLeast(20);
    await guest.framesAtLeast(20);
    guest.send({ type: "call.leave" });
    await guest.closed();
    await host.next("call.roster");

    const slot = CODE_ALPHABET.indexOf(code[0]);
    const stored = await runInDurableObject(callStub(slot), async (_instance, state) => ({
      keys: (await state.storage.list()).size,
      alarm: await state.storage.getAlarm(),
    }));
    expect(writes).toEqual([]);
    expect(stored).toEqual({ keys: 0, alarm: null });
  });

  it("ends the creator's call for everyone when the same character starts a new call on that slot", async () => {
    const { host, code, roster } = await startCall();
    const { peer: guest } = await joinCall(code, "Guest");
    await host.next("call.roster");

    const again = await startCall();

    expect(await guest.next("call.ended")).toMatchObject({ type: "call.ended" });
    expect(await guest.closed()).toBe(CloseCode.ended);
    expect(await host.closed()).toBe(CloseCode.ended);
    expect(again.roster.callId).not.toBe(roster.callId);
    expect(again.roster.participants).toEqual([expect.objectContaining({ userId: userId("Host"), slot: 0 })]);
  });

  it("still refuses a new call on a slot whose call another character holds after its creator left", async () => {
    const { host, code } = await startCall();
    const { peer: guest } = await joinCall(code, "Guest");
    await host.next("call.roster");
    host.send({ type: "call.leave" });
    await host.closed();
    await guest.next("call.roster");

    const fresh = await startCall();

    expect(fresh.code[0]).not.toBe(code[0]);
    expect(guest.closeCode).toBeNull();
  });
});

describe("call approval", () => {
  it("holds a newcomer as pending, with no slot, frames or roster, until the creator approves", async () => {
    const { host, code } = await startCall("Host", APPROVING_JOIN);
    const guest = await knock(code, "Guest");

    expect(await guest.next("call.joinPending")).toEqual({ type: "call.joinPending" });
    expect(await host.next("call.joinRequest")).toMatchObject({
      userId: userId("Guest"),
      from: { userId: userId("Guest"), name: "Guest", world: "伊弗利特" },
    });

    host.sendFrame(frame(0));
    guest.sendFrame(frame(1));
    await settle(host);
    guest.send({ type: "call.join", code });
    await guest.next("call.joinPending");
    expect(guest.frames).toEqual([]);
    expect(host.frames).toEqual([]);
    expect(guest.inbox.filter((message) => message.type === "call.roster")).toEqual([]);

    host.send({ type: "call.approve", userId: userId("Guest") });

    const participants = [
      expect.objectContaining({ userId: userId("Host"), slot: 0 }),
      expect.objectContaining({ userId: userId("Guest"), slot: 1 }),
    ];
    expect((await guest.next("call.roster")).participants).toEqual(participants);
    expect((await host.next("call.roster")).participants).toEqual(participants);
  });

  it("declines a newcomer the creator denies with reason denied and leaves the call as it was", async () => {
    const { host, code } = await startCall("Host", APPROVING_JOIN);
    const guest = await knock(code, "Guest");
    await host.next("call.joinRequest");

    host.send({ type: "call.deny", userId: userId("Guest") });

    expect(await guest.next("call.declined")).toEqual({ type: "call.declined", reason: DECLINE_DENIED });
    expect(await guest.closed()).toBe(CloseCode.ended);
    host.send({ type: "call.join" });
    expect((await host.next("call.roster")).participants).toEqual([expect.objectContaining({ userId: userId("Host") })]);
    expect(guest.inbox.filter((message) => message.type === "call.roster")).toEqual([]);
  });

  it("ignores approve and deny from anyone but the creator", async () => {
    const { host, code } = await startCall("Host", APPROVING_JOIN);
    const member = await knock(code, "Member");
    await host.next("call.joinRequest");
    host.send({ type: "call.approve", userId: userId("Member") });
    await member.next("call.roster");
    await host.next("call.roster");
    const waiting = await knock(code, "Waiting");
    await waiting.next("call.joinPending");
    await host.next("call.joinRequest");

    member.send({ type: "call.approve", userId: userId("Waiting") });
    member.send({ type: "call.deny", userId: userId("Waiting") });
    await settle(member);
    waiting.send({ type: "call.join", code });

    expect(await waiting.next("call.joinPending")).toEqual({ type: "call.joinPending" });
    expect(waiting.closeCode).toBeNull();
    expect(waiting.inbox.filter((message) => message.type === "call.roster")).toEqual([]);
  });

  it("keeps the open call for a host whose client does not ask for approval", async () => {
    const { host, code } = await startCall();

    const { peer: guest, roster } = await joinCall(code, "Guest");

    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: userId("Host"), slot: 0 }),
      expect.objectContaining({ userId: userId("Guest"), slot: 1 }),
    ]);
    expect((await host.next("call.roster")).participants).toHaveLength(2);
    expect(host.inbox.filter((message) => message.type === "call.joinRequest")).toEqual([]);
    expect(guest.inbox.filter((message) => message.type === "call.joinPending")).toEqual([]);
  });

  it("tells the creator when a waiting newcomer gives up", async () => {
    const { host, code } = await startCall("Host", APPROVING_JOIN);
    const guest = await knock(code, "Guest");
    await host.next("call.joinRequest");

    guest.send({ type: "call.leave" });

    expect(await host.next("call.joinCancelled")).toEqual({ type: "call.joinCancelled", userId: userId("Guest") });
  });

  it("declines waiting newcomers with host_away when the creator leaves, and keeps the call for those inside", async () => {
    const { host, code } = await startCall("Host", APPROVING_JOIN);
    const member = await knock(code, "Member");
    await host.next("call.joinRequest");
    host.send({ type: "call.approve", userId: userId("Member") });
    await member.next("call.roster");
    const waiting = await knock(code, "Waiting");
    await host.next("call.joinRequest");

    host.send({ type: "call.leave" });

    expect(await waiting.next("call.declined")).toEqual({ type: "call.declined", reason: DECLINE_HOST_AWAY });
    expect(await waiting.closed()).toBe(CloseCode.ended);
    expect((await member.next("call.roster")).participants).toEqual([expect.objectContaining({ userId: userId("Member") })]);
    expect(member.closeCode).toBeNull();
  });

  it("while the creator is away declines newcomers but lets a member the creator approved reconnect", async () => {
    const { host, code } = await startCall("Host", APPROVING_JOIN);
    const member = await knock(code, "Member");
    await host.next("call.joinRequest");
    host.send({ type: "call.approve", userId: userId("Member") });
    await member.next("call.roster");
    const keeper = await knock(code, "Keeper");
    await host.next("call.joinRequest");
    host.send({ type: "call.approve", userId: userId("Keeper") });
    await keeper.next("call.roster");
    host.send({ type: "call.leave" });
    await host.closed();
    member.send({ type: "call.leave" });
    await member.closed();
    await keeper.next("call.roster");

    const late = await knock(code, "Late");
    const { roster } = await joinCall(code, "Member");

    expect(await late.next("call.declined")).toEqual({ type: "call.declined", reason: DECLINE_HOST_AWAY });
    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: userId("Member") }),
      expect.objectContaining({ userId: userId("Keeper") }),
    ]);
  });

  it("lets the creator reconnect with the code without approval and hands it the requests still waiting", async () => {
    const { host, code, roster } = await startCall("Host", APPROVING_JOIN);
    const guest = await knock(code, "Guest");
    await host.next("call.joinRequest");

    const { peer: rejoined, roster: rejoinedRoster } = await joinCall(code, "Host");

    expect(await host.closed()).toBe(CloseCode.replaced);
    expect(rejoinedRoster).toMatchObject({ callId: roster.callId, participants: [expect.objectContaining({ userId: userId("Host") })] });
    expect(await rejoined.next("call.joinRequest")).toMatchObject({ userId: userId("Guest") });
    rejoined.send({ type: "call.approve", userId: userId("Guest") });
    expect((await guest.next("call.roster")).participants).toHaveLength(2);
  });
});
