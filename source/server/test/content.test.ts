import { listDurableObjectIds } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import announcements from "../content/announcements.json";
import calendar from "../content/calendar.json";
import worker from "../src/index";
import { CONTENT_MAX_AGE_SECONDS } from "../src/content";
import { BASE, INVITE, env, self } from "./support";

const ROUTES = ["/content/announcements", "/content/calendar"];
const ISO_WITH_ZONE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?(\.\d+)?(Z|[+-]\d{2}:\d{2})$/;

function contentRequest(path: string, headers: Record<string, string> = {}): Request {
  return new Request(`${BASE}${path}`, { headers });
}

async function fetchContent(path: string, headers: Record<string, string> = {}): Promise<Response> {
  return self.fetch(contentRequest(path, { Authorization: `Bearer ${INVITE}`, ...headers }));
}

async function countRooms(): Promise<number> {
  return (await listDurableObjectIds(env.ROOMS)).length + (await listDurableObjectIds(env.JAM_ROOMS)).length;
}

describe("static content", () => {
  it.each(ROUTES)("rejects %s without the invite before touching anything", async (path) => {
    const missing = await self.fetch(contentRequest(path));
    const wrong = await self.fetch(contentRequest(path, { Authorization: "Bearer wrong-invite" }));

    expect(missing.status).toBe(401);
    expect(wrong.status).toBe(401);
    expect(missing.headers.get("ETag")).toBeNull();
  });

  it("serves each document with an ETag and a private cache lifetime", async () => {
    const served = await fetchContent("/content/announcements");
    const calendarServed = await fetchContent("/content/calendar");

    expect(served.status).toBe(200);
    expect(served.headers.get("Content-Type")).toBe("application/json; charset=utf-8");
    expect(served.headers.get("Cache-Control")).toBe(`private, max-age=${CONTENT_MAX_AGE_SECONDS}`);
    expect(served.headers.get("ETag")).toMatch(/^"[0-9a-f]{32}"$/);
    expect(await served.json()).toEqual(announcements);
    expect(calendarServed.status).toBe(200);
    expect(await calendarServed.json()).toEqual(calendar);
    expect(calendarServed.headers.get("ETag")).not.toBe(served.headers.get("ETag"));
  });

  it.each(ROUTES)("answers a matching If-None-Match on %s with an empty 304", async (path) => {
    const first = await fetchContent(path);
    const etag = first.headers.get("ETag") ?? "";
    await first.arrayBuffer();

    const exact = await fetchContent(path, { "If-None-Match": etag });
    const weakInList = await fetchContent(path, { "If-None-Match": `"stale", W/${etag}` });
    const stale = await fetchContent(path, { "If-None-Match": "\"stale\"" });

    expect(exact.status).toBe(304);
    expect(exact.headers.get("ETag")).toBe(etag);
    expect(exact.headers.get("Cache-Control")).toBe(`private, max-age=${CONTENT_MAX_AGE_SECONDS}`);
    expect(await exact.text()).toBe("");
    expect(weakInList.status).toBe(304);
    expect(stale.status).toBe(200);
    await stale.arrayBuffer();
  });

  it("checks the invite before the ETag, so a stolen tag reveals nothing", async () => {
    const first = await fetchContent("/content/announcements");
    const etag = first.headers.get("ETag") ?? "";
    await first.arrayBuffer();

    const response = await self.fetch(contentRequest("/content/announcements", { "If-None-Match": etag }));

    expect(response.status).toBe(401);
  });

  it("never reaches a Durable Object", async () => {
    const before = await countRooms();

    for (const path of ROUTES) {
      await (await fetchContent(path)).arrayBuffer();
      await (await self.fetch(contentRequest(path))).arrayBuffer();
    }

    expect(await countRooms()).toBe(before);
  });

  it("fails closed when the invite secret is missing", async () => {
    const response = await worker.fetch(contentRequest("/content/calendar", { Authorization: `Bearer ${INVITE}` }),
      { ...env, INVITE_CODE: undefined });

    expect(response.status).toBe(500);
  });

  it("only answers GET", async () => {
    const response = await self.fetch(new Request(`${BASE}/content/announcements`, {
      method: "POST",
      headers: { Authorization: `Bearer ${INVITE}` },
    }));

    expect(response.status).toBe(404);
  });
});

describe("shipped content files", () => {
  it("lists announcements the plugin can read", () => {
    const seen = new Set<string>();
    for (const item of announcements.items) {
      expect(typeof item.id).toBe("string");
      expect(item.id.length).toBeGreaterThan(0);
      expect(seen.has(item.id)).toBe(false);
      seen.add(item.id);
      expect(typeof item.title).toBe("string");
      expect(typeof item.body).toBe("string");
      expect(Number.isInteger(item.createdAtUnix)).toBe(true);
      expect(item.createdAtUnix).toBeGreaterThan(0);
      expect(item.createdAtUnix).toBeLessThan(100_000_000_000);
    }
  });

  it("lists in-game events with zoned times that end after they begin", () => {
    const seen = new Set<number>();
    for (const event of calendar) {
      expect(Number.isSafeInteger(event.id)).toBe(true);
      expect(seen.has(event.id)).toBe(false);
      seen.add(event.id);
      expect(typeof event.name).toBe("string");
      expect(event.name.length).toBeGreaterThan(0);
      expect(event.begin).toMatch(ISO_WITH_ZONE);
      expect(event.end).toMatch(ISO_WITH_ZONE);
      expect(Date.parse(event.end)).toBeGreaterThanOrEqual(Date.parse(event.begin));
    }
  });
});
