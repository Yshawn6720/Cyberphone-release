import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  MASTERS_OUT,
  WHITE,
  backgroundMarkup,
  brightInk,
  fetchSymbolMarkup,
  hues,
  measureSymbol,
  placementFor,
  renderMaster,
  shipDown,
  svgDocument,
  symbolGroup,
  tileStops,
} from "./cyberphone-style.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const ICONS_OUT = resolve(here, "../../src/Aetherphone/Icons");
const SETTINGS_INK = "#D8D8DC";
const GAMBA_GOLD = "#FFD36B";

/**
 * @param {string} symbol Phosphor glyph name
 * @param {"colour" | "paper" | "graphite" | "photos"} family tile family
 * @param {keyof typeof hues} hue app hue
 * @param {{ round?: boolean, ink?: string }} [options]
 */
function icon(symbol, family, hue, options = {}) {
  return { symbol, family, hue, round: options.round === true, ink: options.ink };
}

const map = {
  message: icon("chats", "colour", "Orange"),
  messages: icon("chat-circle", "colour", "Green", { round: true }),
  settings: icon("gear", "graphite", "Slate", { round: true, ink: SETTINGS_INK }),
  chirper: icon("feather", "colour", "Chirper"),
  aethergram: icon("aperture", "colour", "Aethergram", { round: true }),
  velvet: icon("fire", "colour", "Velvet", { round: true }),
  polls: icon("chart-bar", "colour", "Indigo"),
  announcements: icon("megaphone", "colour", "Orange"),
  camera: icon("camera", "graphite", "Slate", { ink: WHITE }),
  photos: icon("flower", "photos", "Gold", { round: true }),
  feedback: icon("chat-teardrop-text", "colour", "Teal"),
  music: icon("music-note", "colour", "Green"),
  maps: icon("map-trifold", "colour", "Teal"),
  venues: icon("map-pin", "colour", "Orchid"),
  games: icon("game-controller", "colour", "Red"),
  market: icon("chart-line-up", "colour", "Gold"),
  appstore: icon("squares-four", "colour", "Azure"),
  skywatcher: icon("cloud-sun", "colour", "Cyan"),
  collections: icon("trophy", "colour", "Indigo"),
  inventory: icon("backpack", "colour", "Orange"),
  fishing: icon("fish", "colour", "Teal"),
  clock: icon("clock", "graphite", "Red", { round: true, ink: WHITE }),
  notes: icon("note", "paper", "Gold"),
  calculator: icon("calculator", "graphite", "Slate", { ink: WHITE }),
  timers: icon("hourglass", "colour", "Lime"),
  shortcuts: icon("lightning", "colour", "Indigo"),
  wallet: icon("wallet", "colour", "Green"),
  dailies: icon("list-checks", "colour", "Teal"),
  calendar: icon("calendar", "paper", "Red"),
  news: icon("newspaper", "colour", "Slate"),
  character: icon("user-circle", "colour", "Azure", { round: true }),
  notifications: icon("bell", "colour", "Red"),
  jobs: icon("sword", "colour", "Indigo"),
  strats: icon("scroll", "colour", "Rose"),
  health: icon("heart", "colour", "Lime"),
  aetherstream: icon("monitor-play", "colour", "Violet"),
  muster: icon("flag-banner", "colour", "Cyan"),
  yellowpages: icon("book-open", "colour", "Gold"),
  casino: icon("poker-chip", "colour", "GambaRose", { round: true, ink: GAMBA_GOLD }),
  housing: icon("house", "colour", "Teal"),
  hunts: icon("crosshair", "colour", "Red", { round: true }),
  coin: icon("coin", "colour", "Gold", { round: true }),
  phone: icon("phone", "colour", "Green"),
  contacts: icon("address-book", "colour", "Slate"),
  findpeople: icon("user-focus", "colour", "Azure"),
  kupoai: icon("sparkle", "colour", "Violet"),
  memory: icon("cards", "colour", "Gold"),
  bubbles: icon("circles-three", "colour", "Teal"),
  whack: icon("hammer", "colour", "Lime"),
  breakout: icon("wall", "colour", "Orchid"),
  nonogram: icon("grid-nine", "colour", "Slate"),
  watersort: icon("flask", "colour", "Azure"),
  simon: icon("circles-four", "colour", "Green", { round: true }),
  flap: icon("bird", "colour", "Cyan"),
  snake: icon("path", "colour", "Lime"),
  flow: icon("flow-arrow", "colour", "Violet"),
  match3: icon("diamond", "colour", "Orchid"),
  2048: icon("stack-simple", "colour", "Orange"),
  solitaire: icon("spade", "colour", "Emerald"),
  tetris: icon("puzzle-piece", "colour", "Cyan"),
  reversi: icon("yin-yang", "colour", "Teal", { round: true }),
  minesweeper: icon("bomb", "colour", "Red"),
  sudoku: icon("grid-four", "colour", "Azure"),
  chess: icon("crown", "colour", "Gold"),
  stack: icon("stack", "colour", "Indigo"),
  crystaldrop: icon("diamonds-four", "colour", "Violet"),
  beat: icon("metronome", "colour", "Rose"),
  blade: icon("knife", "colour", "Red"),
  trivia: icon("question", "colour", "Indigo", { round: true }),
  skyfall: icon("meteor", "colour", "Orange"),
  invaders: icon("alien", "colour", "Violet"),
  capman: icon("ghost", "colour", "Gold"),
  hop: icon("rabbit", "colour", "Green"),
  squadron: icon("airplane", "colour", "Azure"),
  doom: icon("skull", "colour", "Red"),
  wordrun: icon("text-aa", "colour", "Emerald"),
  coil: icon("spiral", "colour", "Orchid", { round: true }),
  updraft: icon("wind", "colour", "Cyan"),
  swoop: icon("wave-sine", "colour", "Lime"),
  slice: icon("orange-slice", "colour", "Orange", { round: true }),
  spiral: icon("tornado", "colour", "Teal"),
  mahjong: icon("flower-lotus", "colour", "Emerald"),
  gloop: icon("drop", "colour", "Lime"),
  drift: icon("planet", "colour", "Cyan", { round: true }),
  crawler: icon("bug", "colour", "Emerald"),
  trails: icon("motorcycle", "colour", "Orchid"),
  trailblaze: icon("person-simple-run", "colour", "Emerald"),
  thrust: icon("rocket-launch", "colour", "Rose"),
  pinball: icon("joystick", "colour", "Gold"),
  crates: icon("package", "colour", "Gold"),
  delve: icon("shovel", "colour", "Teal"),
  moogleclicker: icon("hand-tap", "colour", "Violet"),
  claim: icon("polygon", "colour", "Violet"),
  lander: icon("moon-stars", "colour", "Azure"),
  luckydraw: icon("clover", "colour", "Red", { round: true }),
  broadside: icon("sailboat", "colour", "Cyan"),
  pegfall: icon("confetti", "colour", "Azure"),
  fling: icon("castle-turret", "colour", "Red"),
  siege: icon("plant", "colour", "Green"),
  crater: icon("mountains", "colour", "Orange"),
  fuse: icon("fire-simple", "colour", "Indigo"),
  snip: icon("scissors", "colour", "Rose"),
  minigolf: icon("golf", "colour", "Emerald", { round: true }),
  herd: icon("footprints", "colour", "Rose"),
  tempo: icon("waveform", "colour", "Violet"),
  uno: icon("cards-three", "colour", "Rose"),
  pool: icon("number-circle-eight", "colour", "Green", { round: true }),
  connectfour: icon("number-circle-four", "colour", "Cyan", { round: true }),
};

