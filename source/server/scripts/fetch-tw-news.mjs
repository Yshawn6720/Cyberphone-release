// Reads the news lists of the FINAL FANTASY XIV Traditional Chinese site and writes content/news-tw.json for the
// relay's /content/news-tw document. It keeps only titles, dates, categories, links, a short summary and, for
// maintenance posts, the announced window; article bodies and images are never stored.
// Usage (bash): node scripts/fetch-tw-news.mjs [output file]
// Exit codes: 0 the news changed and the file was written, 3 nothing changed (the file is left alone), 1 failure
// (nothing written). Only exit 0 needs a relay rebuild; see docs/self-hosting-relay.md.
import { readFile, rename, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const SITE = "https://www.ffxiv.com.tw";
const SITE_HOST = new URL(SITE).hostname;
const LIST_PATH = "/web/news/news_list.aspx";
const USER_AGENT = "CyberphoneRelayNews/1.0 (+https://github.com/Yshawn6720/Cyberphone)";
const REQUEST_GAP_MS = 1500;
const REQUEST_TIMEOUT_MS = 20000;
const PAGES_PER_CATEGORY = 2;
const ITEMS_PER_CATEGORY = 12;
const SUMMARY_CHARACTERS = 120;
const TAIWAN_OFFSET = "+08:00";
const TAIWAN_OFFSET_MS = 8 * 60 * 60 * 1000;
const DAY_MS = 24 * 60 * 60 * 1000;
const UNCHANGED_EXIT = 3;

// The site's own categories (news_list.aspx?category=N) mapped onto the plugin's News tabs. The site has no
// server status feed, so the plugin hides that tab on the Traditional Chinese client.
const CATEGORIES = [
  { site: 1, key: "topics" },
  { site: 4, key: "notices" },
  { site: 3, key: "maintenance" },
  { site: 2, key: "updates" },
];
const MAINTENANCE = "maintenance";

/** @type {Record<string, string>} */
const NAMED_ENTITIES = { amp: "&", lt: "<", gt: ">", quot: "\"", apos: "'", nbsp: " " };
const ENTITY = /&(#x[0-9a-f]+|#\d+|[a-z]+);/gi;
const TAG = /<[^>]*>/g;
const DASHES = /\s*[\u2013\u2014]+\s*/g;
const GREETING = /^(親愛的|感謝(各位|您)|各位(親愛的)?光之戰士)/;
const HEADING = /^(維護時間|更新內容|活動時間|活動內容|注意事項)[：:]?$/;
const WINDOW = /(\d{1,2})\s*\/\s*(\d{1,2})(?:\s*[（(][^）)]{1,3}[）)])?\s*(\d{1,2})\s*[:：]\s*(\d{2})\s*[~～〜\-－至]\s*(?:(\d{1,2})\s*\/\s*(\d{1,2})(?:\s*[（(][^）)]{1,3}[）)])?\s*)?(\d{1,2})\s*[:：]\s*(\d{2})/;

const outputPath = process.argv[2] ?? fileURLToPath(new URL("../content/news-tw.json", import.meta.url));

/**
 * @typedef {{ newsId: string, url: string, title: string, date: string, article: boolean }} ListRow
 * @typedef {{ time: string | null, description: string | null, start: string | null, end: string | null }} Facts
 * @typedef {{ facts: Facts, title: string, url: string, windowKnown: boolean }} KnownItem
 * @typedef {{ id: string, url: string, title: string, time: string, description?: string, start?: string, end?: string }} NewsItem
 */

let lastRequestAt = 0;

/**
 * @param {string} url
 * @returns {Promise<string>}
 */
