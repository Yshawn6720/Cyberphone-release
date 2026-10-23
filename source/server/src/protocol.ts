export const CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
export const CODE_LENGTH = 6;
export const DEFAULT_MAX_ROOMS = 3;
export const DEFAULT_MAX_ROOMS_PER_ADDRESS = 3;
// How long the slot registry holds a slot it handed out, covering the hop from the registry to the slot's room.
export const SLOT_RESERVATION_MS = 10 * 1000;
export const MAX_MEMBERS = 6;
export const MAX_MESSAGE_BYTES = 64 * 1024;
export const ROOM_LIFETIME_MS = 6 * 60 * 60 * 1000;
export const ALONE_TIMEOUT_MS = 30 * 60 * 1000;
// How long a party outlives its host's dropped connection, waiting for the plugin to reconnect as that host.
export const HOST_GRACE_MS = 60 * 1000;
export const FEATURE_PARTY = 1;
export const FEATURE_DANMAKU = 2;
export const FEATURE_DUAL_VIDEO = 4;
export const PERMISSION_GRANT_MASK = 1 | 2;
export const PERMISSION_CAN_HOST = 4;
export const MAX_SHARED_QUEUE = 64;

export interface RateProfile {
  capacity: number;
  refillMs: number;
  dropLimit?: number;
  dropWindowMs?: number;
}

export const HOST_RATE: RateProfile = { capacity: 40, refillMs: 2000 };
export const VIEWER_RATE: RateProfile = { capacity: 10, refillMs: 10000 };
// Reactions draw on their own bucket so a burst of them never starves join, leave or control messages. The plugin
// sends at most 4 at once and then 1 a second; the extra headroom here only absorbs network jitter.
export const REACTION_RATE: RateProfile = { capacity: 6, refillMs: 1000 };
// Danmaku lines draw on their own bucket too. The plugin sends at most 3 at once and then 1 every 1.75 seconds.
export const DANMAKU_RATE: RateProfile = { capacity: 4, refillMs: 1500 };
// Game moves are human paced (one in flight per player, resyncs at most every 2 s); a burst of 20 and 2 a second
// sustained covers a fast Connect Four exchange plus reconnect handshakes, while a flooding client gets dropped.
export const GAME_RATE: RateProfile = { capacity: 20, refillMs: 500 };
export const DROP_WINDOW_MS = 60 * 1000;
export const DROP_LIMIT = 30;
// Voice frames are 20 ms of Opus, so a talking member sends 50 a second. 60 a second sustained plus a one second
// burst absorbs capture and send jitter; only a member that keeps exceeding it (over 200 dropped frames within
// 10 seconds, about 20 extra frames a second) is disconnected.
export const CALL_FRAME_RATE: RateProfile = { capacity: 50, refillMs: 1000 / 60, dropLimit: 200, dropWindowMs: 10 * 1000 };
export const CALL_CONTROL_RATE: RateProfile = { capacity: 10, refillMs: 2000 };
export const MAX_CALL_MEMBERS = 4;
export const MAX_CALL_TEXT_BYTES = 1024;
// The plugin's MediaFrame: version byte, 16 byte call id, slot byte, 16 bit sequence, then one Opus packet, which
// RFC 6716 caps at 1275 bytes.
export const MEDIA_VERSION = 1;
export const MEDIA_SLOT_OFFSET = 17;
export const MEDIA_HEADER_BYTES = 20;
export const MAX_CALL_FRAME_BYTES = MEDIA_HEADER_BYTES + 1275;
// Nearby lists answer one query at a time per client address: a burst of 4, then one every 15 seconds. The plugin
// asks only when its nearby page opens or on an explicit refresh, at most once per 15 seconds, so one player never
// meets this limit; it only stops a script from turning the list into a poll.
export const NEARBY_RATE: RateProfile = { capacity: 4, refillMs: 15 * 1000 };
export const MAX_NEARBY_ROOMS = 20;
export const MAX_LOCATION_ID = 0xffffffff;

export const CloseCode = {
  ended: 1000,
  rate: 1008,
  tooBig: 1009,
  unsupported: 1003,
  replaced: 4001,
  kicked: 4003,
} as const;

// A refused /rt request still completes the WebSocket handshake, so the reason survives a Cloudflare Tunnel (which
// turns any refused upgrade into a bare 502); the socket then carries one relay.refused message and this close code.
export const RefusalClose = {
  calls_disabled: 4403,
  address_rooms: 4409,
  rate_limited: 4429,
  rooms_full: 4503,
} as const;

export type RefusalReason = keyof typeof RefusalClose;
export const REFUSED_SIGNAL = "relay.refused";
// Every connect limiter counts over a 60 second window.
export const RATE_RETRY_AFTER_SECONDS = 60;

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
  joinPending: "stream.joinPending",
  joinRequest: "stream.joinRequest",
  approve: "stream.approve",
  deny: "stream.deny",
  declined: "stream.declined",
  kick: "stream.kick",
  kicked: "stream.kicked",
  react: "stream.react",
  reaction: "stream.reaction",
  danmaku: "stream.danmaku",
  danmakuLine: "stream.danmakuLine",
  queueSuggest: "stream.queueSuggest",
  queueSuggestion: "stream.queueSuggestion",
  queueApprove: "stream.queueApprove",
  queueDeny: "stream.queueDeny",
  queueSuggestionResult: "stream.queueSuggestionResult",
  playbackFailed: "stream.playbackFailed",
  viewerFailed: "stream.viewerFailed",
  nearbyRoster: "stream.nearby.roster",
  hostAway: "stream.hostAway",
  hostBack: "stream.hostBack",
} as const;

