import { env as workerEnv, exports as workerExports } from "cloudflare:workers";
import { expect } from "vitest";
import type { Env } from "../src/index";

export const env = workerEnv as unknown as Env;
export const self = (workerExports as unknown as { default: { fetch(input: Request | string, init?: RequestInit): Promise<Response> } }).default;
export const INVITE = "test-invite-code";
export const BASE = "https://relay.test";

export type Message = Record<string, unknown>;

export class Peer {
  readonly inbox: Message[] = [];
  readonly frames: Uint8Array[] = [];
  closeCode: number | null = null;
  private waiters: Array<() => void> = [];

  constructor(readonly socket: WebSocket) {
    socket.binaryType = "arraybuffer";
    socket.accept();
    socket.addEventListener("message", (event) => {
      if (typeof event.data === "string") {
        this.inbox.push(JSON.parse(event.data) as Message);
      } else {
        this.frames.push(new Uint8Array(event.data as ArrayBuffer));
      }

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

  sendFrame(frame: Uint8Array): void {
    this.socket.send(frame);
  }

  async framesAtLeast(count: number): Promise<Uint8Array[]> {
    for (let attempt = 0; attempt < 200 && this.frames.length < count; attempt++) {
      await this.wait();
    }

    if (this.frames.length < count) {
      throw new Error(`timed out waiting for ${count} frames; got ${this.frames.length}`);
    }

    return this.frames;
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

  // A refused upgrade sends relay.refused straight after the handshake, so it lands within the first wait; an
  // accepted host socket stays silent until it speaks.
  async refusal(): Promise<string | null> {
    if (this.inbox.length === 0) {
      await this.wait();
    }

    const refused = this.inbox.find((message) => message.type === "relay.refused");
    return refused === undefined ? null : refused.reason as string;
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

export function realtimeRequest(query: string, bearer: string = INVITE): Request {
  return new Request(`${BASE}/rt?${query}`, {
    headers: { Upgrade: "websocket", Authorization: `Bearer ${bearer}` },
  });
}

export async function connect(query: string, bearer: string = INVITE): Promise<Peer> {
  const response = await self.fetch(realtimeRequest(query, bearer));
  expect(response.status).toBe(101);
  if (response.webSocket === null) {
    throw new Error("missing webSocket");
  }

  return new Peer(response.webSocket);
}

export async function refusedWith(response: Response): Promise<{ message: Message; closeCode: number }> {
  expect(response.status).toBe(101);
  if (response.webSocket === null) {
    throw new Error("missing webSocket");
  }

  const peer = new Peer(response.webSocket);
  const message = await peer.next("relay.refused");
  return { message, closeCode: await peer.closed() };
}

export function identity(name: string): string {
  return `name=${encodeURIComponent(name)}&world=${encodeURIComponent("伊弗利特")}`;
}