async function fetchPage(url) {
  const wait = lastRequestAt + REQUEST_GAP_MS - Date.now();
  if (wait > 0) {
    await new Promise((resolve) => setTimeout(resolve, wait));
  }

  lastRequestAt = Date.now();
  const response = await fetch(url, {
    headers: { "User-Agent": USER_AGENT, Accept: "text/html" },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  if (!response.ok) {
    throw new Error(`${url} answered ${response.status}`);
  }

  return response.text();
}

/**
 * @param {string} text
 * @returns {string}
 */
function decodeEntities(text) {
  return text.replace(ENTITY, (match, name) => {
    if (name[0] === "#") {
      const hex = name[1] === "x" || name[1] === "X";
      const code = Number.parseInt(name.slice(hex ? 2 : 1), hex ? 16 : 10);
      return Number.isFinite(code) && code > 0 && code <= 0x10ffff ? String.fromCodePoint(code) : match;
    }

    return NAMED_ENTITIES[name.toLowerCase()] ?? match;
  });
}

/**
 * @param {string} html
 * @returns {string}
 */
function plainText(html) {
  return withoutDashes(decodeEntities(html.replace(TAG, "")).replace(/\s+/g, " ").trim());
}

/**
 * The repository forbids em and en dashes (CI greps every file), and the site uses them in titles and posts.
 * @param {string} text
 * @returns {string}
 */
function withoutDashes(text) {
  return text.replace(DASHES, " - ").trim();
}

/**
 * @param {string} html
 * @returns {ListRow[]}
 */
function parseList(html) {
  const start = html.indexOf("<div class=\"list news_list\">");
  const end = html.indexOf("<ul class=\"pagination\">", start);
  if (start < 0 || end < 0) {
    throw new Error("the news list markup was not found; the site layout may have changed");
  }

  /** @type {ListRow[]} */
  const rows = [];
  for (const block of html.slice(start, end).split("<div class=\"item\">").slice(1)) {
    const newsId = /<div class="news_id">\s*(\d+)\s*<\/div>/.exec(block)?.[1];
    const titleCell = /<div class="title[^"]*">([\s\S]*?)<\/div>/.exec(block)?.[1];
    const anchor = titleCell === undefined ? null : /<a\s+href\s*=\s*"([^"]+)"[^>]*>([\s\S]*?)<\/a>/.exec(titleCell);
    const date = /<div class="publish_date">\s*(\d{4})\/(\d{2})\/(\d{2})\s*<\/div>/.exec(block);
    if (newsId === undefined || anchor === null || date === null) {
      throw new Error("a news list row did not match the expected layout");
    }

    const url = new URL(decodeEntities(anchor[1]), SITE);
    const title = plainText(anchor[2]);
    if (title.length === 0 || (url.protocol !== "https:" && url.protocol !== "http:")) {
      continue;
    }

    rows.push({
      newsId,
      url: url.href,
      title,
      date: `${date[1]}-${date[2]}-${date[3]}`,
      article: url.hostname === SITE_HOST && url.pathname.endsWith("/news_content.aspx"),
    });
  }

  return rows;
}

/**
 * @param {string} html
 * @returns {{ time: string | null, lines: string[] }}
 */
function parseArticle(html) {
  const date = /<div class="Date">\s*(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2})\s*<\/div>/.exec(html);
  const time = date === null ? null : `${date[1]}-${date[2]}-${date[3]}T${date[4]}:${date[5]}:00${TAIWAN_OFFSET}`;
  const start = html.indexOf("<div class=\"article\">");
  if (start < 0) {
    return { time, lines: [] };
  }

  const stop = html.indexOf("<hr", start);
  const body = html.slice(start, stop < 0 ? undefined : stop)
    .replace(/<(script|style)[\s\S]*?<\/\1>/gi, "")
    .replace(/<br\s*\/?>/gi, "\n")
    .replace(/<\/(p|div|li|h\d|tr|section)>/gi, "\n");
  const lines = decodeEntities(body.replace(TAG, ""))
    .split("\n")
    .map((line) => withoutDashes(line.replace(/\s+/g, " ")))
    .filter((line) => line.length > 0);
  return { time, lines };
}

/**
 * @param {string[]} lines
 * @returns {string}
 */
function summarize(lines) {
  let summary = "";
  for (const line of lines) {
    if (GREETING.test(line) || HEADING.test(line)) {
      continue;
    }

    summary = summary.length === 0 ? line : `${summary} ${line}`;
    if ([...summary].length >= SUMMARY_CHARACTERS) {
      break;
    }
  }

  const characters = [...summary];
  return characters.length > SUMMARY_CHARACTERS ? `${characters.slice(0, SUMMARY_CHARACTERS).join("").trimEnd()}…` : summary;
}

/**
 * @param {number} year
 * @param {number} month
 * @param {number} day
 * @param {number} hour
 * @param {number} minute
 * @returns {number}
 */
function taiwanTime(year, month, day, hour, minute) {
  return Date.UTC(year, month - 1, day, hour, minute) - TAIWAN_OFFSET_MS;
}

