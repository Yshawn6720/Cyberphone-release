import announcements from "../content/announcements.json";
import calendar from "../content/calendar.json";
import newsTaiwan from "../content/news-tw.json";
import radio from "../content/radio.json";

export const CONTENT_MAX_AGE_SECONDS = 6 * 60 * 60;
const ETAG_BYTES = 16;

const encoder = new TextEncoder();

async function digestTag(body: string): Promise<string> {
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", encoder.encode(body)));
  let hex = "";
  for (let index = 0; index < ETAG_BYTES; index++) {
    hex += digest[index].toString(16).padStart(2, "0");
  }

  return `"${hex}"`;
}

function matchesTag(header: string | null, etag: string): boolean {
  if (header === null) {
    return false;
  }

  for (const part of header.split(",")) {
    const candidate = part.trim();
    if (candidate === "*" || candidate === etag || candidate === `W/${etag}`) {
      return true;
    }
  }

  return false;
}

export class StaticDocument {
  readonly body: string;
  private tag: Promise<string> | null = null;

  constructor(content: unknown) {
    this.body = JSON.stringify(content);
  }

  etag(): Promise<string> {
    this.tag ??= digestTag(this.body);
    return this.tag;
  }

  async respond(request: Request): Promise<Response> {
    const etag = await this.etag();
    const headers = { ETag: etag, "Cache-Control": `private, max-age=${CONTENT_MAX_AGE_SECONDS}` };
    if (matchesTag(request.headers.get("If-None-Match"), etag)) {
      return new Response(null, { status: 304, headers });
    }

    return new Response(this.body, { headers: { ...headers, "Content-Type": "application/json; charset=utf-8" } });
  }
}

export const ANNOUNCEMENTS = new StaticDocument(announcements);
export const CALENDAR = new StaticDocument(calendar);
export const NEWS_TAIWAN = new StaticDocument(newsTaiwan);
export const RADIO = new StaticDocument(radio);
