import {
  abortAllDurableObjects,
  evictDurableObject,
  listDurableObjectIds,
  reset,
  runDurableObjectAlarm,
  runInDurableObject,
} from "cloudflare:test";
import { afterEach, describe, expect, it } from "vitest";
import worker, { type Env } from "../src/index";
import type { PartyRoom } from "../src/room";
import {
  CODE_ALPHABET,
  CloseCode,
  DROP_LIMIT,
  HOST_RATE,
  MAX_MEMBERS,
  MAX_MESSAGE_BYTES,
  MAX_PENDING_SUGGESTIONS,
  REACTION_RATE,
  RATE_RETRY_AFTER_SECONDS,
  RefusalClose,
  VIEWER_RATE,
} from "../src/protocol";
import { BASE, INVITE, type Message, Peer, connect, env, identity, realtimeRequest, refusedWith, self } from "./support";

async function openRoom(name = "Host", approvalRequired = false, who = identity(name)): Promise<{ host: Peer; code: string }> {
  const host = await connect(`create=1&${who}`);
  host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 1, paused: false, guestPermissions: 0, approvalRequired });
  const echo = await host.next("stream.state");
  return { host, code: echo.code as string };
}

async function joinRoom(host: Peer, code: string, name: string): Promise<Peer> {
  const viewer = await connect(`code=${code}&${identity(name)}`);
  viewer.send({ type: "stream.join", code, features: 1 });
  await host.next("stream.roster");
  host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 2, paused: false, guestPermissions: 0 });
  await host.next("stream.state");
  await viewer.next("stream.joined");
  return viewer;
}

async function askToJoin(host: Peer, code: string, name: string, who = identity(name)): Promise<{ guest: Peer; request: Message }> {
  const guest = await connect(`code=${code}&${who}`);
  guest.send({ type: "stream.join", code, features: 1 });
  await guest.next("stream.joinPending");
  const request = await host.next("stream.joinRequest");
  return { guest, request };
}

async function approve(host: Peer, guest: Peer, userId: string): Promise<Message> {
  host.send({ type: "stream.approve", userId });
  const roster = await host.next("stream.roster");
  host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 3, paused: false });
  await host.next("stream.state");
  await guest.next("stream.joined");
  return roster;
}

function slotStub(code: string): DurableObjectStub<PartyRoom> {
  const slot = CODE_ALPHABET.indexOf(code[0]);
  return env.ROOMS.get(env.ROOMS.idFromName(`slot:${slot}`)) as DurableObjectStub<PartyRoom>;
}

function countingLimiter(limit: number): RateLimit {
  const counts = new Map<string, number>();
  return {
    async limit({ key }) {
      const count = (counts.get(key) ?? 0) + 1;
      counts.set(key, count);
      return { success: count <= limit };
    },
  };
}

async function upgradeOutcome(query: string, overrides: Partial<Env>, address = "192.0.2.1"): Promise<string> {
  const request = realtimeRequest(query);
  request.headers.set("CF-Connecting-IP", address);
  const response = await worker.fetch(request, { ...env, ...overrides });
  if (response.webSocket === null) {
    return `http ${response.status}`;
  }

  return await new Peer(response.webSocket).refusal() ?? "open";
}

afterEach(async () => {
  await abortAllDurableObjects();
  await reset();
});

describe("invite gate", () => {
  it("rejects a wrong invite at the Worker without creating a Durable Object", async () => {
    const before = (await listDurableObjectIds(env.ROOMS)).length;

    const realtime = await self.fetch(realtimeRequest(`create=1&${identity("Host")}`, "wrong-invite"));
    const challenge = await self.fetch(`${BASE}/auth/challenge`, {
      method: "POST",
      body: JSON.stringify({ name: "Host", world: "Tonberry", invite: "wrong-invite" }),
    });

    expect(realtime.status).toBe(401);
    expect(challenge.status).toBe(401);
    expect((await listDurableObjectIds(env.ROOMS)).length).toBe(before);
  });

  it("fails closed when the invite secret is missing", async () => {
    const response = await worker.fetch(realtimeRequest(`create=1&${identity("Host")}`), { ...env, INVITE_CODE: undefined });

    expect(response.status).toBe(500);
  });

  it("issues a session token through challenge and verify that works for /me and /rt", async () => {
    const challenge = await self.fetch(`${BASE}/auth/challenge`, {
      method: "POST",
      body: JSON.stringify({ name: "Host", world: "Tonberry", invite: INVITE }),
    });
    const { challengeId } = await challenge.json<{ challengeId: string }>();
    const verify = await self.fetch(`${BASE}/auth/verify`, { method: "POST", body: JSON.stringify({ challengeId }) });
    const verified = await verify.json<{ ok: boolean; token: string; user: { id: string } }>();
    const me = await self.fetch(`${BASE}/me`, { headers: { Authorization: `Bearer ${verified.token}` } });
    const tampered = `${verified.token.slice(0, -2)}AA`;
    const forged = await self.fetch(`${BASE}/me`, { headers: { Authorization: `Bearer ${tampered}` } });
    const challengeAsSession = await self.fetch(`${BASE}/me`, { headers: { Authorization: `Bearer ${challengeId}` } });

    expect(verified.ok).toBe(true);
    expect(verified.user.id).toBe("Host@Tonberry");
    expect((await me.json<{ id: string }>()).id).toBe("Host@Tonberry");
    expect(forged.status).toBe(401);
    expect(challengeAsSession.status).toBe(401);
    const host = await connect("create=1", verified.token);
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 0, paused: true });
    expect((await host.next("stream.state")).hostId).toBe("Host@Tonberry");
  });
});