/**
 * @param {number} milliseconds
 * @returns {string}
 */
function isoInTaiwan(milliseconds) {
  return `${new Date(milliseconds + TAIWAN_OFFSET_MS).toISOString().slice(0, 19)}${TAIWAN_OFFSET}`;
}

/**
 * Finds the announced window ("10/20 10:00 ～ 10:30", "09/29 13:00 ~ 18:00", an end on a later day) under the
 * 維護時間 heading or, failing that, anywhere in the post. The year comes from the publish time.
 * @param {string[]} lines
 * @param {string} published
 * @returns {{ start: string, end: string } | null}
 */
function maintenanceWindow(lines, published) {
  const heading = lines.findIndex((line) => line.startsWith("維護時間"));
  const ordered = heading < 0 ? lines : [...lines.slice(heading), ...lines.slice(0, heading)];
  let match = null;
  for (const line of ordered) {
    match = WINDOW.exec(line);
    if (match !== null) {
      break;
    }
  }

  if (match === null) {
    return null;
  }

  const publishedAt = new Date(Date.parse(published) + TAIWAN_OFFSET_MS);
  const publishYear = publishedAt.getUTCFullYear();
  const publishMonth = publishedAt.getUTCMonth() + 1;
  const startMonth = Number(match[1]);
  const startDay = Number(match[2]);
  const startHour = Number(match[3]);
  const startMinute = Number(match[4]);
  const endMonth = match[5] === undefined ? startMonth : Number(match[5]);
  const endDay = match[6] === undefined ? startDay : Number(match[6]);
  const endHour = Number(match[7]);
  const endMinute = Number(match[8]);
  const valid = [startMonth, endMonth].every((month) => month >= 1 && month <= 12)
    && [startDay, endDay].every((day) => day >= 1 && day <= 31)
    && [startHour, endHour].every((hour) => hour <= 24)
    && [startMinute, endMinute].every((minute) => minute <= 59);
  if (!valid) {
    return null;
  }

  const startYear = startMonth + 6 < publishMonth ? publishYear + 1 : publishYear;
  const start = taiwanTime(startYear, startMonth, startDay, startHour, startMinute);
  let end = taiwanTime(endMonth < startMonth ? startYear + 1 : startYear, endMonth, endDay, endHour, endMinute);
  if (end < start && match[5] === undefined) {
    end += DAY_MS;
  }

  return end < start ? null : { start: isoInTaiwan(start), end: isoInTaiwan(end) };
}

/**
 * @param {string} path
 * @returns {Promise<Record<string, unknown> | null>}
 */
async function readPrevious(path) {
  let text;
  try {
    text = await readFile(path, "utf8");
  } catch (error) {
    if (error instanceof Error && "code" in error && error.code === "ENOENT") {
      console.log(`no previous ${path}; every article is read`);
      return null;
    }

    throw error;
  }

  return JSON.parse(text);
}

/**
 * @param {unknown} value
 * @returns {string | null}
 */
function textOrNull(value) {
  return typeof value === "string" && value.length > 0 ? value : null;
}

/**
 * Indexes the previous output by item id so unchanged posts are not read again. A maintenance copy wins because
 * only that list carries the window.
 * @param {Record<string, unknown> | null} previous
 * @returns {Map<string, KnownItem>}
 */
function knownItems(previous) {
  /** @type {Map<string, KnownItem>} */
  const known = new Map();
  if (previous === null) {
    return known;
  }

  for (const category of CATEGORIES) {
    const items = previous[category.key];
    if (!Array.isArray(items)) {
      continue;
    }

    const maintenance = category.key === MAINTENANCE;
    for (const item of items) {
      const id = textOrNull(item?.id);
      if (id === null || (known.has(id) && !maintenance)) {
        continue;
      }

      known.set(id, {
        facts: {
          time: textOrNull(item.time),
          description: textOrNull(item.description),
          start: textOrNull(item.start),
          end: textOrNull(item.end),
        },
        title: String(item.title),
        url: String(item.url),
        windowKnown: maintenance,
      });
    }
  }

  return known;
}

/**
 * @param {ListRow} row
 * @returns {Promise<Facts>}
 */
async function readArticle(row) {
  const article = parseArticle(await fetchPage(row.url));
  const summary = summarize(article.lines);
  const window = maintenanceWindow(article.lines, article.time ?? `${row.date}T00:00:00${TAIWAN_OFFSET}`);
  return {
    time: article.time,
    description: summary.length > 0 ? summary : null,
    start: window?.start ?? null,
    end: window?.end ?? null,
  };
}

