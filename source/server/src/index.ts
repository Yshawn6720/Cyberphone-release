import { bearerOf, inviteCodes, matchingInvite, parseIdentity, signToken, verifyAnyToken, type Identity } from "./auth";
import { ANNOUNCEMENTS, CALENDAR, NEWS_TAIWAN, RADIO, type StaticDocument } from "./content";
import {
  CLAIM_PATH,
  NEARBY_PATH,
  RELEASE_PATH,
  roomsOf,
  slotStub,
  type NearbyAnswer,
  type NearbyQuery,
  type SlotClaim,
  type SlotDecision,
  type SlotRelease,
} from "./registry";
import { JOIN_HEADER, type JoinTicket, type RoomListing } from "./room";
import {
  RATE_RETRY_AFTER_SECONDS,
  REFUSED_SIGNAL,
  RefusalClose,
  callsEnabled,
  locationId,
  maxRooms,
  maxRoomsPerAddress,
  normalizeCode,
  randomCode,
  roomKind,
  slotOfCode,
  type RefusalReason,
  type RoomKind,
} from "./protocol";

export { CallRoom } from "./call";
export { GameRoom } from "./game";
export { JamRoom } from "./jam";
export { PartyRoom } from "./room";
export { SlotRegistry } from "./registry";

export interface Env {
  ROOMS: DurableObjectNamespace;
  JAM_ROOMS: DurableObjectNamespace;
  GAME_ROOMS: DurableObjectNamespace;
  CALL_ROOMS: DurableObjectNamespace;
  SLOT_REGISTRY: DurableObjectNamespace;
  CONNECT_LIMITER: RateLimit;
  ADDRESS_LIMITER: RateLimit;
  TOTAL_LIMITER: RateLimit;
  INVITE_CODE?: string;
  MAX_ROOMS_PER_ADDRESS?: string;
  MAX_ROOMS?: string;
  MAX_ROOMS_PARTY?: string;
  MAX_ROOMS_JAM?: string;
  MAX_ROOMS_GAME?: string;
  MAX_ROOMS_CALL?: string;
  CALLS_ENABLED?: string;
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function userOf(identity: Identity): Record<string, unknown> {
  return {
    id: identity.id,
    name: identity.name,
    world: identity.world,
    displayName: identity.name,
    handle: "",
    bio: "",
    followers: 0,
    following: 0,
    posts: 0,
    isFollowing: false,
    isMe: true,
    avatarUrl: null,
    grams: 0,
  };
}

async function readBody(request: Request): Promise<Record<string, unknown> | null> {
  const text = await request.text();
  if (text.length > 4096) {
    return null;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    if (!(error instanceof SyntaxError)) {
      throw error;
    }

    // quality-allow: a non-JSON body is answered with 400 by every caller of readBody.
    return null;
  }

  return typeof parsed === "object" && parsed !== null ? parsed as Record<string, unknown> : null;
}

function addressOf(request: Request): string {
  return request.headers.get("CF-Connecting-IP") ?? "unknown";
}

async function underLimit(request: Request, env: Env, identity: Identity): Promise<boolean> {
  return (await env.CONNECT_LIMITER.limit({ key: identity.id })).success
    && (await env.ADDRESS_LIMITER.limit({ key: addressOf(request) })).success
    && (await env.TOTAL_LIMITER.limit({ key: "relay" })).success;
}

async function challenge(request: Request, invites: readonly string[]): Promise<Response> {
  const body = await readBody(request);
  if (body === null || typeof body.invite !== "string") {
    return json({ error: "bad_request" }, 400);
  }

  const invite = await matchingInvite(body.invite, invites);
  if (invite === null) {
    return json({ error: "invite" }, 401);
  }

  const identity = parseIdentity(body.name, body.world);
  if (identity === null) {
    return json({ error: "identity" }, 400);
  }

  const challengeId = await signToken(invite, "challenge", identity, Date.now());
  return json({ challengeId, code: "", instructions: "" });
}

async function verify(request: Request, invites: readonly string[]): Promise<Response> {
  const body = await readBody(request);
  if (body === null || typeof body.challengeId !== "string") {
    return json({ error: "bad_request" }, 400);
  }

  const now = Date.now();
  const verified = await verifyAnyToken(invites, body.challengeId, "challenge", now);
  if (verified === null) {
    return json({ ok: false, reason: "expired" });
  }

  const token = await signToken(verified.invite, "session", verified.identity, now);
  return json({ ok: true, token, user: userOf(verified.identity) });
}

async function me(request: Request, invites: readonly string[]): Promise<Response> {
  const bearer = bearerOf(request);
  const verified = bearer === null ? null : await verifyAnyToken(invites, bearer, "session", Date.now());
  return verified === null ? json({ error: "unauthorized" }, 401) : json(userOf(verified.identity));
}

async function content(request: Request, invites: readonly string[], document: StaticDocument): Promise<Response> {
  const bearer = bearerOf(request);
  if (bearer === null || await matchingInvite(bearer, invites) === null) {
    return json({ error: "unauthorized" }, 401);
  }

  return document.respond(request);
}

async function realtimeIdentity(request: Request, url: URL, invites: readonly string[]): Promise<Identity | null> {
  const bearer = bearerOf(request);
  if (bearer === null) {
    return null;
  }

  const name = url.searchParams.get("name");
  const world = url.searchParams.get("world");
  if (name !== null || world !== null) {
    return await matchingInvite(bearer, invites) !== null ? parseIdentity(name, world) : null;
  }

  return (await verifyAnyToken(invites, bearer, "session", Date.now()))?.identity ?? null;
}

function forward(request: Request, rooms: DurableObjectNamespace, slot: number, ticket: JoinTicket): Promise<Response> {
  const headers = new Headers(request.headers);
  headers.set(JOIN_HEADER, encodeURIComponent(JSON.stringify(ticket)));
  return slotStub(rooms, slot).fetch(new Request(request.url, { method: "GET", headers }));
}

function registryOf(env: Env, kind: RoomKind): DurableObjectStub {
  return env.SLOT_REGISTRY.get(env.SLOT_REGISTRY.idFromName(kind));
}

async function claimSlot(env: Env, claim: SlotClaim): Promise<SlotDecision> {
  const body = JSON.stringify(claim);
  const response = await registryOf(env, claim.kind).fetch(`https://relay.internal${CLAIM_PATH}`, { method: "POST", body });
  return await response.json() as SlotDecision;
}

async function releaseSlot(env: Env, kind: RoomKind, release: SlotRelease): Promise<void> {
  const body = JSON.stringify(release);
  await registryOf(env, kind).fetch(`https://relay.internal${RELEASE_PATH}`, { method: "POST", body });
}

function preferredSlot(id: string, roomCount: number): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < id.length; index++) {
    hash = Math.imul(hash ^ id.charCodeAt(index), 0x01000193) >>> 0;
  }

  return hash % roomCount;
}

