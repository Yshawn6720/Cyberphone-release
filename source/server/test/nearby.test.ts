import { abortAllDurableObjects, reset } from "cloudflare:test";
import { afterEach, describe, expect, it } from "vitest";
import { CloseCode, NEARBY_RATE } from "../src/protocol";
import { BASE, INVITE, type Message, type Peer, connect, identity, realtimeRequest, self } from "./support";

const URL_A = "https://video.test/a";
const TERRITORY = 132;
const WORLD = 73;

function nearbyRequest(query: string, bearer = INVITE): Request {
  return new Request(`${BASE}/rooms/nearby?${query}`, { headers: { Authorization: `Bearer ${bearer}` } });
}

async function nearby(kind: string, territory = TERRITORY, world = WORLD): Promise<Message> {
  const response = await self.fetch(nearbyRequest(`kind=${kind}&territory=${territory}&world=${world}`));
  expect(response.status).toBe(200);
  return await response.json() as Message;
}

async function openDiscoverableParty({ approvalRequired = false, legacy = false } = {}): Promise<{ host: Peer; code: string }> {
  const host = await connect(`create=1&${identity("Host")}`);
  const nearbyFollowsApproval = legacy ? {} : { nearbyFollowsApproval: true };
  host.send({ type: "stream.state", url: URL_A, positionSeconds: 1, paused: false, discoverable: true, nearbyOptIn: true, territoryId: TERRITORY, worldId: WORLD, approvalRequired, ...nearbyFollowsApproval });
  const echo = await host.next("stream.state");
  return { host, code: echo.code as string };
}

async function knockFromList(name = "Guest"): Promise<Peer> {
  const entry = ((await nearby("party")).nearbyStreams as Message[])[0];
  const guest = await connect(`nearby=${entry.code}&${identity(name)}`);
  guest.send({ type: "stream.join", code: entry.code, features: 1 });
  return guest;
}

async function startDiscoverableJam(name = "Host"): Promise<{ host: Peer; code: string }> {
  const host = await connect(`kind=jam&create=1&${identity(name)}`);
  host.send({ type: "jam.start", title: "Late night" });
  const joined = await host.next("jam.joined");
  host.send({ type: "jam.nearby", territoryId: TERRITORY, worldId: WORLD });
  host.send({ type: "jam.settings", discoverable: true });
  await host.next("jam.roster");
  return { host, code: joined.code as string };
}

afterEach(async () => {
  await abortAllDurableObjects();
  await reset();
});