export const CONTROL_ACTIONS: Readonly<Record<string, true>> = { play: true, pause: true, seek: true, next: true };
export const DECLINE_DENIED = "denied";
export const REACTION_KINDS = 6;
export const MAX_ADMITTED = 8;
export const MAX_PENDING_SUGGESTIONS = 8;
export const MAX_SUGGESTION_URL = 2048;
export const MAX_FAILURE_REASON = 200;
export const MAX_DANMAKU_CHARS = 60;

export const JamSignal = {
  start: "jam.start",
  join: "jam.join",
  joined: "jam.joined",
  declined: "jam.declined",
  leave: "jam.leave",
  end: "jam.end",
  ended: "jam.ended",
  roster: "jam.roster",
  state: "jam.state",
  control: "jam.control",
  controlRequest: "jam.controlRequest",
  transfer: "jam.transfer",
  hostChanged: "jam.hostChanged",
  queue: "jam.queue",
  queueAdd: "jam.queueAdd",
  queueAdvance: "jam.queueAdvance",
  queueRemove: "jam.queueRemove",
  queueMove: "jam.queueMove",
  joinPending: "jam.joinPending",
  joinRequest: "jam.joinRequest",
  joinCancelled: "jam.joinCancelled",
  approve: "jam.approve",
  deny: "jam.deny",
  kick: "jam.kick",
  kicked: "jam.kicked",
  react: "jam.react",
  reaction: "jam.reaction",
  settings: "jam.settings",
  nearby: "jam.nearby",
  nearbyRoster: "jam.nearby.roster",
} as const;

export const JAM_CONTROL_ACTIONS: Readonly<Record<string, true>> = {
  play: true,
  pause: true,
  seek: true,
  next: true,
  previous: true,
};

// A new Jam lets guests add songs and steer playback; the host narrows it with jam.settings.
export const JAM_GUEST_PERMISSIONS = 1 | 2;
export const JAM_PERMISSION_ADD = 1;
export const JAM_PERMISSION_CONTROL = 2;
export const JAM_REACTION_KINDS = 8;
export const MAX_JAM_QUEUE = 50;
export const MAX_JAM_TITLE = 64;
export const MAX_TRACK_TEXT = 200;
export const MAX_THUMBNAIL_URL = 512;
export const THUMBNAIL_HOSTS: readonly string[] = ["ytimg.com", "ggpht.com", "googleusercontent.com"];

export const GameSignal = {
  create: "game.create",
  join: "game.join",
  attach: "game.attach",
  resync: "game.resync",
  act: "game.act",
  claim: "game.claim",
  leave: "game.leave",
  close: "game.close",
  kick: "game.kick",
  attached: "game.attached",
  snapshot: "game.snapshot",
  event: "game.event",
  private: "game.private",
  acted: "game.acted",
  declined: "game.declined",
  ended: "game.ended",
} as const;

export const CallSignal = {
  join: "call.join",
  leave: "call.leave",
  approve: "call.approve",
  deny: "call.deny",
  roster: "call.roster",
  joinPending: "call.joinPending",
  joinRequest: "call.joinRequest",
  joinCancelled: "call.joinCancelled",
  declined: "call.declined",
  ended: "call.ended",
} as const;

export type RoomKind = "party" | "jam" | "game" | "call";

export function roomKind(value: string | null): RoomKind | null {
  if (value === null || value === "party") {
    return "party";
  }

  return value === "jam" || value === "game" || value === "call" ? value : null;
}

export function callsEnabled(configured: string | undefined): boolean {
  const value = (configured ?? "").trim().toLowerCase();
  return value === "1" || value === "true";
}

export function normalizeCode(input: string | null): string | null {
  if (input === null) {
    return null;
  }

  const code = input.toUpperCase().replace(/[^A-Z0-9]/g, "");
  return code.length === CODE_LENGTH ? code : null;
}

function roomLimit(configured: string | undefined): number | null {
  const parsed = Number.parseInt(configured ?? "", 10);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return null;
  }

  return Math.min(parsed, CODE_ALPHABET.length);
}

export function maxRooms(kindLimit: string | undefined, sharedLimit: string | undefined): number {
  return roomLimit(kindLimit) ?? roomLimit(sharedLimit) ?? DEFAULT_MAX_ROOMS;
}

export function maxRoomsPerAddress(configured: string | undefined): number {
  return roomLimit(configured) ?? DEFAULT_MAX_ROOMS_PER_ADDRESS;
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

// A discoverable room is listed under a second code that leads to the same slot (its first letter names the slot,
// like every room code) but only ever opens a join request, so the real code stays with the people the host invited.
export function nearbyCode(roomCode: string): string {
  const slot = CODE_ALPHABET.indexOf(roomCode[0]);
  let code = randomCode(slot);
  while (code === roomCode) {
    code = randomCode(slot);
  }

  return code;
}

export function locationId(value: unknown): number | null {
  const parsed = typeof value === "string" ? Number(value) : value;
  return typeof parsed === "number" && Number.isInteger(parsed) && parsed > 0 && parsed <= MAX_LOCATION_ID
    ? parsed
    : null;
}
