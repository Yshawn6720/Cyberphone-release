import { DROP_LIMIT, DROP_WINDOW_MS, type RateProfile } from "./protocol";

export const OPEN = 1;

export type Message = Record<string, unknown>;

export interface Bucket {
  tokens: number;
  at: number;
  windowStart: number;
  dropped: number;
}

export function freshBucket(rate: RateProfile, now: number): Bucket {
  return { tokens: rate.capacity, at: now, windowStart: now, dropped: 0 };
}

export function send(socket: WebSocket, message: Message): void {
  if (socket.readyState === OPEN) {
    socket.send(JSON.stringify(message));
  }
}

export function parseMessage(raw: string): Message | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch (error) {
    if (!(error instanceof SyntaxError)) {
      throw error;
    }

    // quality-allow: a malformed client frame changes no room state; the protocol ignores it and the caller still charges its rate token.
    return null;
  }

  return typeof parsed === "object" && parsed !== null && !Array.isArray(parsed) ? parsed as Message : null;
}

export function exceedsUtf8Bytes(text: string, limit: number): boolean {
  if (text.length > limit) {
    return true;
  }

  if (text.length * 3 <= limit) {
    return false;
  }

  let bytes = 0;
  for (let index = 0; index < text.length; index++) {
    const unit = text.charCodeAt(index);
    bytes += unit < 0x80 ? 1 : unit < 0x800 || (unit >= 0xd800 && unit <= 0xdfff) ? 2 : 3;
    if (bytes > limit) {
      return true;
    }
  }

  return false;
}

export function charge(bucket: Bucket, rate: RateProfile, now: number): "accept" | "drop" | "close" {
  bucket.tokens = Math.min(rate.capacity, bucket.tokens + (now - bucket.at) / rate.refillMs);
  bucket.at = now;
  if (bucket.tokens >= 1) {
    bucket.tokens -= 1;
    return "accept";
  }

  if (now - bucket.windowStart > (rate.dropWindowMs ?? DROP_WINDOW_MS)) {
    bucket.windowStart = now;
    bucket.dropped = 0;
  }

  bucket.dropped++;
  return bucket.dropped > (rate.dropLimit ?? DROP_LIMIT) ? "close" : "drop";
}
