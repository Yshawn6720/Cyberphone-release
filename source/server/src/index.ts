import { bearerOf, inviteMatches, parseIdentity, signToken, verifyToken, type Identity } from "./auth";
import { JOIN_HEADER, type JoinTicket } from "./room";
import { maxRooms, normalizeCode, randomCode, slotOfCode } from "./protocol";

export { PartyRoom } from "./room";

export interface Env {
  ROOMS: DurableObjectNamespace;
  CONNECT_LIMITER: RateLimit;
  INVITE_CODE?: string;
  MAX_ROOMS?: string;
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

async function underLimit(env: Env): Promise<boolean> {
  const outcome = await env.CONNECT_LIMITER.limit({ key: "relay" });
  return outcome.success;
}

async function challenge(request: Request, invite: string): Promise<Response> {
  const body = await readBody(request);
  if (body === null || typeof body.invite !== "string") {
    return json({ error: "bad_request" }, 400);
  }

  if (!await inviteMatches(body.invite, invite)) {
    return json({ error: "invite" }, 401);
  }

  const identity = parseIdentity(body.name, body.world);
  if (identity === null) {
    return json({ error: "identity" }, 400);
  }

  const challengeId = await signToken(invite, "challenge", identity, Date.now());
  return json({ challengeId, code: "", instructions: "" });
}

async function verify(request: Request, invite: string): Promise<Response> {
  const body = await readBody(request);
  if (body === null || typeof body.challengeId !== "string") {
    return json({ error: "bad_request" }, 400);
  }

  const now = Date.now();
  const identity = await verifyToken(invite, body.challengeId, "challenge", now);
  if (identity === null) {
    return json({ ok: false, reason: "expired" });
  }

  const token = await signToken(invite, "session", identity, now);
  return json({ ok: true, token, user: userOf(identity) });
}

async function me(request: Request, invite: string): Promise<Response> {
  const bearer = bearerOf(request);
  const identity = bearer === null ? null : await verifyToken(invite, bearer, "session", Date.now());
  return identity === null ? json({ error: "unauthorized" }, 401) : json(userOf(identity));
}

async function realtimeIdentity(request: Request, url: URL, invite: string): Promise<Identity | null> {
  const bearer = bearerOf(request);
  if (bearer === null) {
    return null;
  }

  const name = url.searchParams.get("name");
  const world = url.searchParams.get("world");
  if (name !== null || world !== null) {
    return await inviteMatches(bearer, invite) ? parseIdentity(name, world) : null;
  }

  return verifyToken(invite, bearer, "session", Date.now());
}

function forward(request: Request, env: Env, slot: number, ticket: JoinTicket): Promise<Response> {
  const headers = new Headers(request.headers);
  headers.set(JOIN_HEADER, encodeURIComponent(JSON.stringify(ticket)));
  const stub = env.ROOMS.get(env.ROOMS.idFromName(`slot:${slot}`));
  return stub.fetch(new Request(request.url, { method: "GET", headers }));
}

function preferredSlot(id: string, roomCount: number): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < id.length; index++) {
    hash = Math.imul(hash ^ id.charCodeAt(index), 0x01000193) >>> 0;
  }

  return hash % roomCount;
}

async function realtime(request: Request, env: Env, url: URL, invite: string): Promise<Response> {
  if (request.headers.get("Upgrade") !== "websocket") {
    return json({ error: "upgrade_required" }, 426);
  }

  const identity = await realtimeIdentity(request, url, invite);
  if (identity === null) {
    return json({ error: "unauthorized" }, 401);
  }

  const roomCount = maxRooms(env.MAX_ROOMS);
  const code = normalizeCode(url.searchParams.get("code"));
  const create = url.searchParams.get("create") === "1";
  if (code === null && !create) {
    return json({ error: "bad_request" }, 400);
  }

  if (!await underLimit(env)) {
    return json({ error: "rate_limited" }, 429);
  }

  if (code !== null) {
    const slot = slotOfCode(code, roomCount);
    if (slot === null) {
      return json({ error: "room_not_found" }, 404);
    }

    return forward(request, env, slot, { role: "viewer", code, ...identity });
  }

  const start = preferredSlot(identity.id, roomCount);
  for (let offset = 0; offset < roomCount; offset++) {
    const slot = (start + offset) % roomCount;
    const response = await forward(request, env, slot, { role: "host", code: randomCode(slot), ...identity });
    if (response.status !== 409) {
      return response;
    }
  }

  return json({ error: "rooms_full" }, 503);
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const invite = env.INVITE_CODE;
    if (invite === undefined || invite.length === 0) {
      return json({ error: "not_configured" }, 500);
    }

    const url = new URL(request.url);
    const route = `${request.method} ${url.pathname}`;
    switch (route) {
      case "POST /auth/challenge":
        return challenge(request, invite);
      case "POST /auth/verify":
        return verify(request, invite);
      case "GET /me":
        return me(request, invite);
      case "GET /rt":
        return realtime(request, env, url, invite);
      default:
        return json({ error: "not_found" }, 404);
    }
  },
} satisfies ExportedHandler<Env>;