describe("watch party", () => {
  it("creates a room and assigns a code on the host's first state", async () => {
    const host = await connect(`create=1&${identity("Host")}`);
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 5, paused: false });

    const echo = await host.next("stream.state");

    expect(echo.code).toMatch(/^[A-Z0-9]{6}$/);
    expect(echo.features).toBe(1);
    expect(echo.hostId).toBe("Host@伊弗利特");
    expect(typeof echo.stateAtUnixMs).toBe("number");
    expect(echo.participants).toEqual([expect.objectContaining({ userId: "Host@伊弗利特", slot: 0 })]);
  });

  it("lets a second client join by code and then receive the host's state", async () => {
    const { host, code } = await openRoom();
    const viewer = await connect(`code=${code.toLowerCase()}&${identity("Guest")}`);
    viewer.send({ type: "stream.join", code, features: 1 });

    const roster = await host.next("stream.roster");
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 9, paused: false, guestPermissions: 2 });
    const joined = await viewer.next("stream.joined");
    const echo = await host.next("stream.state");
    host.send({ type: "stream.state", url: "https://video.test/b", positionSeconds: 0, paused: true, guestPermissions: 2 });
    const state = await viewer.next("stream.state");

    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特", slot: 0 }),
      expect.objectContaining({ userId: "Guest@伊弗利特", slot: 1, displayName: "Guest" }),
    ]);
    expect(joined).toMatchObject({ hostId: "Host@伊弗利特", code, features: 1, url: "https://video.test/a", positionSeconds: 9, guestPermissions: 2 });
    expect(typeof joined.stateAtUnixMs).toBe("number");
    expect(echo.members).toEqual([{ userId: "Guest@伊弗利特", flags: 4 }]);
    expect(state).toMatchObject({ url: "https://video.test/b", paused: true, hostId: "Host@伊弗利特" });
  });

  it("readmits a reconnecting viewer whose join names a host that has since handed off", async () => {
    const { host, code } = await openRoom();
    const first = await joinRoom(host, code, "First");
    await joinRoom(host, code, "Second");
    host.send({ type: "stream.transfer", userId: "First@伊弗利特" });
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 4, paused: false });
    await first.next("stream.hostChanged");
    first.inbox.length = 0;

    const returning = await connect(`code=${code}&${identity("Second")}`);
    returning.send({ type: "stream.join", hostId: "Host@伊弗利特", features: 1 });
    await first.next("stream.roster");
    first.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 5, paused: false });
    const joined = await returning.next("stream.joined");

    expect(joined.hostId).toBe("First@伊弗利特");
    expect(returning.inbox.some((message) => message.type === "stream.declined")).toBe(false);
  });

  it("sends joined from the new host to a viewer whose join raced a transfer", async () => {
    const { host, code } = await openRoom();
    const first = await joinRoom(host, code, "First");
    host.send({ type: "stream.transfer", userId: "First@伊弗利特" });
    await host.next("stream.roster");
    const late = await connect(`code=${code}&${identity("Late")}`);
    late.send({ type: "stream.join", code, features: 1 });
    await host.next("stream.roster");

    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 6, paused: false });
    await first.next("stream.hostChanged");
    first.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 7, paused: false });
    const joined = await late.next("stream.joined");

    expect(joined).toMatchObject({ hostId: "First@伊弗利特", positionSeconds: 7 });
    expect(late.inbox.some((message) => message.type === "stream.hostChanged")).toBe(false);
  });

  it("keeps a requested transfer while its target reconnects and completes it once they rejoin", async () => {
    const { host, code } = await openRoom();
    await joinRoom(host, code, "First");
    host.send({ type: "stream.transfer", userId: "First@伊弗利特" });
    await host.next("stream.roster");
    const returning = await connect(`code=${code}&${identity("First")}`);
    returning.send({ type: "stream.join", code, features: 1 });
    await host.next("stream.roster");

    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 8, paused: false });
    await returning.next("stream.joined");
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 9, paused: false });
    const changed = await returning.next("stream.hostChanged");

    expect(changed).toMatchObject({ hostId: "First@伊弗利特", userId: "Host@伊弗利特" });
  });

  it("returns 404 for a code with no room", async () => {
    const response = await self.fetch(realtimeRequest(`code=${CODE_ALPHABET[0]}AAAAA&${identity("Guest")}`));

    expect(response.status).toBe(404);
  });

  it("ends the room for everyone when the host leaves, freeing that slot for a new room", async () => {
    const { host, code } = await openRoom("A");
    await openRoom("B");
    await openRoom("C");
    const viewer = await joinRoom(host, code, "Guest");

    host.send({ type: "stream.leave" });
    const ended = await viewer.next("stream.ended");

    expect(ended.hostId).toBe("A@伊弗利特");
    expect(await viewer.closed()).toBe(CloseCode.ended);
    const again = await openRoom("Next");
    expect(again.code[0]).toBe(code[0]);
  });

  it("ends the room when the host's socket drops", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");

    host.socket.close(1001, "gone");

    expect((await viewer.next("stream.ended")).reason).toBe("host_left");
  });

  it("lets a host whose old socket never closed reopen by ending that room, even with every slot full", async () => {
    const stale = await openRoom("Host");
    const viewer = await joinRoom(stale.host, stale.code, "Guest");
    await openRoom("B");
    await openRoom("C");

    const again = await openRoom("Host");

    expect((await viewer.next("stream.ended")).hostId).toBe("Host@伊弗利特");
    expect(await viewer.closed()).toBe(CloseCode.ended);
    expect(again.code[0]).toBe(stale.code[0]);
    expect(again.code).not.toBe(stale.code);
  });

  it("tells the host when a viewer leaves", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");

    viewer.send({ type: "stream.leave" });
    const left = await host.next("stream.left");
    const roster = await host.next("stream.roster");

    expect(left.userId).toBe("Guest@伊弗利特");
    expect(roster.participants).toEqual([expect.objectContaining({ userId: "Host@伊弗利特" })]);
  });

  it("forwards a viewer control to the host with a full sender", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");

    viewer.send({ type: "stream.control", action: "seek", positionSeconds: 42 });
    const request = await host.next("stream.controlRequest");

    expect(request).toMatchObject({ action: "seek", positionSeconds: 42, userId: "Guest@伊弗利特" });
    expect(request.from).toMatchObject({ userId: "Guest@伊弗利特", name: "Guest", world: "伊弗利特", displayName: "Guest", state: "active", muted: false });
  });

  it("transfers the host role on the next state and swaps roles", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");

    host.send({ type: "stream.transfer", userId: "Guest@伊弗利特" });
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 30, paused: false });
    const toOld = await host.next("stream.hostChanged");
    const toNew = await viewer.next("stream.hostChanged");
    viewer.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 31, paused: false });
    const echo = await viewer.next("stream.state");
    const forwarded = await host.next("stream.state");
    host.send({ type: "stream.leave" });
    const left = await viewer.next("stream.left");

    expect(toOld).toMatchObject({ hostId: "Guest@伊弗利特", userId: "Host@伊弗利特", url: "https://video.test/a", positionSeconds: 30, code });
    expect(toNew.participants).toEqual([
      expect.objectContaining({ userId: "Guest@伊弗利特", slot: 0 }),
      expect.objectContaining({ userId: "Host@伊弗利特", slot: 1 }),
    ]);
    expect(echo.hostId).toBe("Guest@伊弗利特");
    expect(forwarded.positionSeconds).toBe(31);
    expect(left.userId).toBe("Host@伊弗利特");
    expect(viewer.inbox.some((message) => message.type === "stream.ended")).toBe(false);
  });

  it("keeps room state and spent rate tokens across hibernation", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");
    for (let index = 1; index < VIEWER_RATE.capacity; index++) {
      viewer.send({ type: "stream.control", action: "pause" });
      await host.next("stream.controlRequest");
    }

    await evictDurableObject(slotStub(code));
    host.send({ type: "stream.state", url: "https://video.test/c", positionSeconds: 3, paused: false });
    const state = await viewer.next("stream.state");
    for (let index = 0; index <= DROP_LIMIT; index++) {
      viewer.send({ type: "stream.control", action: "pause" });
    }

    expect(state).toMatchObject({ url: "https://video.test/c", code, hostId: "Host@伊弗利特" });
    expect(await viewer.closed()).toBe(CloseCode.rate);
  });
});

