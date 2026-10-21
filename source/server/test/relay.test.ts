import { env as workerEnv, exports as workerExports } from "cloudflare:workers";
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
import { CODE_ALPHABET, CloseCode, DROP_LIMIT, HOST_RATE, MAX_MEMBERS, MAX_MESSAGE_BYTES, VIEWER_RATE } from "../src/protocol";

const env = workerEnv as unknown as Env;
const self = (workerExports as unknown as { default: { fetch(input: Request | string, init?: RequestInit): Promise<Response> } }).default;
const INVITE = "test-invite-code";
const BASE = "https://relay.test";

type Message = Record<string, unknown>;

class Peer {
  readonly inbox: Message[] = [];
  closeCode: number | null = null;
  private waiters: Array<() => void> = [];

  constructor(readonly socket: WebSocket) {
    socket.accept();
    socket.addEventListener("message", (event) => {
      this.inbox.push(JSON.parse(event.data as string) as Message);
      this.wake();
    });
    socket.addEventListener("close", (event) => {
      this.closeCode = event.code;
      this.wake();
    });
  }

  send(message: Message): void {
    this.socket.send(JSON.stringify(message));
  }

  async next(type: string): Promise<Message> {
    for (let attempt = 0; attempt < 200; attempt++) {
      const index = this.inbox.findIndex((message) => message.type === type);
      if (index >= 0) {
        return this.inbox.splice(index, 1)[0];
      }

      await this.wait();
    }

    throw new Error(`timed out waiting for ${type}; inbox: ${JSON.stringify(this.inbox)}`);
  }

  async closed(): Promise<number> {
    for (let attempt = 0; attempt < 200 && this.closeCode === null; attempt++) {
      await this.wait();
    }

    if (this.closeCode === null) {
      throw new Error("socket did not close");
    }

    return this.closeCode;
  }

  private wake(): void {
    const waiters = this.waiters;
    this.waiters = [];
    waiters.forEach((resolve) => resolve());
  }

  private wait(): Promise<void> {
    const { promise, resolve } = Promise.withResolvers<void>();
    this.waiters.push(resolve);
    // Socket events wake waiters directly; the short fallback only bounds a wait whose event already fired.
    setTimeout(resolve, 25);
    return promise;
  }
}

function realtimeRequest(query: string, bearer: string = INVITE): Request {
  return new Request(`${BASE}/rt?${query}`, {
    headers: { Upgrade: "websocket", Authorization: `Bearer ${bearer}` },
  });
}

async function connect(query: string, bearer: string = INVITE): Promise<Peer> {
  const response = await self.fetch(realtimeRequest(query, bearer));
  expect(response.status).toBe(101);
  if (response.webSocket === null) {
    throw new Error("missing webSocket");
  }

  return new Peer(response.webSocket);
}

function identity(name: string): string {
  return `name=${encodeURIComponent(name)}&world=${encodeURIComponent("伊弗利特")}`;
}

async function openRoom(name = "Host"): Promise<{ host: Peer; code: string }> {
  const host = await connect(`create=1&${identity(name)}`);
  host.send({ type: "stream.state", url: "https://video.test/a", positionSeconds: 1, paused: false, guestPermissions: 0 });
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

function slotStub(code: string): DurableObjectStub<PartyRoom> {
  const slot = CODE_ALPHABET.indexOf(code[0]);
  return env.ROOMS.get(env.ROOMS.idFromName(`slot:${slot}`)) as DurableObjectStub<PartyRoom>;
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
  it("refuses a room beyond MAX_ROOMS after probing every occupied slot", async () => {
    const rooms = [await openRoom("A"), await openRoom("B"), await openRoom("C")];

    const response = await self.fetch(realtimeRequest(`create=1&${identity("D")}`));

    expect(new Set(rooms.map((room) => room.code[0])).size).toBe(3);
    expect(response.status).toBe(503);
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
