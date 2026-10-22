import { broadside } from "./broadside";
import { chess } from "./chess";
import { connectFour } from "./connectfour";
import type { GameEngine } from "./engine";
import { uno } from "./uno";

export const GAME_ENGINES: ReadonlyMap<string, GameEngine<unknown>> = new Map<string, GameEngine<unknown>>([
  [uno.kind, uno],
  [chess.kind, chess],
  [connectFour.kind, connectFour],
  [broadside.kind, broadside],
]);