describe("hard caps", () => {
  it("refuses a room beyond MAX_ROOMS after probing every occupied slot, with the reason on an open socket", async () => {
    const rooms = [await openRoom("A"), await openRoom("B"), await openRoom("C")];

    const { message, closeCode } = await refusedWith(await self.fetch(realtimeRequest(`create=1&${identity("D")}`)));

    expect(new Set(rooms.map((room) => room.code[0])).size).toBe(3);
    expect(message).toEqual({ type: "relay.refused", reason: "rooms_full", maxRooms: 3 });
    expect(closeCode).toBe(RefusalClose.rooms_full);
  });

  it("caps each room kind by its own limit ahead of the shared MAX_ROOMS", async () => {
    const overrides = { MAX_ROOMS_PARTY: "2" };
    const party = [
      await upgradeOutcome(`create=1&${identity("A")}`, overrides),
      await upgradeOutcome(`create=1&${identity("B")}`, overrides),
      await upgradeOutcome(`create=1&${identity("C")}`, overrides),
    ];

    const jam = await upgradeOutcome(`kind=jam&create=1&${identity("D")}`, overrides);

    expect(party).toEqual(["open", "open", "rooms_full"]);
    expect(jam).toBe("open");
  });

  it("refuses a connection beyond MAX_MEMBERS at upgrade time", async () => {
    const { code } = await openRoom();
    for (let index = 1; index < MAX_MEMBERS; index++) {
      await connect(`code=${code}&${identity(`Guest${index}`)}`);
    }

    const response = await self.fetch(realtimeRequest(`code=${code}&${identity("Late")}`));

    expect(response.status).toBe(409);
  });

  it("drops a fast host's excess states without ending the room", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");
    host.inbox.length = 0;

    for (let index = 0; index < HOST_RATE.capacity + 5; index++) {
      host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: index });
    }

    viewer.send({ type: "stream.control", action: "pause" });
    await host.next("stream.controlRequest");
    const echoes = host.inbox.filter((message) => message.type === "stream.state").length;
    expect(echoes).toBeGreaterThan(0);
    expect(echoes).toBeLessThan(HOST_RATE.capacity + 5);
    expect(host.closeCode).toBeNull();
    expect(viewer.closeCode).toBeNull();
  });

  it("closes a viewer that keeps flooding past the drop limit", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");

    for (let index = 0; index < VIEWER_RATE.capacity + DROP_LIMIT + 2; index++) {
      viewer.send({ type: "stream.control", action: "pause" });
    }

    expect(await viewer.closed()).toBe(CloseCode.rate);
    expect((await host.next("stream.left")).userId).toBe("Guest@伊弗利特");
  });

  it("closes a connection that sends a binary frame", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");

    viewer.socket.send(new Uint8Array([1, 2, 3]));

    expect(await viewer.closed()).toBe(CloseCode.unsupported);
    expect((await host.next("stream.left")).userId).toBe("Guest@伊弗利特");
  });

  it("measures the message cap in UTF-8 bytes, not characters", async () => {
    const { host } = await openRoom();
    const title = "伊".repeat(Math.ceil(MAX_MESSAGE_BYTES / 3) + 1);

    host.send({ type: "stream.state", url: "https://video.test/a", upcomingQueue: [{ url: "https://video.test/b", title }] });

    expect(title.length).toBeLessThan(MAX_MESSAGE_BYTES);
    expect(await host.closed()).toBe(CloseCode.tooBig);
  });
});