/**
 * @param {Record<string, unknown> | null} previous
 * @param {Record<string, NewsItem[]>} lists
 * @returns {boolean}
 */
function sameNews(previous, lists) {
  return previous !== null
    && CATEGORIES.every((category) => JSON.stringify(previous[category.key]) === JSON.stringify(lists[category.key]));
}

/**
 * @returns {Promise<number>}
 */
async function main() {
  const previous = await readPrevious(outputPath);
  const known = knownItems(previous);
  /** @type {Map<string, { facts: Facts, windowKnown: boolean }>} */
  const read = new Map();
  /** @type {Record<string, NewsItem[]>} */
  const lists = {};
  let articlesRead = 0;
  let articlesSkipped = 0;

  for (const category of CATEGORIES) {
    /** @type {Map<string, ListRow>} */
    const rows = new Map();
    for (let page = 1; page <= PAGES_PER_CATEGORY; page++) {
      const query = page === 1 ? `?category=${category.site}` : `?page=${page}&category=${category.site}`;
      const listed = parseList(await fetchPage(`${SITE}${LIST_PATH}${query}`));
      if (page === 1 && listed.length === 0) {
        throw new Error(`category ${category.site} listed nothing; the site layout may have changed`);
      }

      for (const row of listed) {
        if (!rows.has(row.newsId)) {
          rows.set(row.newsId, row);
        }
      }
    }

    const maintenance = category.key === MAINTENANCE;
    const newest = [...rows.values()]
      .sort((left, right) => right.date.localeCompare(left.date) || Number(right.newsId) - Number(left.newsId))
      .slice(0, ITEMS_PER_CATEGORY);
    /** @type {NewsItem[]} */
    const items = [];
    for (const row of newest) {
      const id = `tw-${row.newsId}`;
      let entry = read.get(id);
      if (entry === undefined || (maintenance && !entry.windowKnown && row.article)) {
        const cached = known.get(id);
        const reusable = cached !== undefined && cached.title === row.title && cached.url === row.url
          && (!row.article || (cached.facts.description !== null && (!maintenance || cached.windowKnown)));
        if (reusable) {
          entry = { facts: cached.facts, windowKnown: cached.windowKnown };
        } else if (row.article) {
          try {
            entry = { facts: await readArticle(row), windowKnown: true };
            articlesRead++;
          } catch (error) {
            articlesSkipped++;
            console.warn(`article ${row.url} skipped: ${error instanceof Error ? error.message : String(error)}`);
            entry = { facts: { time: null, description: null, start: null, end: null }, windowKnown: true };
          }
        } else {
          entry = { facts: { time: null, description: null, start: null, end: null }, windowKnown: true };
        }

        read.set(id, entry);
      }

      /** @type {NewsItem} */
      const item = { id, url: row.url, title: row.title, time: entry.facts.time ?? `${row.date}T00:00:00${TAIWAN_OFFSET}` };
      if (entry.facts.description !== null) {
        item.description = withoutDashes(entry.facts.description);
      }

      if (maintenance && entry.facts.start !== null && entry.facts.end !== null) {
        item.start = entry.facts.start;
        item.end = entry.facts.end;
      }

      items.push(item);
    }

    items.sort((left, right) => Date.parse(right.time) - Date.parse(left.time) || right.id.localeCompare(left.id));
    lists[category.key] = items;
  }

  if (sameNews(previous, lists)) {
    console.log(`unchanged: ${outputPath} (${articlesRead} articles read)`);
    return UNCHANGED_EXIT;
  }

  const document = { source: `${SITE}${LIST_PATH}`, updatedAt: isoInTaiwan(Date.now()), ...lists };
  const temporary = `${outputPath}.tmp`;
  await writeFile(temporary, `${JSON.stringify(document, null, 2)}\n`, "utf8");
  await rename(temporary, outputPath);
  const counts = CATEGORIES.map((category) => `${category.key} ${lists[category.key].length}`).join(", ");
  console.log(`written: ${outputPath} (${counts}; ${articlesRead} articles read, ${articlesSkipped} skipped)`);
  return 0;
}

main().then(
  (code) => process.exit(code),
  (error) => {
    console.error(`news not updated: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  },
);
