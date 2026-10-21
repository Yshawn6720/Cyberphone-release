export const CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
export const CODE_LENGTH = 6;
export const DEFAULT_MAX_ROOMS = 3;
export const MAX_MEMBERS = 6;
export const MAX_MESSAGE_BYTES = 64 * 1024;
export const ROOM_LIFETIME_MS = 6 * 60 * 60 * 1000;
export const ALONE_TIMEOUT_MS = 30 * 60 * 1000;
export const FEATURE_PARTY = 1;
export const PERMISSION_GRANT_MASK = 1 | 2;
export const PERMISSION_CAN_HOST = 4;
export const MAX_SHARED_QUEUE = 64;

export interface RateProfile {
  capacity: number;
  refillMs: number;
}

export const HOST_RATE: RateProfile = { capacity: 40, refillMs: 2000 };
export const VIEWER_RATE: RateProfile = { capacity: 10, refillMs: 10000 };
export const DROP_WINDOW_MS = 60 * 1000;
export const DROP_LIMIT = 30;

export const CloseCode = {
  ended: 1000,
  rate: 1008,
  tooBig: 1009,
  unsupported: 1003,
  replaced: 4001,
} as const;

export const SignalType = {
  state: "stream.state",
  join: "stream.join",
  joined: "stream.joined",
  leave: "stream.leave",
  left: "stream.left",
  roster: "stream.roster",
  ended: "stream.ended",
  control: "stream.control",
  controlRequest: "stream.controlRequest",
  transfer: "stream.transfer",
  hostChanged: "stream.hostChanged",
} as const;

export const CONTROL_ACTIONS: Readonly<Record<string, true>> = { play: true, pause: true, seek: true, next: true };

export function normalizeCode(input: string | null): string | null {
  if (input === null) {
    return null;
  }

  const code = input.toUpperCase().replace(/[^A-Z0-9]/g, "");
  return code.length === CODE_LENGTH ? code : null;
}

export function maxRooms(configured: string | undefined): number {
  const parsed = Number.parseInt(configured ?? "", 10);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return DEFAULT_MAX_ROOMS;
  }

  return Math.min(parsed, CODE_ALPHABET.length);
}

export function randomCode(slot: number): string {
  const random = new Uint8Array(CODE_LENGTH - 1);
  crypto.getRandomValues(random);
  let code = CODE_ALPHABET[slot];
  for (const value of random) {
    code += CODE_ALPHABET[value % CODE_ALPHABET.length];
  }

  return code;
}

export function slotOfCode(code: string, roomCount: number): number | null {
  const slot = CODE_ALPHABET.indexOf(code[0]);
  return slot >= 0 && slot < roomCount ? slot : null;
}