function maxRoomsFor(env: Env, kind: RoomKind): number {
  switch (kind) {
    case "jam":
      return maxRooms(env.MAX_ROOMS_JAM, env.MAX_ROOMS);
    case "game":
      return maxRooms(env.MAX_ROOMS_GAME, env.MAX_ROOMS);
    case "call":
      return maxRooms(env.MAX_ROOMS_CALL, env.MAX_ROOMS);
    case "party":
      return maxRooms(env.MAX_ROOMS_PARTY, env.MAX_ROOMS);
  }
}

function refuse(reason: RefusalReason, details: Record<string, number> = {}): Response {
  const [client, server] = Object.values(new WebSocketPair());
  server.accept();
  server.send(JSON.stringify({ type: REFUSED_SIGNAL, reason, ...details }));
  server.close(RefusalClose[reason], reason);
  return new Response(null, { status: 101, webSocket: client });
}

function nearbyEntry(kind: RoomKind, listing: RoomListing): Record<string, unknown> {
  return kind === "party"
    ? {
      hostId: "",
      name: listing.hostName,
      world: "",
      displayName: listing.hostName,
      handle: "",
      avatarUrl: null,
      code: listing.code,
      memberCount: listing.memberCount,
    }
    : {
      jamId: listing.jamId,
      code: listing.code,
      title: listing.title,
      hostId: "",
      hostDisplayName: listing.hostName,
      hostHandle: "",
      hostAvatarUrl: null,
      memberCount: listing.memberCount,
      track: listing.track,
    };
}