describe("connection limits", () => {
  it("counts connections per character, so one character's burst leaves another free to connect", async () => {
    const overrides = { CONNECT_LIMITER: countingLimiter(2) };
    const allowed = [
      await upgradeOutcome(`create=1&${identity("Busy")}`, overrides),
      await upgradeOutcome(`kind=jam&create=1&${identity("Busy")}`, overrides),
    ];

    const refused = await upgradeOutcome(`kind=game&create=1&${identity("Busy")}`, overrides);
    const other = await upgradeOutcome(`create=1&${identity("Calm")}`, overrides);

    expect(allowed).toEqual(["open", "open"]);
    expect(refused).toBe("rate_limited");
    expect(other).toBe("open");
  });

  it("counts connections per address across characters", async () => {
    const overrides = { ADDRESS_LIMITER: countingLimiter(2) };
    const allowed = [
      await upgradeOutcome(`create=1&${identity("A")}`, overrides, "192.0.2.1"),
      await upgradeOutcome(`create=1&${identity("B")}`, overrides, "192.0.2.1"),
    ];

    const refused = await upgradeOutcome(`create=1&${identity("C")}`, overrides, "192.0.2.1");
    const otherAddress = await upgradeOutcome(`create=1&${identity("D")}`, overrides, "192.0.2.2");

    expect(allowed).toEqual(["open", "open"]);
    expect(refused).toBe("rate_limited");
    expect(otherAddress).toBe("open");
  });

  it("caps total connections across characters and addresses", async () => {
    const overrides = { TOTAL_LIMITER: countingLimiter(2) };
    const allowed = [
      await upgradeOutcome(`create=1&${identity("A")}`, overrides, "192.0.2.1"),
      await upgradeOutcome(`create=1&${identity("B")}`, overrides, "192.0.2.2"),
    ];

    const refused = await upgradeOutcome(`create=1&${identity("C")}`, overrides, "192.0.2.3");

    expect(allowed).toEqual(["open", "open"]);
    expect(refused).toBe("rate_limited");
  });

  it("tells a rate limited client when to retry, on an open socket closed with 4429", async () => {
    const request = realtimeRequest(`create=1&${identity("Busy")}`);
    const response = await worker.fetch(request, { ...env, CONNECT_LIMITER: countingLimiter(0) });

    const { message, closeCode } = await refusedWith(response);

    expect(message).toEqual({ type: "relay.refused", reason: "rate_limited", retryAfterSeconds: RATE_RETRY_AFTER_SECONDS });
    expect(closeCode).toBe(RefusalClose.rate_limited);
  });
});

