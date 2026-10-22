const encoder = new TextEncoder();
const decoder = new TextDecoder();

export const MAX_IDENTITY_PART_LENGTH = 32;
export const CHALLENGE_LIFETIME_MS = 10 * 60 * 1000;
export const SESSION_LIFETIME_MS = 30 * 24 * 60 * 60 * 1000;

const controlCharacter = /[\u0000-\u001f\u007f]/;

export interface Identity {
  id: string;
  name: string;
  world: string;
}

type TokenKind = "challenge" | "session";

interface TokenClaims {
  kind: TokenKind;
  name: string;
  world: string;
  exp: number;
}

function isIdentityPart(value: unknown): value is string {
  return typeof value === "string"
    && value.length > 0
    && value.length <= MAX_IDENTITY_PART_LENGTH
    && value.trim() === value
    && !controlCharacter.test(value);
}

export function parseIdentity(name: unknown, world: unknown): Identity | null {
  if (!isIdentityPart(name) || !isIdentityPart(world) || name.includes("@")) {
    return null;
  }

  return { id: `${name}@${world}`, name, world };
}

// INVITE_CODE may list several codes separated by commas (one per community), so a leaked code can be revoked by
// removing it without changing everyone else's.
export function inviteCodes(configured: string | undefined): string[] {
  const codes: string[] = [];
  for (const part of (configured ?? "").split(",")) {
    const code = part.trim();
    if (code.length > 0) {
      codes.push(code);
    }
  }

  return codes;
}

// Compares against every code, so the time taken does not reveal which position matched.
export async function matchingInvite(candidate: string, invites: readonly string[]): Promise<string | null> {
  const digest = await crypto.subtle.digest("SHA-256", encoder.encode(candidate));
  let matched: string | null = null;
  for (const invite of invites) {
    const expected = await crypto.subtle.digest("SHA-256", encoder.encode(invite));
    if (crypto.subtle.timingSafeEqual(digest, expected) && matched === null) {
      matched = invite;
    }
  }

  return matched;
}

function signingKey(invite: string): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    "raw",
    encoder.encode(`cyberphone-relay-token:${invite}`),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign", "verify"],
  );
}

function toBase64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/, "");
}

function fromBase64Url(text: string): Uint8Array | null {
  if (!/^[A-Za-z0-9_-]*$/.test(text)) {
    return null;
  }

  const padded = text.replaceAll("-", "+").replaceAll("_", "/") + "===".slice((text.length + 3) % 4);
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }

  return bytes;
}

export async function signToken(invite: string, kind: TokenKind, identity: Identity, now: number): Promise<string> {
  const lifetime = kind === "challenge" ? CHALLENGE_LIFETIME_MS : SESSION_LIFETIME_MS;
  const claims: TokenClaims = { kind, name: identity.name, world: identity.world, exp: now + lifetime };
  const payload = toBase64Url(encoder.encode(JSON.stringify(claims)));
  const signature = await crypto.subtle.sign("HMAC", await signingKey(invite), encoder.encode(payload));
  return `${payload}.${toBase64Url(new Uint8Array(signature))}`;
}

function readClaims(payload: Uint8Array): TokenClaims | null {
  const parsed: unknown = JSON.parse(decoder.decode(payload));
  if (typeof parsed !== "object" || parsed === null) {
    return null;
  }

  const claims = parsed as Record<string, unknown>;
  if ((claims.kind !== "challenge" && claims.kind !== "session") || typeof claims.exp !== "number"
    || typeof claims.name !== "string" || typeof claims.world !== "string") {
    return null;
  }

  return { kind: claims.kind, name: claims.name, world: claims.world, exp: claims.exp };
}

async function verifyToken(invite: string, token: string, kind: TokenKind, now: number): Promise<Identity | null> {
  const parts = token.split(".");
  if (parts.length !== 2) {
    return null;
  }

  const payload = fromBase64Url(parts[0]);
  const signature = fromBase64Url(parts[1]);
  if (payload === null || signature === null) {
    return null;
  }

  const valid = await crypto.subtle.verify("HMAC", await signingKey(invite), signature, encoder.encode(parts[0]));
  if (!valid) {
    return null;
  }

  const claims = readClaims(payload);
  if (claims === null || claims.kind !== kind || claims.exp <= now) {
    return null;
  }

  return parseIdentity(claims.name, claims.world);
}

export interface VerifiedToken {
  identity: Identity;
  invite: string;
}

// A token is signed with the invite code it was issued under, so removing that code voids its tokens too.
export async function verifyAnyToken(invites: readonly string[], token: string, kind: TokenKind, now: number): Promise<VerifiedToken | null> {
  for (const invite of invites) {
    const identity = await verifyToken(invite, token, kind, now);
    if (identity !== null) {
      return { identity, invite };
    }
  }

  return null;
}

export function bearerOf(request: Request): string | null {
  const header = request.headers.get("Authorization");
  if (header === null || !header.startsWith("Bearer ")) {
    return null;
  }

  const value = header.slice("Bearer ".length).trim();
  return value.length > 0 ? value : null;
}