// Lists the discoverable parties or Jams whose host stands in the asker's zone. Only the host's character name and
// the room's nearby code leave the relay; joining with that code always waits for the host's approval.
async function nearbyRooms(request: Request, env: Env, url: URL, invites: readonly string[]): Promise<Response> {
  const bearer = bearerOf(request);
  if (bearer === null || await matchingInvite(bearer, invites) === null) {
    return json({ error: "unauthorized" }, 401);
  }

  const kind = roomKind(url.searchParams.get("kind"));
  const territoryId = locationId(url.searchParams.get("territory"));
  const worldId = locationId(url.searchParams.get("world"));
  if ((kind !== "party" && kind !== "jam") || territoryId === null || worldId === null) {
    return json({ error: "bad_request" }, 400);
  }

  const query: NearbyQuery = { kind, territoryId, worldId, address: addressOf(request), roomCount: maxRoomsFor(env, kind) };
  const response = await registryOf(env, kind).fetch(`https://relay.internal${NEARBY_PATH}`, {
    method: "POST",
    body: JSON.stringify(query),
  });
  const answer = await response.json() as NearbyAnswer;
  if ("retryAfterSeconds" in answer) {
    return new Response(JSON.stringify({ error: "rate_limited", retryAfterSeconds: answer.retryAfterSeconds }), {
      status: 429,
      headers: { "Content-Type": "application/json", "Retry-After": `${answer.retryAfterSeconds}` },
    });
  }

  const entries = answer.listings.map((listing) => nearbyEntry(kind, listing));
  return json(kind === "party"
    ? { type: "stream.nearby.roster", nearbyStreams: entries }
    : { type: "jam.nearby.roster", nearbyJams: entries });
}

async function realtime(request: Request, env: Env, url: URL, invites: readonly string[]): Promise<Response> {
  if (request.headers.get("Upgrade") !== "websocket") {
    return json({ error: "upgrade_required" }, 426);
  }

  const identity = await realtimeIdentity(request, url, invites);
  if (identity === null) {
    return json({ error: "unauthorized" }, 401);
  }

  const kind = roomKind(url.searchParams.get("kind"));
  if (kind === "call" && !callsEnabled(env.CALLS_ENABLED)) {
    return refuse("calls_disabled");
  }

  const nearby = normalizeCode(url.searchParams.get("nearby"));
  const code = nearby ?? normalizeCode(url.searchParams.get("code"));
  const create = url.searchParams.get("create") === "1";
  if (kind === null || (code === null && !create) || (nearby !== null && kind !== "party" && kind !== "jam")) {
    return json({ error: "bad_request" }, 400);
  }

  const rooms = roomsOf(env, kind);
  const roomCount = maxRoomsFor(env, kind);
  if (!await underLimit(request, env, identity)) {
    return refuse("rate_limited", { retryAfterSeconds: RATE_RETRY_AFTER_SECONDS });
  }

  const address = addressOf(request);
  if (code !== null) {
    const slot = slotOfCode(code, roomCount);
    if (slot === null) {
      return json({ error: "room_not_found" }, 404);
    }

    return forward(request, rooms, slot, { role: "viewer", code, address, nearby: nearby !== null, ...identity });
  }

  const perAddress = maxRoomsPerAddress(env.MAX_ROOMS_PER_ADDRESS);
  const start = preferredSlot(identity.id, roomCount);
  const decision = await claimSlot(env, { kind, id: identity.id, address, start, roomCount, perAddress });
  if ("refused" in decision) {
    return decision.refused === "rooms_full"
      ? refuse("rooms_full", { maxRooms: roomCount })
      : refuse("address_rooms", { maxRooms: perAddress });
  }

  const ticket: JoinTicket = { role: "host", code: randomCode(decision.slot), address, ...identity };
  let response: Response;
  try {
    response = await forward(request, rooms, decision.slot, ticket);
  } finally {
    await releaseSlot(env, kind, { id: identity.id, slot: decision.slot });
  }

  return response.status === 409 ? refuse("rooms_full", { maxRooms: roomCount }) : response;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const invites = inviteCodes(env.INVITE_CODE);
    if (invites.length === 0) {
      return json({ error: "not_configured" }, 500);
    }

    const url = new URL(request.url);
    const route = `${request.method} ${url.pathname}`;
    switch (route) {
      case "POST /auth/challenge":
        return challenge(request, invites);
      case "POST /auth/verify":
        return verify(request, invites);
      case "GET /me":
        return me(request, invites);
      case "GET /rt":
        return realtime(request, env, url, invites);
      case "GET /rooms/nearby":
        return nearbyRooms(request, env, url, invites);
      case "GET /content/announcements":
        return content(request, invites, ANNOUNCEMENTS);
      case "GET /content/calendar":
        return content(request, invites, CALENDAR);
      case "GET /content/news-tw":
        return content(request, invites, NEWS_TAIWAN);
      case "GET /content/radio":
        return content(request, invites, RADIO);
      default:
        return json({ error: "not_found" }, 404);
    }
  },
} satisfies ExportedHandler<Env>;