describe("nearby parties", () => {
  it("lists a discoverable party in the host's zone under a nearby code, never the room code", async () => {
    const { code } = await openDiscoverableParty();

    const listed = await nearby("party");
    const otherZone = await nearby("party", TERRITORY + 1);
    const otherWorld = await nearby("party", TERRITORY, WORLD + 1);
    const entry = (listed.nearbyStreams as Message[])[0];

    expect(listed).toMatchObject({
      type: "stream.nearby.roster",
      nearbyStreams: [{ hostId: "", name: "Host", world: "", displayName: "Host", handle: "", avatarUrl: null, memberCount: 1 }],
    });
    expect(entry.code).toMatch(/^[A-Z2-9]{6}$/);
    expect(entry.code).not.toBe(code);
    expect((entry.code as string)[0]).toBe(code[0]);
    expect(JSON.stringify(listed)).not.toContain(code);
    expect(JSON.stringify(listed)).not.toContain("伊弗利特");
    expect(otherZone.nearbyStreams).toEqual([]);
    expect(otherWorld.nearbyStreams).toEqual([]);
  });

  it("keeps a private party out of the list and drops a party once the host turns discovery off", async () => {
    const privateHost = await connect(`create=1&${identity("Private")}`);
    privateHost.send({ type: "stream.state", url: URL_A, positionSeconds: 1, paused: false, territoryId: TERRITORY, worldId: WORLD });
    await privateHost.next("stream.state");
    const { host } = await openDiscoverableParty();
    const before = await nearby("party");

    host.send({ type: "stream.state", url: URL_A, positionSeconds: 2, paused: false, discoverable: false });
    await host.next("stream.state");
    const after = await nearby("party");

    expect(before.nearbyStreams).toEqual([expect.objectContaining({ name: "Host" })]);
    expect(after.nearbyStreams).toEqual([]);
  });

  it("keeps a host that sends discoverable without the nearby opt-in out of the list, as 1.3.4.0 does by default", async () => {
    const legacy = await connect(`create=1&${identity("Legacy")}`);
    legacy.send({ type: "stream.state", url: URL_A, positionSeconds: 1, paused: false, discoverable: true, territoryId: TERRITORY, worldId: WORLD });
    await legacy.next("stream.state");

    const listed = await nearby("party");

    expect(listed.nearbyStreams).toEqual([]);
  });

  it("lets a join from the list straight in when approval is off, then hands over the room code", async () => {
    const { host, code } = await openDiscoverableParty();

    const guest = await knockFromList();
    const roster = await host.next("stream.roster");
    host.send({ type: "stream.state", url: URL_A, positionSeconds: 3, paused: false });
    await host.next("stream.state");
    const joined = await guest.next("stream.joined");

    expect(roster.participants).toEqual([
      expect.objectContaining({ userId: "Host@伊弗利特" }),
      expect.objectContaining({ userId: "Guest@伊弗利特" }),
    ]);
    expect(joined.code).toBe(code);
    expect(guest.inbox.some((message) => message.type === "stream.joinPending")).toBe(false);
    expect(host.inbox.some((message) => message.type === "stream.joinRequest")).toBe(false);
  });

  it("sends a join from the list to the host for approval when approval is on, then hands over the room code", async () => {
    const { host, code } = await openDiscoverableParty({ approvalRequired: true });

    const guest = await knockFromList();
    const pending = await guest.next("stream.joinPending");
    const request = await host.next("stream.joinRequest");
    host.send({ type: "stream.approve", userId: "Guest@伊弗利特" });
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: URL_A, positionSeconds: 3, paused: false });
    await host.next("stream.state");
    const joined = await guest.next("stream.joined");

    expect(pending.hostId).toBe("Host@伊弗利特");
    expect(request.from).toMatchObject({ userId: "Guest@伊弗利特", displayName: "Guest" });
    expect(joined.code).toBe(code);
  });

  it("lets a join from the list that is waiting in once the host turns approval off", async () => {
    const { host } = await openDiscoverableParty({ approvalRequired: true });
    const guest = await knockFromList();
    await guest.next("stream.joinPending");
    await host.next("stream.joinRequest");

    host.send({ type: "stream.state", url: URL_A, positionSeconds: 4, paused: false, approvalRequired: false, nearbyFollowsApproval: true });
    const joined = await guest.next("stream.joined");

    expect(joined).toMatchObject({ approvalRequired: false, positionSeconds: 4 });
  });

  it("still holds a join from the list for approval under a host that does not say nearby follows approval", async () => {
    const { host } = await openDiscoverableParty({ legacy: true });

    const guest = await knockFromList();
    const pending = await guest.next("stream.joinPending");
    const request = await host.next("stream.joinRequest");

    expect(pending.hostId).toBe("Host@伊弗利特");
    expect(request.userId).toBe("Guest@伊弗利特");
  });

  it("keeps a join from the list waiting when such a host turns approval off, until the host approves it", async () => {
    const { host } = await openDiscoverableParty({ approvalRequired: true, legacy: true });
    const guest = await knockFromList();
    await guest.next("stream.joinPending");
    await host.next("stream.joinRequest");

    host.send({ type: "stream.state", url: URL_A, positionSeconds: 5, paused: false, approvalRequired: false });
    await host.next("stream.state");
    host.send({ type: "stream.approve", userId: "Guest@伊弗利特" });
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: URL_A, positionSeconds: 6, paused: false });
    const joined = await guest.next("stream.joined");

    expect(joined.positionSeconds).toBe(6);
  });

  it("holds a join from the list for approval again once the room passes to a host that does not say nearby follows approval", async () => {
    const { host } = await openDiscoverableParty();
    const first = await knockFromList("First");
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: URL_A, positionSeconds: 2, paused: false });
    await first.next("stream.joined");

    host.send({ type: "stream.transfer", userId: "First@伊弗利特" });
    await host.next("stream.roster");
    host.send({ type: "stream.state", url: URL_A, positionSeconds: 3, paused: false, nearbyFollowsApproval: true });
    await first.next("stream.hostChanged");
    const late = await knockFromList("Late");
    const pending = await late.next("stream.joinPending");
    const request = await first.next("stream.joinRequest");

    expect(pending.hostId).toBe("First@伊弗利特");
    expect(request.userId).toBe("Late@伊弗利特");
  });

  it("refuses a nearby code once discovery is off and never accepts the room code as a nearby code", async () => {
    const { host, code } = await openDiscoverableParty();
    const entry = ((await nearby("party")).nearbyStreams as Message[])[0];

    const roomCodeAsNearby = await self.fetch(realtimeRequest(`nearby=${code}&${identity("Guest")}`));
    host.send({ type: "stream.state", url: URL_A, positionSeconds: 2, paused: false, discoverable: false });
    await host.next("stream.state");
    const staleNearby = await self.fetch(realtimeRequest(`nearby=${entry.code}&${identity("Guest")}`));

    expect(roomCodeAsNearby.status).toBe(404);
    expect(staleNearby.status).toBe(404);
  });

  it("tells only the host which link a viewer could not play", async () => {
    const { host, code } = await openDiscoverableParty();
    const viewers: Peer[] = [];
    for (const name of ["Broken", "Other"]) {
      const viewer = await connect(`code=${code}&${identity(name)}`);
      viewer.send({ type: "stream.join", code, features: 1 });
      await host.next("stream.roster");
      host.send({ type: "stream.state", url: URL_A, positionSeconds: 2, paused: false });
      await host.next("stream.state");
      await viewer.next("stream.joined");
      viewers.push(viewer);
    }

    viewers[0].send({ type: "stream.playbackFailed", url: URL_A, reason: "x".repeat(500) });
    viewers[0].send({ type: "stream.playbackFailed", url: 42 });
    const failed = await host.next("stream.viewerFailed");

    expect(failed).toMatchObject({ hostId: "Host@伊弗利特", userId: "Broken@伊弗利特", url: URL_A });
    expect((failed.reason as string).length).toBe(200);
    expect(failed.from).toMatchObject({ userId: "Broken@伊弗利特" });
    expect(viewers[1].inbox.some((message) => message.type === "stream.viewerFailed")).toBe(false);
    expect(host.inbox.some((message) => message.type === "stream.viewerFailed")).toBe(false);
  });
});

