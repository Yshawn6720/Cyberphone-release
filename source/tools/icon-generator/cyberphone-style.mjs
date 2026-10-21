import sharp from "sharp";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * @typedef {{ left: number, top: number, width: number, height: number }} Bounds
 */

const here = dirname(fileURLToPath(import.meta.url));
export const MASTERS_OUT = resolve(here, "masters");
const PHOSPHOR_VERSION = "2.1.1";
const SYMBOL_CACHE = resolve(MASTERS_OUT, `phosphor-${PHOSPHOR_VERSION}`);
const PHOSPHOR_VIEWBOX = 256;
export const MASTER_SIZE = 1024;
export const SHIPPED_SIZE = 512;
const MEASURE_ALPHA_THRESHOLD = 128;
const PNG_OPTIONS = { compressionLevel: 9, palette: false };

const SYMBOL_WIDTH_FRACTION = 0.58;
const ROUND_SYMBOL_WIDTH_FRACTION = 0.62;
export const TALL_SYMBOL_HEIGHT_FRACTION = 0.6;
const TALL_ASPECT_LIMIT = 0.88;

const TILE_TOP_LIGHTNESS = 0.42;
const TILE_BOTTOM_LIGHTNESS = 0.24;
const INK_LIGHTNESS = 1.25;
const GAMUT_SEARCH_STEPS = 32;

export const WHITE = "#FFFFFF";

export const hues = {
  Green: "#21A837",
  Orange: "#E1741D",
  Gold: "#BE871D",
  Lime: "#809C1D",
  Emerald: "#21A47D",
  Teal: "#21A29D",
  Cyan: "#219FB6",
  Azure: "#1F96F1",
  Indigo: "#728AF9",
  Violet: "#A778F9",
  Orchid: "#EC42F8",
  Rose: "#F95589",
  GambaRose: "#D4246A",
  Red: "#F95C53",
  Slate: "#8A8F9C",
  Chirper: "#2985F0",
  Aethergram: "#EB4D61",
  Velvet: "#E51A5B",
};

/**
 * Dark tile gradient: the hue at OKLCH lightness x0.42 (top) down to x0.24 (bottom).
 * @param {string} hue hex colour
 * @returns {[string, string]} top and bottom stop colours
 */
export function tileStops(hue) {
  return [shiftLightness(hue, TILE_TOP_LIGHTNESS), shiftLightness(hue, TILE_BOTTOM_LIGHTNESS)];
}

/**
 * Symbol ink on a dark tile: the hue brightened (OKLCH lightness x1.25).
 * @param {string} hue hex colour
 * @returns {string} hex colour
 */
export function brightInk(hue) {
  return shiftLightness(hue, INK_LIGHTNESS);
}

/**
 * @param {string} hex
 * @returns {number[]} linear RGB channels
 */
function hexToLinearRgb(hex) {
  const value = parseInt(hex.slice(1), 16);
  const channels = [(value >> 16) & 255, (value >> 8) & 255, value & 255];
  return channels.map((channel) => srgbToLinear(channel / 255));
}

/**
 * @param {number[]} channels linear RGB
 * @returns {string} uppercase hex colour
 */
function linearRgbToHex(channels) {
  /** @param {number} channel */
  const toByte = (channel) =>
    Math.round(Math.min(1, Math.max(0, linearToSrgb(channel))) * 255)
      .toString(16)
      .padStart(2, "0");
  return `#${channels.map(toByte).join("")}`.toUpperCase();
}

/**
 * @param {number} channel sRGB channel in 0..1
 * @returns {number} linear channel
 */
function srgbToLinear(channel) {
  if (channel <= 0.04045) {
    return channel / 12.92;
  }
  return ((channel + 0.055) / 1.055) ** 2.4;
}

/**
 * @param {number} channel linear channel
 * @returns {number} sRGB channel in 0..1 (may be out of range)
 */
function linearToSrgb(channel) {
  if (channel <= 0.0031308) {
    return channel * 12.92;
  }
  return 1.055 * channel ** (1 / 2.4) - 0.055;
}

/**
 * @param {number[]} linearRgb
 * @returns {{ lightness: number, chroma: number, hue: number }}
 */