describe("zero idle cost", () => {
  it("writes no storage rows and keeps one fixed alarm while a party runs, and leaves no alarm after it ends", async () => {
    const { host, code } = await openRoom();
    const viewer = await joinRoom(host, code, "Guest");
    const alarmAtStart = await runInDurableObject(slotStub(code), (_instance, state) => state.storage.getAlarm());
    for (let index = 0; index < 5; index++) {
      host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: index });
      await viewer.next("stream.state");
    }

    const during = await runInDurableObject(slotStub(code), async (_instance, state) => ({
      rows: (await state.storage.list()).size,
      alarm: await state.storage.getAlarm(),
    }));
    host.send({ type: "stream.leave" });
    await viewer.closed();
    const after = await runInDurableObject(slotStub(code), async (_instance, state) => ({
      rows: (await state.storage.list()).size,
      alarm: await state.storage.getAlarm(),
    }));

    expect(alarmAtStart).not.toBeNull();
    expect(during).toEqual({ rows: 0, alarm: alarmAtStart });
    expect(after).toEqual({ rows: 0, alarm: null });
  });

  it("closes a silent room when its single alarm fires and frees the slot", async () => {
    await openRoom("A");
    await openRoom("B");
    const { host, code } = await openRoom("Silent");
    const stub = slotStub(code);

    await runInDurableObject(stub, (_instance, state) => {
      for (const socket of state.getWebSockets()) {
        const member = socket.deserializeAttachment() as { room: { aloneSince: number } | null };
        if (member.room !== null) {
          member.room.aloneSince = 0;
          socket.serializeAttachment(member);
        }
      }
    });
    const ran = await runDurableObjectAlarm(stub);

    expect(ran).toBe(true);
    expect((await host.next("stream.ended")).reason).toBe("idle");
    expect(await host.closed()).toBe(CloseCode.ended);
    expect(await runDurableObjectAlarm(stub)).toBe(false);
    expect((await openRoom("Next")).code[0]).toBe(code[0]);
  });
});

describe("join approval", () => {
  it("holds a newcomer out of the room until the host approves, then admits them on the next state", async () => {
    const { host, code } = await openRoom("Host", true);

    const { guest, request } = await askToJoin(host, code, "Guest");
    const roster = await approve(host, guest, "Guest@伊弗利特");

    expect(request).toMatchObject({ userId: "Guest@伊弗利特", hostId: "Host@伊弗利特" });
    expect(request.from).toMatchObject({ userId: "Guest@伊弗利特", displayName: "Guest" });
    expect(host.inbox.some((message) => message.type === "stream.roster")).toBe(false);
    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特", slot: 0 }),
      expect.objectContaining({ userId: "Guest@伊弗利特", slot: 1 }),
    ]);
  });

  it("tells a denied newcomer, closes their connection and clears the host's request", async () => {
    const { host, code } = await openRoom("Host", true);
    const { guest } = await askToJoin(host, code, "Guest");

    host.send({ type: "stream.deny", userId: "Guest@伊弗利特" });
    const declined = await guest.next("stream.declined");
    const left = await host.next("stream.left");

    expect(declined).toMatchObject({ reason: "denied", hostId: "Host@伊弗利特" });
    expect(await guest.closed()).toBe(CloseCode.ended);
    expect(left.userId).toBe("Guest@伊弗利特");
  });

  it("clears the host's request when a waiting newcomer gives up, and ignores what they sent while waiting", async () => {
    const { host, code } = await openRoom("Host", true);
    const { guest } = await askToJoin(host, code, "Guest");

    guest.send({ type: "stream.control", action: "pause" });
    guest.send({ type: "stream.react", reaction: 1 });
    guest.send({ type: "stream.queueSuggest", suggestionId: "s-1", url: "https://video.test/b" });
    guest.send({ type: "stream.leave" });
    const left = await host.next("stream.left");

    expect(left.userId).toBe("Guest@伊弗利特");
    expect(host.inbox.map((message) => message.type)).toEqual([]);
  });

  it("lets an approved viewer whose connection dropped come back without asking again", async () => {
    const { host, code } = await openRoom("Host", true);
    const { guest } = await askToJoin(host, code, "Guest");
    await approve(host, guest, "Guest@伊弗利特");
    guest.socket.close(1001, "network");
    await host.next("stream.left");
    await host.next("stream.roster");

    const back = await connect(`code=${code}&${identity("Guest")}`);
    back.send({ type: "stream.join", hostId: "Host@伊弗利特", features: 1 });
    const roster = await host.next("stream.roster");
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 4, paused: false });
    const joined = await back.next("stream.joined");

    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特" }),
      expect.objectContaining({ userId: "Guest@伊弗利特" }),
    ]);
    expect(joined.positionSeconds).toBe(4);
    expect(back.inbox.some((message) => message.type === "stream.joinPending")).toBe(false);
    expect(host.inbox.some((message) => message.type === "stream.joinRequest")).toBe(false);
  });

  it("lets waiting newcomers in once the host turns approval off", async () => {
    const { host, code } = await openRoom("Host", true);
    const { guest } = await askToJoin(host, code, "Guest");

    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 5, paused: false, approvalRequired: false });
    const joined = await guest.next("stream.joined");
    await host.next("stream.state");
    const roster = await host.next("stream.roster");

    expect(joined).toMatchObject({ approvalRequired: false, positionSeconds: 5 });
    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特" }),
      expect.objectContaining({ userId: "Guest@伊弗利特" }),
    ]);
  });

  it("hands a waiting request to the new host after a transfer", async () => {
    const { host, code } = await openRoom("Host", true);
    const first = await askToJoin(host, code, "First");
    await approve(host, first.guest, "First@伊弗利特");
    const late = await askToJoin(host, code, "Late");

    host.send({ type: "stream.transfer", userId: "First@伊弗利特" });
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 6, paused: false });
    await first.guest.next("stream.hostChanged");
    const handed = await first.guest.next("stream.joinRequest");
    first.guest.send({ type: "stream.approve", userId: "Late@伊弗利特" });
    await first.guest.next("stream.roster");
    first.guest.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 7, paused: false });
    const joined = await late.guest.next("stream.joined");

    expect(handed).toMatchObject({ userId: "Late@伊弗利特", hostId: "First@伊弗利特" });
    expect(joined.hostId).toBe("First@伊弗利特");
  });
});

