import { abortAllDurableObjects, evictDurableObject, reset, runInDurableObject } from "cloudflare:test";
import { afterEach, describe, expect, it } from "vitest";
import type { JamRoom, JamRoomState } from "../src/jam";
import { CODE_ALPHABET, CloseCode, JAM_GUEST_PERMISSIONS } from "../src/protocol";
import { type Peer, connect, env, identity, realtimeRequest, self } from "./support";

const SONG_A = { videoId: "aaaaaaaaaaa", title: "Song A", author: "Artist", thumbnailUrl: "https://i.ytimg.com/vi/a/hq.jpg", durationSeconds: 200 };
const SONG_B = { videoId: "bbbbbbbbbbb", title: "Song B", author: "Artist", durationSeconds: 180 };
const SONG_C = { videoId: "ccccccccccc", title: "Song C" };

async function startJam(name = "Host", title = "Late night"): Promise<{ host: Peer; code: string; joined: Record<string, unknown> }> {
  const host = await connect(`kind=jam&create=1&${identity(name)}`);
  host.send({ type: "jam.start", title });
  const joined = await host.next("jam.joined");
  return { host, code: joined.code as string, joined };
}

async function joinJam(host: Peer, code: string, name: string): Promise<{ guest: Peer; joined: Record<string, unknown> }> {
  const guest = await connect(`kind=jam&code=${code}&${identity(name)}`);
  guest.send({ type: "jam.join", code });
  const joined = await guest.next("jam.joined");
  await host.next("jam.roster");
  return { guest, joined };
}

function jamStub(code: string): DurableObjectStub<JamRoom> {
  const slot = CODE_ALPHABET.indexOf(code[0]);
  return env.JAM_ROOMS.get(env.JAM_ROOMS.idFromName(`slot:${slot}`)) as DurableObjectStub<JamRoom>;
}

function storedRoom(code: string): Promise<JamRoomState | undefined> {
  return runInDurableObject(jamStub(code), (_instance, state) => state.storage.get<JamRoomState>("room"));
}

afterEach(async () => {
  await abortAllDurableObjects();
  await reset();
});

