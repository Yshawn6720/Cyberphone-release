import { writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
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
const IMAGES_OUT = resolve(here, "../../src/Aetherphone/Images");
const SYMBOL = "device-mobile";
const TILE_HUE = hues.Cyan;

const markup = await fetchSymbolMarkup(SYMBOL);
const transform = placementFor(await measureSymbol(markup), false);
const symbol = symbolGroup(markup, brightInk(TILE_HUE), transform);

// Icon.png: opaque tile with the symbol, 512 px RGB (no alpha), same as the app icon pairs.
const iconMaster = await renderMaster(svgDocument(backgroundMarkup(tileStops(TILE_HUE)) + symbol), true);
writeFileSync(resolve(IMAGES_OUT, "Icon.png"), await shipDown(iconMaster, true));

// Emblem.png: symbol only on a transparent canvas, 512 px RGBA.
const emblemMaster = await renderMaster(svgDocument(symbol), false);
writeFileSync(resolve(IMAGES_OUT, "Emblem.png"), await shipDown(emblemMaster, false));

console.log(`Wrote ${SYMBOL} (Cyberphone duotone, tile ${TILE_HUE}) to ${IMAGES_OUT}: Icon.png (RGB), Emblem.png (RGBA)`);