describe("removing a member", () => {
  it("closes the removed member's connection and tells the rest of the room", async () => {
    const { host, code } = await openRoom();
    const stays = await joinRoom(host, code, "Stays");
    const removed = await joinRoom(host, code, "Removed");
    await stays.next("stream.roster");

    host.send({ type: "stream.kick", userId: "Removed@伊弗利特" });
    const kicked = await removed.next("stream.kicked");
    const left = await host.next("stream.left");
    const roster = await host.next("stream.roster");
    const told = await stays.next("stream.left");

    expect(kicked).toMatchObject({ hostId: "Host@伊弗利特", userId: "Removed@伊弗利特" });
    expect(await removed.closed()).toBe(CloseCode.kicked);
    expect(left.userId).toBe("Removed@伊弗利特");
    expect(told.userId).toBe("Removed@伊弗利特");
    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特" }),
      expect.objectContaining({ userId: "Stays@伊弗利特" }),
    ]);
  });

  it("does not ban a removed member, but makes them ask again while approval is on", async () => {
    const { host, code } = await openRoom("Host", true);
    const { guest } = await askToJoin(host, code, "Guest");
    await approve(host, guest, "Guest@伊弗利特");
    host.send({ type: "stream.kick", userId: "Guest@伊弗利特" });
    await guest.closed();
    await host.next("stream.left");

    const again = await askToJoin(host, code, "Guest");
    await approve(host, again.guest, "Guest@伊弗利特");

    expect(again.request.userId).toBe("Guest@伊弗利特");
    expect(again.guest.closeCode).toBeNull();
  });

  it("ignores a removal sent by a viewer", async () => {
    const { host, code } = await openRoom();
    const sender = await joinRoom(host, code, "Sender");
    const target = await joinRoom(host, code, "Target");
    host.inbox.length = 0;

    sender.send({ type: "stream.kick", userId: "Target@伊弗利特" });
    sender.send({ type: "stream.control", action: "pause" });
    await host.next("stream.controlRequest");

    expect(target.closeCode).toBeNull();
    expect(host.inbox.some((message) => message.type === "stream.left")).toBe(false);
  });
});

describe("reactions", () => {
  it("forwards a reaction to everyone else in the room", async () => {
    const { host, code } = await openRoom();
    const sender = await joinRoom(host, code, "Sender");
    const other = await joinRoom(host, code, "Other");
    sender.inbox.length = 0;

    sender.send({ type: "stream.react", reaction: 2 });
    const atHost = await host.next("stream.reaction");
    const atOther = await other.next("stream.reaction");
    host.send({ type: "stream.react", reaction: 5 });
    const fromHost = await sender.next("stream.reaction");

    expect(atHost).toMatchObject({ reaction: 2, userId: "Sender@伊弗利特" });
    expect(atOther).toMatchObject({ reaction: 2, userId: "Sender@伊弗利特" });
    expect(fromHost).toMatchObject({ reaction: 5, userId: "Host@伊弗利特" });
    expect(sender.inbox.some((message) => message.type === "stream.reaction")).toBe(false);
  });

  it("ignores a reaction outside the six kinds", async () => {
    const { host, code } = await openRoom();
    const sender = await joinRoom(host, code, "Sender");

    sender.send({ type: "stream.react", reaction: 6 });
    sender.send({ type: "stream.react", reaction: -1 });
    sender.send({ type: "stream.react", reaction: 1.5 });
    sender.send({ type: "stream.react", reaction: "1" });
    sender.send({ type: "stream.react", reaction: 3 });
    const reaction = await host.next("stream.reaction");

    expect(reaction.reaction).toBe(3);
  });

  it("drops reactions beyond the reaction budget without spending the control budget", async () => {
    const { host, code } = await openRoom();
    const sender = await joinRoom(host, code, "Sender");
    host.inbox.length = 0;

    for (let index = 0; index < 20; index++) {
      sender.send({ type: "stream.react", reaction: index % 6 });
    }

    for (let index = 1; index < VIEWER_RATE.capacity; index++) {
      sender.send({ type: "stream.control", action: "pause" });
      await host.next("stream.controlRequest");
    }

    const forwarded = host.inbox.filter((message) => message.type === "stream.reaction").length;
    expect(forwarded).toBeGreaterThanOrEqual(REACTION_RATE.capacity);
    expect(forwarded).toBeLessThanOrEqual(REACTION_RATE.capacity + 1);
    expect(sender.closeCode).toBeNull();
  });

  it("closes a member that keeps flooding reactions past the drop limit", async () => {
    const { host, code } = await openRoom();
    const sender = await joinRoom(host, code, "Sender");

    for (let index = 0; index < REACTION_RATE.capacity + DROP_LIMIT + 2; index++) {
      sender.send({ type: "stream.react", reaction: 0 });
    }

    expect(await sender.closed()).toBe(CloseCode.rate);
    expect((await host.next("stream.left")).userId).toBe("Sender@伊弗利特");
  });
});