describe("jam", () => {
  it("answers the host's start with a snapshot naming the code, the host and open guest permissions", async () => {
    const { joined } = await startJam();

    expect(joined).toMatchObject({
      hostId: "Host@伊弗利特",
      title: "Late night",
      guestPermissions: JAM_GUEST_PERMISSIONS,
      approvalRequired: false,
      entries: [],
      queueVersion: 0,
      track: null,
      paused: true,
      jamMembers: [expect.objectContaining({ userId: "Host@伊弗利特", isHost: true })],
    });
    expect(joined.code).toMatch(/^[A-Z2-9]{6}$/);
    expect(typeof joined.jamId).toBe("string");
  });

  it("gives a joining guest the playing song, its position and the queue, and tells the host", async () => {
    const { host, code } = await startJam();
    host.send({ type: "jam.state", track: SONG_A, positionSeconds: 42, paused: false });
    await host.next("jam.state");
    host.send({ type: "jam.queueAdd", track: SONG_B, mode: "end" });
    await host.next("jam.queue");

    const guest = await connect(`kind=jam&code=${code}&${identity("Guest")}`);
    guest.send({ type: "jam.join", code });
    const joined = await guest.next("jam.joined");
    const roster = await host.next("jam.roster");

    expect(joined).toMatchObject({
      code,
      hostId: "Host@伊弗利特",
      track: expect.objectContaining({ videoId: SONG_A.videoId }),
      positionSeconds: 42,
      paused: false,
      entries: [expect.objectContaining({ entryId: 1, track: expect.objectContaining({ videoId: SONG_B.videoId }) })],
    });
    expect(typeof joined.stateAtUnixMs).toBe("number");
    expect(roster.jamMembers).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特", isHost: true }),
      expect.objectContaining({ userId: "Guest@伊弗利特", isHost: false, permissions: JAM_GUEST_PERMISSIONS }),
    ]);
  });

  it("forwards host state to guests and echoes it to the host with a server timestamp", async () => {
    const { host, code } = await startJam();
    const { guest } = await joinJam(host, code, "Guest");

    host.send({ type: "jam.state", track: SONG_A, positionSeconds: 5, paused: false });
    const forwarded = await guest.next("jam.state");
    const echo = await host.next("jam.state");

    expect(forwarded).toMatchObject({ hostId: "Host@伊弗利特", positionSeconds: 5, paused: false, stale: false });
    expect(forwarded.stateAtUnixMs).toBe(echo.stateAtUnixMs);
  });

  it("ignores state from a guest", async () => {
    const { host, code } = await startJam();
    const { guest } = await joinJam(host, code, "Guest");

    guest.send({ type: "jam.state", track: SONG_C, positionSeconds: 1, paused: false });
    host.send({ type: "jam.state", track: SONG_A, positionSeconds: 2, paused: false });
    const state = await guest.next("jam.state");

    expect(state.track).toMatchObject({ videoId: SONG_A.videoId });
    expect(guest.inbox.some((message) => message.type === "jam.state")).toBe(false);
  });

  it("queues a guest's song for everyone, puts play-next first, and pops an advanced entry", async () => {
    const { host, code } = await startJam();
    const { guest } = await joinJam(host, code, "Guest");

    guest.send({ type: "jam.queueAdd", track: SONG_B, mode: "end" });
    await host.next("jam.queue");
    await guest.next("jam.queue");
    host.send({ type: "jam.queueAdd", track: SONG_C, mode: "next" });
    const both = await guest.next("jam.queue");
    await host.next("jam.queue");
    host.send({ type: "jam.queueAdvance", entryId: 2 });
    const popped = await guest.next("jam.queue");

    expect(both.entries).toEqual([
      expect.objectContaining({ entryId: 2, addedByUserId: "Host@伊弗利特" }),
      expect.objectContaining({ entryId: 1, addedByUserId: "Guest@伊弗利特", addedByName: "Guest" }),
    ]);
    expect(popped.entries).toEqual([expect.objectContaining({ entryId: 1 })]);
    expect(popped.queueVersion).toBe((both.queueVersion as number) + 1);
  });

  it("answers an advance for an entry that is already gone with the current queue", async () => {
    const { host } = await startJam();

    host.send({ type: "jam.queueAdvance", entryId: 99 });

    expect(await host.next("jam.queue")).toMatchObject({ entries: [], queueVersion: 0 });
  });

  it("lets a guest remove only their own entry while the host removes any", async () => {
    const { host, code } = await startJam();
    const { guest } = await joinJam(host, code, "Guest");
    host.send({ type: "jam.queueAdd", track: SONG_B, mode: "end" });
    await guest.next("jam.queue");
    guest.send({ type: "jam.queueAdd", track: SONG_C, mode: "end" });
    await guest.next("jam.queue");
    await host.next("jam.queue");
    await host.next("jam.queue");

    guest.send({ type: "jam.queueRemove", entryId: 1 });
    guest.send({ type: "jam.queueRemove", entryId: 2 });
    const afterGuest = await host.next("jam.queue");
    await guest.next("jam.queue");
    host.send({ type: "jam.queueRemove", entryId: 1 });
    const afterHost = await guest.next("jam.queue");

    expect(afterGuest.entries).toEqual([expect.objectContaining({ entryId: 1 })]);
    expect(afterHost.entries).toEqual([]);
  });

  it("moves an entry to a clamped index and bumps the queue version", async () => {
    const { host } = await startJam();
    for (const track of [SONG_A, SONG_B, SONG_C]) {
      host.send({ type: "jam.queueAdd", track, mode: "end" });
      await host.next("jam.queue");
    }

    host.send({ type: "jam.queueMove", entryId: 1, toIndex: 99 });
    const moved = await host.next("jam.queue");

    expect(moved.entries).toEqual([
      expect.objectContaining({ entryId: 2 }),
      expect.objectContaining({ entryId: 3 }),
      expect.objectContaining({ entryId: 1 }),
    ]);
    expect(moved.queueVersion).toBe(4);
  });

  it("refuses a track without a usable video id and drops a thumbnail from an unknown host", async () => {
    const { host } = await startJam();

    host.send({ type: "jam.queueAdd", track: { videoId: "../../etc", title: "Bad" }, mode: "end" });
    host.send({ type: "jam.queueAdd", track: { ...SONG_B, thumbnailUrl: "https://tracker.example/pixel.png" }, mode: "end" });
    const queue = await host.next("jam.queue");

    expect(queue.entries).toEqual([expect.objectContaining({ track: expect.objectContaining({ videoId: SONG_B.videoId, thumbnailUrl: null }) })]);
  });

  it("forwards a guest's playback control to the host and ignores unknown actions", async () => {
    const { host, code } = await startJam();
    const { guest } = await joinJam(host, code, "Guest");

    guest.send({ type: "jam.control", action: "launch" });
    guest.send({ type: "jam.control", action: "seek", positionSeconds: 30 });
    const request = await host.next("jam.controlRequest");

    expect(request).toMatchObject({ userId: "Guest@伊弗利特", action: "seek", positionSeconds: 30 });
    expect(host.inbox.some((message) => message.type === "jam.controlRequest")).toBe(false);
  });

  it("hands the host role over and lets only the new host publish", async () => {
    const { host, code } = await startJam();
    const { guest } = await joinJam(host, code, "Guest");

    host.send({ type: "jam.transfer", userId: "Guest@伊弗利特" });
    const changed = await guest.next("jam.hostChanged");
    await host.next("jam.hostChanged");
    host.send({ type: "jam.state", track: SONG_C, positionSeconds: 1, paused: false });
    guest.send({ type: "jam.state", track: SONG_A, positionSeconds: 9, paused: false });
    const state = await host.next("jam.state");

    expect(changed.hostId).toBe("Guest@伊弗利特");
    expect(changed.jamMembers).toEqual([
      expect.objectContaining({ userId: "Guest@伊弗利特", isHost: true }),
      expect.objectContaining({ userId: "Host@伊弗利特", isHost: false }),
    ]);
    expect(state).toMatchObject({ hostId: "Guest@伊弗利特", positionSeconds: 9 });
  });

  it("ends the Jam for guests when the host leaves and leaves no storage or alarm behind", async () => {
    const { host, code } = await startJam();
    const { guest } = await joinJam(host, code, "Guest");
    host.send({ type: "jam.queueAdd", track: SONG_B, mode: "end" });
    await guest.next("jam.queue");

    host.send({ type: "jam.end" });
    const ended = await guest.next("jam.ended");
    const after = await runInDurableObject(jamStub(code), async (_instance, state) => ({
      rows: (await state.storage.list()).size,
      alarm: await state.storage.getAlarm(),
    }));

    expect(ended.reason).toBe("host_left");
    expect(await guest.closed()).toBe(CloseCode.ended);
    expect(after).toEqual({ rows: 0, alarm: null });
  });

  it("stores playback only when it changes, not on a heartbeat that confirms the expected position", async () => {
    const { host, code } = await startJam();
    host.send({ type: "jam.state", track: SONG_A, positionSeconds: 10, paused: true });
    await host.next("jam.state");
    host.send({ type: "jam.state", track: SONG_A, positionSeconds: 10.5, paused: true });
    await host.next("jam.state");
    const afterHeartbeat = await storedRoom(code);
    host.send({ type: "jam.state", track: SONG_B, positionSeconds: 0, paused: false });
    await host.next("jam.state");
    const afterChange = await storedRoom(code);

    expect(afterHeartbeat?.play).toMatchObject({ positionSeconds: 10, paused: true });
    expect(afterChange?.play).toMatchObject({ track: expect.objectContaining({ videoId: SONG_B.videoId }), paused: false });
  });

  it("keeps the queue and the playing song across hibernation", async () => {
    const { host, code } = await startJam();
    host.send({ type: "jam.state", track: SONG_A, positionSeconds: 3, paused: false });
    await host.next("jam.state");
    host.send({ type: "jam.queueAdd", track: SONG_B, mode: "end" });
    await host.next("jam.queue");

    await evictDurableObject(jamStub(code));
    const { joined } = await joinJam(host, code, "Guest");

    expect(joined).toMatchObject({
      title: "Late night",
      track: expect.objectContaining({ videoId: SONG_A.videoId }),
      entries: [expect.objectContaining({ track: expect.objectContaining({ videoId: SONG_B.videoId }) })],
    });
  });

  it("keeps Jam and watch party rooms apart and rejects an unknown kind", async () => {
    const { code } = await startJam();

    const asParty = await self.fetch(realtimeRequest(`code=${code}&${identity("Guest")}`));
    const unknown = await self.fetch(realtimeRequest(`kind=radio&create=1&${identity("Guest")}`));

    expect(asParty.status).toBe(404);
    expect(unknown.status).toBe(400);
  });
});