function linearRgbToOklch([red, green, blue]) {
  const longCone = Math.cbrt(0.4122214708 * red + 0.5363325363 * green + 0.0514459929 * blue);
  const mediumCone = Math.cbrt(0.2119034982 * red + 0.6806995451 * green + 0.1073969566 * blue);
  const shortCone = Math.cbrt(0.0883024619 * red + 0.2817188376 * green + 0.6299787005 * blue);
  const lightness = 0.2104542553 * longCone + 0.793617785 * mediumCone - 0.0040720468 * shortCone;
  const greenRedAxis = 1.9779984951 * longCone - 2.428592205 * mediumCone + 0.4505937099 * shortCone;
  const blueYellowAxis = 0.0259040371 * longCone + 0.7827717662 * mediumCone - 0.808675766 * shortCone;
  return { lightness, chroma: Math.hypot(greenRedAxis, blueYellowAxis), hue: Math.atan2(blueYellowAxis, greenRedAxis) };
}

/**
 * @param {{ lightness: number, chroma: number, hue: number }} color
 * @returns {number[]} linear RGB (may be out of gamut)
 */
function oklchToLinearRgb({ lightness, chroma, hue }) {
  const greenRedAxis = chroma * Math.cos(hue);
  const blueYellowAxis = chroma * Math.sin(hue);
  const longCone = (lightness + 0.3963377774 * greenRedAxis + 0.2158037573 * blueYellowAxis) ** 3;
  const mediumCone = (lightness - 0.1055613458 * greenRedAxis - 0.0638541728 * blueYellowAxis) ** 3;
  const shortCone = (lightness - 0.0894841775 * greenRedAxis - 1.291485548 * blueYellowAxis) ** 3;
  return [
    4.0767416621 * longCone - 3.3077115913 * mediumCone + 0.2309699292 * shortCone,
    -1.2684380046 * longCone + 2.6097574011 * mediumCone - 0.3413193965 * shortCone,
    -0.0041960863 * longCone - 0.7034186147 * mediumCone + 1.707614701 * shortCone,
  ];
}

/**
 * @param {number[]} channels linear RGB
 * @returns {boolean}
 */
function isInsideGamut(channels) {
  return channels.every((channel) => channel >= -0.0005 && channel <= 1.0005);
}

/**
 * Binary-searches the largest chroma that stays inside sRGB at the given lightness and hue.
 * @param {{ lightness: number, chroma: number, hue: number }} color
 * @returns {number[]} linear RGB inside gamut
 */
function clipChromaToGamut(color) {
  if (isInsideGamut(oklchToLinearRgb(color))) {
    return oklchToLinearRgb(color);
  }
  let lowChroma = 0;
  let highChroma = color.chroma;
  for (let step = 0; step < GAMUT_SEARCH_STEPS; step++) {
    const middleChroma = (lowChroma + highChroma) / 2;
    if (isInsideGamut(oklchToLinearRgb({ ...color, chroma: middleChroma }))) {
      lowChroma = middleChroma;
    } else {
      highChroma = middleChroma;
    }
  }
  return oklchToLinearRgb({ ...color, chroma: lowChroma });
}

/**
 * @param {string} hex colour
 * @param {number} factor multiplier on OKLCH lightness
 * @returns {string} hex colour
 */
export function shiftLightness(hex, factor) {
  const color = linearRgbToOklch(hexToLinearRgb(hex));
  return linearRgbToHex(clipChromaToGamut({ ...color, lightness: color.lightness * factor }));
}

/**
 * Returns the Phosphor duotone inner SVG markup for a symbol, using the on-disk cache when present.
 * @param {string} name Phosphor icon name, e.g. "chats"
 * @returns {Promise<string>} inner markup of the <svg> element
 */
export async function fetchSymbolMarkup(name) {
  mkdirSync(SYMBOL_CACHE, { recursive: true });
  const cachePath = resolve(SYMBOL_CACHE, `${name}-duotone.svg`);
  if (existsSync(cachePath)) {
    return innerMarkup(readFileSync(cachePath, "utf8"));
  }
  const url = `https://cdn.jsdelivr.net/npm/@phosphor-icons/core@${PHOSPHOR_VERSION}/assets/duotone/${name}-duotone.svg`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} for ${url}`);
  }
  const svg = await response.text();
  writeFileSync(cachePath, svg);
  return innerMarkup(svg);
}

/**
 * @param {string} svg full <svg> document
 * @returns {string} content between the root <svg> open and close tags
 */
function innerMarkup(svg) {
  const openTagEnd = svg.indexOf(">", svg.indexOf("<svg")) + 1;
  const closeTagStart = svg.lastIndexOf("</svg>");
  return svg.slice(openTagEnd, closeTagStart);
}

/**
 * Wraps symbol markup in a group that paints it in one ink (currentColor layers included).
 * @param {string} markup inner symbol markup
 * @param {string} ink hex colour
 * @param {string} transform SVG transform attribute value
 * @returns {string} <g> element
 */
export function symbolGroup(markup, ink, transform) {
  return `<g transform="${transform}" fill="${ink}">${markup.replaceAll("currentColor", ink)}</g>`;
}

/**
 * @param {string} body SVG children
 * @returns {string} master-size <svg> document
 */
export function svgDocument(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${MASTER_SIZE}" height="${MASTER_SIZE}" viewBox="0 0 ${MASTER_SIZE} ${MASTER_SIZE}">${body}</svg>`;
}