describe("queue suggestions", () => {
  it("passes a viewer's suggestion to the host and the host's answer back to its author only", async () => {
    const { host, code } = await openRoom();
    const author = await joinRoom(host, code, "Author");
    const other = await joinRoom(host, code, "Other");

    author.send({ type: "stream.queueSuggest", suggestionId: "s-1", url: "https://www.youtube.com/watch?v=abc" });
    const suggestion = await host.next("stream.queueSuggestion");
    host.send({ type: "stream.queueApprove", suggestionId: "s-1" });
    const accepted = await author.next("stream.queueSuggestionResult");
    author.send({ type: "stream.queueSuggest", suggestionId: "s-2", url: "https://video.test/b" });
    await host.next("stream.queueSuggestion");
    host.send({ type: "stream.queueApprove", suggestionId: "s-1" });
    host.send({ type: "stream.queueDeny", suggestionId: "s-2" });
    const denied = await author.next("stream.queueSuggestionResult");
    other.send({ type: "stream.control", action: "pause" });
    await host.next("stream.controlRequest");

    expect(suggestion).toMatchObject({ suggestionId: "s-1", url: "https://www.youtube.com/watch?v=abc", userId: "Author@伊弗利特" });
    expect(suggestion.from).toMatchObject({ userId: "Author@伊弗利特", displayName: "Author" });
    expect(accepted).toMatchObject({ suggestionId: "s-1", reason: "accepted" });
    expect(denied).toMatchObject({ suggestionId: "s-2", reason: "denied" });
    expect(author.inbox.some((message) => message.type === "stream.queueSuggestionResult")).toBe(false);
    expect(other.inbox.some((message) => message.type === "stream.queueSuggestionResult")).toBe(false);
  });

  it("drops a suggestion that is not a web link or carries a malformed id", async () => {
    const { host, code } = await openRoom();
    const author = await joinRoom(host, code, "Author");

    author.send({ type: "stream.queueSuggest", suggestionId: "s-1", url: "file:///C:/movie.mp4" });
    author.send({ type: "stream.queueSuggest", suggestionId: "s-2", url: "javascript:alert(1)" });
    author.send({ type: "stream.queueSuggest", suggestionId: "bad id", url: "https://video.test/b" });
    author.send({ type: "stream.queueSuggest", suggestionId: "s-4", url: "https://video.test/c" });
    const suggestion = await host.next("stream.queueSuggestion");

    expect(suggestion.suggestionId).toBe("s-4");
  });
});

describe("zero idle cost with every party feature", () => {
  it("writes no storage rows while a full room of the longest names is approved, suggests, reacts and hands over", async () => {
    const world = "界".repeat(32);
    const who = (name: string) => `name=${encodeURIComponent(name)}&world=${encodeURIComponent(world)}`;
    const hostName = "主".repeat(32);
    const guestNames = Array.from({ length: MAX_MEMBERS - 1 }, (_, index) => `${"客".repeat(31)}${index}`);
    const { host, code } = await openRoom(hostName, true, who(hostName));
    const guests: Peer[] = [];
    for (const name of guestNames) {
      const { guest } = await askToJoin(host, code, name, who(name));
      await approve(host, guest, `${name}@${world}`);
      guests.push(guest);
    }

    for (let index = 0; index < MAX_PENDING_SUGGESTIONS; index++) {
      guests[0].send({ type: "stream.queueSuggest", suggestionId: crypto.randomUUID(), url: "https://video.test/b" });
      await host.next("stream.queueSuggestion");
    }

    const members = guestNames.map((name) => ({ userId: `${name}@${world}`, flags: 3 }));
    const heir = members[MAX_MEMBERS - 2].userId;
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 8, paused: false, members });
    const echo = await host.next("stream.state");
    host.send({ type: "stream.transfer", userId: heir });
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 9, paused: false, members });
    const changed = await guests[MAX_MEMBERS - 2].next("stream.hostChanged");
    guests[0].send({ type: "stream.react", reaction: 4 });
    const reaction = await host.next("stream.reaction");
    const rows = await runInDurableObject(slotStub(code), async (_instance, state) => (await state.storage.list()).size);

    expect(echo.members).toHaveLength(members.length);
    expect(echo.members).toEqual(expect.arrayContaining(members.map((member) => ({ ...member, flags: 7 }))));
    expect(changed.hostId).toBe(heir);
    expect(reaction.reaction).toBe(4);
    expect(rows).toBe(0);
  });
});

function preferredSlotOf(id: string, roomCount: number): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < id.length; index++) {
    hash = Math.imul(hash ^ id.charCodeAt(index), 0x01000193) >>> 0;
  }

  return hash % roomCount;
}

async function endIdleRoom(code: string): Promise<void> {
  const stub = slotStub(code);
  await runInDurableObject(stub, (_instance, state) => {
    for (const socket of state.getWebSockets()) {
      const member = socket.deserializeAttachment() as { room: { aloneSince: number } | null };
      if (member.room !== null) {
        member.room.aloneSince = 0;
        socket.serializeAttachment(member);
      }
    }
  });
  await runDurableObjectAlarm(stub);
}