describe("nearby Jams", () => {
  it("lists a discoverable Jam with its title and song, and holds a join from the list for the host", async () => {
    const { host, code } = await startDiscoverableJam();
    host.send({ type: "jam.state", track: { videoId: "aaaaaaaaaaa", title: "Song A" }, positionSeconds: 1, paused: false });
    await host.next("jam.state");

    const listed = await nearby("jam");
    const entry = (listed.nearbyJams as Message[])[0];
    const guest = await connect(`kind=jam&nearby=${entry.code}&${identity("Guest")}`);
    guest.send({ type: "jam.join", code: entry.code });
    const pending = await guest.next("jam.joinPending");
    const request = await host.next("jam.joinRequest");
    host.send({ type: "jam.approve", userId: "Guest@伊弗利特" });
    const joined = await guest.next("jam.joined");
    const roster = await host.next("jam.roster");

    expect(listed).toMatchObject({
      type: "jam.nearby.roster",
      nearbyJams: [{
        title: "Late night",
        hostId: "",
        hostDisplayName: "Host",
        hostHandle: "",
        hostAvatarUrl: null,
        memberCount: 1,
        track: expect.objectContaining({ videoId: "aaaaaaaaaaa" }),
      }],
    });
    expect(entry.code).not.toBe(code);
    expect(JSON.stringify(listed)).not.toContain(code);
    expect(pending.hostId).toBe("Host@伊弗利特");
    expect(request).toMatchObject({ userId: "Guest@伊弗利特", hostId: "Host@伊弗利特", from: { displayName: "Guest" } });
    expect(joined.code).toBe(code);
    expect(roster.jamMembers).toHaveLength(2);
  });

  it("tells a denied nearby guest and clears the host's request", async () => {
    const { host } = await startDiscoverableJam();
    const entry = ((await nearby("jam")).nearbyJams as Message[])[0];
    const guest = await connect(`kind=jam&nearby=${entry.code}&${identity("Guest")}`);
    guest.send({ type: "jam.join", code: entry.code });
    await host.next("jam.joinRequest");

    host.send({ type: "jam.deny", userId: "Guest@伊弗利特" });
    const declined = await guest.next("jam.declined");
    const cancelled = await host.next("jam.joinCancelled");

    expect(declined.reason).toBe("denied");
    expect(await guest.closed()).toBe(CloseCode.ended);
    expect(cancelled.userId).toBe("Guest@伊弗利特");
  });

  it("leaves a Jam out of the list until the host reports a zone and turns discovery on", async () => {
    const host = await connect(`kind=jam&create=1&${identity("Host")}`);
    host.send({ type: "jam.start", title: "" });
    await host.next("jam.joined");
    host.send({ type: "jam.nearby", territoryId: TERRITORY, worldId: WORLD });
    host.send({ type: "jam.settings", title: "" });
    await host.next("jam.roster");

    const hidden = await nearby("jam");
    const asParty = await nearby("party");

    expect(hidden.nearbyJams).toEqual([]);
    expect(asParty.nearbyStreams).toEqual([]);
  });
});

describe("nearby endpoint", () => {
  it("needs the invite, a party or Jam kind and a zone", async () => {
    const wrongInvite = await self.fetch(nearbyRequest(`kind=party&territory=${TERRITORY}&world=${WORLD}`, "wrong-invite"));
    const gameKind = await self.fetch(nearbyRequest(`kind=game&territory=${TERRITORY}&world=${WORLD}`));
    const noZone = await self.fetch(nearbyRequest("kind=jam&territory=0&world=73"));
    const gameJoin = await self.fetch(realtimeRequest(`kind=game&nearby=ABC234&${identity("Guest")}`));

    expect(wrongInvite.status).toBe(401);
    expect(gameKind.status).toBe(400);
    expect(noZone.status).toBe(400);
    expect(gameJoin.status).toBe(400);
  });

  it("answers a burst of lists from one address and then says when to ask again", async () => {
    for (let index = 0; index < NEARBY_RATE.capacity; index++) {
      await nearby("party");
    }

    const limited = await self.fetch(nearbyRequest(`kind=party&territory=${TERRITORY}&world=${WORLD}`));
    const body = await limited.json() as Message;

    expect(limited.status).toBe(429);
    expect(body.error).toBe("rate_limited");
    expect(body.retryAfterSeconds).toBeGreaterThan(0);
    expect(limited.headers.get("Retry-After")).toBe(`${body.retryAfterSeconds}`);
  });
});