/**
 * Renders the symbol black at master scale and returns its opaque bounding box in Phosphor viewbox units.
 * @param {string} markup inner symbol markup
 * @returns {Promise<Bounds>}
 */
export async function measureSymbol(markup) {
  const measureScale = MASTER_SIZE / PHOSPHOR_VIEWBOX;
  const svg = svgDocument(symbolGroup(markup, "#000000", `scale(${measureScale})`));
  const { data, info } = await sharp(Buffer.from(svg)).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  let left = info.width;
  let top = info.height;
  let right = -1;
  let bottom = -1;
  for (let row = 0; row < info.height; row++) {
    for (let column = 0; column < info.width; column++) {
      const alpha = data[(row * info.width + column) * info.channels + 3];
      if (alpha < MEASURE_ALPHA_THRESHOLD) {
        continue;
      }
      left = Math.min(left, column);
      right = Math.max(right, column);
      top = Math.min(top, row);
      bottom = Math.max(bottom, row);
    }
  }
  if (right < left) {
    throw new Error("symbol rendered no opaque pixels");
  }
  return {
    left: left / measureScale,
    top: top / measureScale,
    width: (right - left + 1) / measureScale,
    height: (bottom - top + 1) / measureScale,
  };
}

/**
 * Centres a symbol on the master canvas. Tall symbols are fitted by height, others by width.
 * @param {Bounds} bounds measured symbol bounds
 * @param {boolean} round true for round tiles (symbol may reach further)
 * @param {number} [tallFraction] share of master height for tall symbols
 * @returns {string} SVG transform attribute value
 */
export function placementFor(bounds, round, tallFraction = TALL_SYMBOL_HEIGHT_FRACTION) {
  const tall = bounds.width / bounds.height < TALL_ASPECT_LIMIT;
  let scale;
  if (tall) {
    scale = (tallFraction * MASTER_SIZE) / bounds.height;
  } else {
    const widthFraction = round ? ROUND_SYMBOL_WIDTH_FRACTION : SYMBOL_WIDTH_FRACTION;
    scale = (widthFraction * MASTER_SIZE) / bounds.width;
  }
  const translateX = MASTER_SIZE / 2 - scale * (bounds.left + bounds.width / 2);
  const translateY = MASTER_SIZE / 2 - scale * (bounds.top + bounds.height / 2);
  return `translate(${translateX.toFixed(3)} ${translateY.toFixed(3)}) scale(${scale.toFixed(5)})`;
}

/**
 * Full-canvas vertical gradient tile.
 * @param {string[]} stops gradient stop colours, top to bottom
 * @returns {string} SVG children
 */
export function backgroundMarkup(stops) {
  const stopMarkup = stops
    .map((stop, stopIndex) => `<stop offset="${((stopIndex / (stops.length - 1)) * 100).toFixed(1)}%" stop-color="${stop}"/>`)
    .join("");
  return `<defs><linearGradient id="tile" x1="0" y1="0" x2="0" y2="1">${stopMarkup}</linearGradient></defs><rect width="${MASTER_SIZE}" height="${MASTER_SIZE}" fill="url(#tile)"/>`;
}

/**
 * Rasterises an SVG document to a master-size PNG.
 * @param {string} svg master-size SVG document
 * @param {boolean} opaque true to drop the alpha channel
 * @returns {Promise<Buffer>} PNG bytes
 */
export async function renderMaster(svg, opaque) {
  const pipeline = sharp(Buffer.from(svg));
  if (opaque) {
    pipeline.removeAlpha();
  }
  return pipeline.png(PNG_OPTIONS).toBuffer();
}

/**
 * Downscales a master PNG to the shipped size.
 * @param {Buffer} master master PNG bytes
 * @param {boolean} opaque true to drop the alpha channel
 * @returns {Promise<Buffer>} PNG bytes
 */
export async function shipDown(master, opaque) {
  const pipeline = sharp(master).resize(SHIPPED_SIZE, SHIPPED_SIZE, { kernel: "lanczos3" });
  if (opaque) {
    pipeline.removeAlpha();
  }
  return pipeline.png(PNG_OPTIONS).toBuffer();
}