/**
 * Tile tint: colour/paper use the entry hue; graphite and photos use Slate.
 * @param {{ family: string, hue: keyof typeof hues, ink?: string }} entry
 * @returns {{ stops: string[], ink: string }}
 */
function tileFor(entry) {
  const tint = entry.family === "graphite" || entry.family === "photos" ? hues.Slate : hues[entry.hue];
  return { stops: tileStops(tint), ink: entry.ink ?? brightInk(tint) };
}

/**
 * @param {string} baseName output base name, without extension
 * @param {string} svg master-size SVG document
 * @param {boolean} opaque true to drop alpha
 */
async function writePair(baseName, svg, opaque) {
  const master = await renderMaster(svg, opaque);
  writeFileSync(resolve(MASTERS_OUT, `${baseName}.png`), master);
  writeFileSync(resolve(ICONS_OUT, `${baseName}.png`), await shipDown(master, opaque));
}

async function paint(id, entry) {
  const markup = await fetchSymbolMarkup(entry.symbol);
  const transform = placementFor(await measureSymbol(markup), entry.round);
  const tile = tileFor(entry);
  const symbol = symbolGroup(markup, tile.ink, transform);
  await writePair(id, svgDocument(backgroundMarkup(tile.stops) + symbol), true);
  await writePair(`${id}.fg`, svgDocument(symbol), false);
  return `${entry.symbol} on ${entry.family} ${entry.hue}`;
}

mkdirSync(ICONS_OUT, { recursive: true });
mkdirSync(MASTERS_OUT, { recursive: true });

const only = new Set(process.argv.slice(2));
const unknown = [...only].filter((id) => !(id in map));
if (unknown.length > 0) {
  console.error(`Unknown ids: ${unknown.join(", ")}`);
  process.exit(2);
}

let written = 0;
const failed = [];
for (const [id, entry] of Object.entries(map)) {
  if (only.size > 0 && !only.has(id)) {
    continue;
  }
  try {
    const summary = await paint(id, entry);
    written++;
    console.log(`  ${id.padEnd(14)} <- ${summary}`);
  } catch (error) {
    failed.push(`${id} (${entry.symbol}): ${error.message}`);
  }
}

console.log(`\nDone: ${written} icons written to ${ICONS_OUT} (masters in ${MASTERS_OUT})`);
if (failed.length > 0) {
  console.log("FAILED:\n" + failed.map((line) => "  " + line).join("\n"));
  process.exitCode = 1;
}