describe("abuse limits", () => {
  it("keeps one character to one room per kind: a new create replaces the room it already hosts, wherever it is", async () => {
    const home = preferredSlotOf("A@伊弗利特", 3);
    let squatterName = "";
    for (let index = 0; squatterName.length === 0; index++) {
      if (preferredSlotOf(`X${index}@伊弗利特`, 3) === home) {
        squatterName = `X${index}`;
      }
    }

    const squatter = await openRoom(squatterName);
    const first = await openRoom("A");
    await endIdleRoom(squatter.code);
    const second = await openRoom("A");

    expect(squatter.code[0]).toBe(CODE_ALPHABET[home]);
    expect(first.code[0]).not.toBe(CODE_ALPHABET[home]);
    expect(second.code[0]).toBe(first.code[0]);
    expect((await first.host.next("stream.ended")).reason).toBe("ended");
  });

  it("caps the rooms one address hosts per kind with an address_rooms refusal, leaving other addresses and kinds free", async () => {
    const overrides = { MAX_ROOMS_PARTY: "8", MAX_ROOMS_PER_ADDRESS: "2" };
    const outcomes = [
      await upgradeOutcome(`create=1&${identity("A")}`, overrides, "198.51.100.7"),
      await upgradeOutcome(`create=1&${identity("B")}`, overrides, "198.51.100.7"),
      await upgradeOutcome(`create=1&${identity("C")}`, overrides, "198.51.100.7"),
    ];
    const otherAddress = await upgradeOutcome(`create=1&${identity("D")}`, overrides, "198.51.100.8");
    const otherKind = await upgradeOutcome(`kind=jam&create=1&${identity("C")}`, overrides, "198.51.100.7");
    const request = realtimeRequest(`create=1&${identity("E")}`);
    request.headers.set("CF-Connecting-IP", "198.51.100.7");
    const { message, closeCode } = await refusedWith(await worker.fetch(request, { ...env, ...overrides }));

    expect(outcomes).toEqual(["open", "open", "address_rooms"]);
    expect(otherAddress).toBe("open");
    expect(otherKind).toBe("open");
    expect(message).toEqual({ type: "relay.refused", reason: "address_rooms", maxRooms: 2 });
    expect(closeCode).toBe(RefusalClose.address_rooms);
  });

  it("holds the per-address cap against a burst of simultaneous creates under made-up names", async () => {
    const overrides = { MAX_ROOMS_PARTY: "8", MAX_ROOMS_PER_ADDRESS: "2" };
    const names = ["P1", "P2", "P3", "P4", "P5", "P6"];

    const outcomes = await Promise.all(names.map((name) => upgradeOutcome(`create=1&${identity(name)}`, overrides, "203.0.113.9")));

    expect(outcomes.filter((outcome) => outcome === "open")).toHaveLength(2);
    expect(outcomes.filter((outcome) => outcome === "address_rooms")).toHaveLength(4);
  });
});

describe("several invite codes", () => {
  async function sessionToken(invite: string, overrides: Partial<Env>): Promise<string> {
    const challenge = await worker.fetch(new Request(`${BASE}/auth/challenge`, {
      method: "POST",
      body: JSON.stringify({ name: "Host", world: "Tonberry", invite }),
    }), { ...env, ...overrides });
    const { challengeId } = await challenge.json<{ challengeId: string }>();
    const verify = await worker.fetch(new Request(`${BASE}/auth/verify`, { method: "POST", body: JSON.stringify({ challengeId }) }),
      { ...env, ...overrides });
    return (await verify.json<{ token: string }>()).token;
  }

  async function meStatus(token: string, overrides: Partial<Env>): Promise<number> {
    const response = await worker.fetch(new Request(`${BASE}/me`, { headers: { Authorization: `Bearer ${token}` } }), { ...env, ...overrides });
    return response.status;
  }

  it("accepts every listed code, and dropping one code voids it and the sessions issued under it only", async () => {
    const both = { INVITE_CODE: "alpha-code, beta-code" };
    const betaOnly = { INVITE_CODE: "beta-code" };
    const alphaToken = await sessionToken("alpha-code", both);
    const betaToken = await sessionToken("beta-code", both);
    const direct = await worker.fetch(realtimeRequest(`create=1&${identity("Host")}`, "beta-code"), { ...env, ...both });
    const stray = await worker.fetch(realtimeRequest(`create=1&${identity("Host")}`, "gamma-code"), { ...env, ...both });

    expect(direct.status).toBe(101);
    expect(stray.status).toBe(401);
    expect(await meStatus(alphaToken, both)).toBe(200);
    expect(await meStatus(betaToken, both)).toBe(200);
    expect(await meStatus(alphaToken, betaOnly)).toBe(401);
    expect(await meStatus(betaToken, betaOnly)).toBe(200);
    expect((await worker.fetch(realtimeRequest(`create=1&${identity("Late")}`, "alpha-code"), { ...env, ...betaOnly })).status).toBe(401);
  });
});
