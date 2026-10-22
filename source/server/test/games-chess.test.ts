import { describe, expect, it } from "vitest";
import { CHESS_CLOCK_MS, CHESS_INCREMENT_MS, CHESS_KIND, type ChessState, chess } from "../src/games/chess";
import { type GameAction, Reason, type RoomView } from "../src/games/engine";

const START = 1_000_000;
const PIECE_CODES: Record<string, number> = { p: 1, n: 2, b: 3, r: 4, q: 5, k: 6 };
const INITIAL_SQUARES = [
  12, 10, 11, 13, 14, 11, 10, 12,
  9, 9, 9, 9, 9, 9, 9, 9,
  0, 0, 0, 0, 0, 0, 0, 0,
  0, 0, 0, 0, 0, 0, 0, 0,
  0, 0, 0, 0, 0, 0, 0, 0,
  0, 0, 0, 0, 0, 0, 0, 0,
  1, 1, 1, 1, 1, 1, 1, 1,
  4, 2, 3, 5, 6, 3, 2, 4,
];
const DTO_FIELDS = [
  "roundIndex", "hostUserId", "players", "squares", "blackToMove", "castling", "enPassant", "halfmoveClock",
  "whiteSeat", "whiteMsRemaining", "blackMsRemaining", "turnStartedAtUnixMs", "lastFrom", "lastTo", "inCheck",
  "moveCount", "actionCount", "lastKind", "endKind", "winnerSeat",
];

function mulberry32(seed: number): () => number {
  let value = seed >>> 0;
  return () => {
    value = (value + 0x6d2b79f5) >>> 0;
    let mixed = Math.imul(value ^ (value >>> 15), value | 1);
    mixed ^= mixed + Math.imul(mixed ^ (mixed >>> 7), mixed | 61);
    return ((mixed ^ (mixed >>> 14)) >>> 0) / 4294967296;
  };
}

const random = mulberry32(20261022);

function at(name: string): number {
  const column = name.charCodeAt(0) - "a".charCodeAt(0);
  return (8 - Number(name[1])) * 8 + column;
}

// The first FEN field lists rank 8 first, the same order as the engine's square 0 (a8).
function boardOf(placement: string): string {
  let board = "";
  for (const mark of placement) {
    if (mark === "/") {
      continue;
    }

    if (mark >= "1" && mark <= "8") {
      board += "0".repeat(Number(mark));
      continue;
    }

    const lower = mark.toLowerCase();
    board += (PIECE_CODES[lower] | (mark === lower ? 8 : 0)).toString(16);
  }

  return board;
}

function action(name: string, from = -1, to = -1, color = 0): GameAction {
  return { action: name, card: -1, color, from, to, column: -1, cell: -1, target: -1, fleet: null };
}

function moveAction(notation: string): GameAction {
  const promotion = notation.length > 4 ? PIECE_CODES[notation[4]] : 0;
  return action("move", at(notation.slice(0, 2)), at(notation.slice(2, 4)), promotion);
}

function mover(state: ChessState): number {
  return state.blackToMove ? 1 - state.whiteSeat : state.whiteSeat;
}

function fresh(): ChessState {
  return chess.start(2, 0, random, START);
}

function setup(placement: string, blackToMove = false, castling = 0, enPassant = -1, halfmoveClock = 0): ChessState {
  const state = fresh();
  state.board = boardOf(placement);
  state.blackToMove = blackToMove;
  state.castling = castling;
  state.enPassant = enPassant;
  state.halfmoveClock = halfmoveClock;
  return state;
}

function play(state: ChessState, notation: string, now = START): string {
  return chess.act(state, mover(state), moveAction(notation), random, now);
}

function playLine(state: ChessState, line: string[]): void {
  for (const notation of line) {
    expect(play(state, notation), notation).toBe(Reason.applied);
  }
}

function squares(state: ChessState): number[] {
  return chess.view(state, room(2)).squares as number[];
}

function room(seatCount: number): RoomView {
  return {
    round: 3,
    hostId: "user-0",
    actionCount: 7,
    seats: Array.from({ length: seatCount }, (_, index) => ({
      userId: `user-${index}`,
      displayName: `Player ${index}`,
      away: index === 1,
      wins: index,
    })),
  };
}

// Counts the moves the engine accepts by offering it every from, to and promotion; the totals are the published
// perft numbers for these positions, so they pin the move generator to the client's ChessBoard.
function acceptedMoves(state: ChessState): ChessState[] {
  const results: ChessState[] = [];
  const board = squares(state);
  for (let from = 0; from < 64; from++) {
    const occupant = board[from];
    if (occupant === 0 || ((occupant & 8) !== 0) !== state.blackToMove) {
      continue;
    }

    const promotions = (occupant & 7) === 1 ? [0, 2, 3, 4, 5] : [0];
    for (let to = 0; to < 64; to++) {
      for (const promotion of promotions) {
        const copy = structuredClone(state);
        if (chess.act(copy, mover(copy), action("move", from, to, promotion), random, START) === Reason.applied) {
          results.push(copy);
        }
      }
    }
  }

  return results;
}

describe("chess engine", () => {
  it("starts with the standard position, a random white seat and two full clocks", () => {
    const state = fresh();
    expect(chess.kind).toBe(CHESS_KIND);
    expect(CHESS_KIND).toBe("games.chess");
    expect(chess.maxSeats).toBe(2);
    expect(chess.startOption(0)).toBe(0);
    expect([0, 1]).toContain(state.whiteSeat);
    expect(squares(state)).toEqual(INITIAL_SQUARES);
    expect(state.castling).toBe(15);
    expect(state.enPassant).toBe(-1);
    expect(state.whiteMsRemaining).toBe(CHESS_CLOCK_MS);
    expect(state.blackMsRemaining).toBe(CHESS_CLOCK_MS);
    expect(chess.deadline(state)).toBe(START + CHESS_CLOCK_MS);
    expect(chess.finished(state)).toBe(false);
    expect(chess.winner(state)).toBe(-1);
    expect(JSON.stringify(state).length).toBeLessThan(1024);

    const seen = new Set<number>();
    const seeded = mulberry32(7);
    for (let round = 0; round < 32; round++) {
      seen.add(chess.start(2, 0, seeded, START).whiteSeat);
    }

    expect(seen).toEqual(new Set([0, 1]));
  });

  it("plays opening moves and tracks en passant, halfmove clock and the last move", () => {
    const state = fresh();
    expect(play(state, "e2e4")).toBe(Reason.applied);
    expect(state.blackToMove).toBe(true);
    expect(state.enPassant).toBe(at("e3"));
    expect(state.lastFrom).toBe(at("e2"));
    expect(state.lastTo).toBe(at("e4"));
    expect(state.moveCount).toBe(1);
    expect(state.lastKind).toBe("move");
    expect(squares(state)[at("e4")]).toBe(1);
    expect(squares(state)[at("e2")]).toBe(0);

    expect(play(state, "g8f6")).toBe(Reason.applied);
    expect(state.blackToMove).toBe(false);
    expect(state.enPassant).toBe(-1);
    expect(state.halfmoveClock).toBe(1);
    expect(state.moveCount).toBe(2);
    expect(squares(state)[at("f6")]).toBe(10);
  });

  it("refuses illegal moves without touching the state", () => {
    const state = fresh();
    const before = structuredClone(state);
    expect(play(state, "e2e5")).toBe(Reason.invalid);
    expect(play(state, "b1b3")).toBe(Reason.invalid);
    expect(play(state, "a1a3")).toBe(Reason.invalid);
    expect(play(state, "e7e5")).toBe(Reason.invalid);
    expect(chess.act(state, state.whiteSeat, action("move", -1, 99), random, START)).toBe(Reason.invalid);
    expect(chess.act(state, state.whiteSeat, action("drop"), random, START)).toBe(Reason.invalid);
    expect(state).toEqual(before);

    const pinned = setup("4k3/4r3/8/8/8/8/4B3/4K3");
    expect(play(pinned, "e2d3")).toBe(Reason.invalid);
    expect(play(pinned, "e1d1")).toBe(Reason.applied);
  });

  it("answers not_your_turn to the seat that is not on move", () => {
    const state = fresh();
    const black = 1 - state.whiteSeat;
    expect(chess.act(state, black, moveAction("e7e5"), random, START)).toBe(Reason.notYourTurn);
    playLine(state, ["d2d4"]);
    expect(chess.act(state, state.whiteSeat, moveAction("e2e4"), random, START)).toBe(Reason.notYourTurn);
  });

  it("castles both ways and spends the castling rights", () => {
    const state = setup("r3k2r/8/8/8/8/8/8/R3K2R", false, 15);
    expect(play(state, "e1g1")).toBe(Reason.applied);
    expect(squares(state)[at("g1")]).toBe(6);
    expect(squares(state)[at("f1")]).toBe(4);
    expect(squares(state)[at("h1")]).toBe(0);
    expect(state.castling).toBe(12);

    expect(play(state, "e8c8")).toBe(Reason.applied);
    expect(squares(state)[at("c8")]).toBe(14);
    expect(squares(state)[at("d8")]).toBe(12);
    expect(squares(state)[at("a8")]).toBe(0);
    expect(state.castling).toBe(0);

    const rookMoved = setup("r3k2r/8/8/8/8/8/8/R3K2R", false, 15);
    expect(play(rookMoved, "h1h2")).toBe(Reason.applied);
    expect(rookMoved.castling).toBe(14);
  });

  it("refuses castling through an attacked square or without the right", () => {
    const attacked = setup("4kr2/8/8/8/8/8/8/R3K2R", false, 3);
    expect(play(attacked, "e1g1")).toBe(Reason.invalid);
    expect(play(attacked, "e1c1")).toBe(Reason.applied);
    expect(squares(attacked)[at("d1")]).toBe(4);

    const noRight = setup("4k3/8/8/8/8/8/8/R3K2R", false, 0);
    expect(play(noRight, "e1g1")).toBe(Reason.invalid);
  });

  it("captures en passant only on the very next move", () => {
    const state = fresh();
    playLine(state, ["e2e4", "a7a6", "e4e5", "d7d5"]);
    expect(state.enPassant).toBe(at("d6"));
    expect(play(state, "e5d6")).toBe(Reason.applied);
    expect(squares(state)[at("d6")]).toBe(1);
    expect(squares(state)[at("d5")]).toBe(0);
    expect(state.halfmoveClock).toBe(0);

    const late = fresh();
    playLine(late, ["e2e4", "a7a6", "e4e5", "d7d5", "g1f3", "a6a5"]);
    expect(play(late, "e5d6")).toBe(Reason.invalid);
  });

  it("promotes to the chosen piece and refuses a promotion without one", () => {
    const state = setup("7k/P7/8/8/8/8/8/K7");
    expect(play(state, "a7a8")).toBe(Reason.invalid);
    expect(play(state, "a7a8k")).toBe(Reason.invalid);
    expect(play(state, "a7a8p")).toBe(Reason.invalid);
    expect(play(state, "a7a8q")).toBe(Reason.applied);
    expect(squares(state)[at("a8")]).toBe(5);
    expect(state.inCheck).toBe(true);
    expect(state.endKind).toBe("");

    const knight = setup("7k/P7/8/8/8/8/8/K7");
    expect(play(knight, "a7a8n")).toBe(Reason.applied);
    expect(squares(knight)[at("a8")]).toBe(2);

    const black = setup("k7/8/8/8/8/8/p7/7K", true);
    expect(play(black, "a2a1r")).toBe(Reason.applied);
    expect(squares(black)[at("a1")]).toBe(12);

    const plain = fresh();
    expect(play(plain, "e2e4q")).toBe(Reason.invalid);
  });

  it("flags a check without ending the game", () => {
    const state = fresh();
    playLine(state, ["e2e4", "f7f5", "d1h5"]);
    expect(state.inCheck).toBe(true);
    expect(state.endKind).toBe("");
    expect(chess.view(state, room(2)).inCheck).toBe(true);
    expect(play(state, "g7g6")).toBe(Reason.applied);
    expect(state.inCheck).toBe(false);
  });

  it("ends in checkmate with fool's mate", () => {
    const state = fresh();
    const black = 1 - state.whiteSeat;
    playLine(state, ["f2f3", "e7e5", "g2g4", "d8h4"]);
    expect(state.endKind).toBe("checkmate");
    expect(state.inCheck).toBe(true);
    expect(chess.finished(state)).toBe(true);
    expect(chess.winner(state)).toBe(black);
    expect(chess.deadline(state)).toBe(0);
    expect(play(state, "a2a3")).toBe(Reason.invalid);
  });

  it("ends in a stalemate draw", () => {
    const state = setup("k7/8/1K6/8/8/8/8/2Q5");
    expect(play(state, "c1c7")).toBe(Reason.applied);
    expect(state.endKind).toBe("stalemate");
    expect(state.inCheck).toBe(false);
    expect(chess.winner(state)).toBe(-1);
  });

  it("ends in a draw once the halfmove clock reaches one hundred", () => {
    const state = setup("4k3/8/8/8/8/8/8/R3K3", false, 0, -1, 99);
    expect(play(state, "a1b1")).toBe(Reason.applied);
    expect(state.halfmoveClock).toBe(100);
    expect(state.endKind).toBe("fifty");
    expect(chess.winner(state)).toBe(-1);

    const reset = setup("4k3/8/8/8/8/8/P7/R3K3", false, 0, -1, 99);
    expect(play(reset, "a2a3")).toBe(Reason.applied);
    expect(reset.halfmoveClock).toBe(0);
    expect(reset.endKind).toBe("");
  });

  it("ends in a draw on insufficient material", () => {
    const bareKings = setup("4k3/8/8/8/8/8/3r4/4K3");
    expect(play(bareKings, "e1d2")).toBe(Reason.applied);
    expect(bareKings.endKind).toBe("material");
    expect(chess.winner(bareKings)).toBe(-1);

    const sameBishops = setup("4kb2/8/8/8/8/8/3r4/2B1K3");
    expect(play(sameBishops, "e1d2")).toBe(Reason.applied);
    expect(sameBishops.endKind).toBe("material");

    const oppositeBishops = setup("4k1b1/8/8/8/8/8/3r4/2B1K3");
    expect(play(oppositeBishops, "e1d2")).toBe(Reason.applied);
    expect(oppositeBishops.endKind).toBe("");
  });

  it("charges the mover's clock and adds the increment", () => {
    const state = fresh();
    expect(play(state, "e2e4", START + 3000)).toBe(Reason.applied);
    expect(state.whiteMsRemaining).toBe(CHESS_CLOCK_MS - 3000 + CHESS_INCREMENT_MS);
    expect(state.blackMsRemaining).toBe(CHESS_CLOCK_MS);
    expect(state.turnStartedAt).toBe(START + 3000);
    expect(chess.deadline(state)).toBe(START + 3000 + CHESS_CLOCK_MS);

    expect(play(state, "e7e5", START + 10000)).toBe(Reason.applied);
    expect(state.blackMsRemaining).toBe(CHESS_CLOCK_MS - 7000 + CHESS_INCREMENT_MS);
    expect(chess.deadline(state)).toBe(START + 10000 + state.whiteMsRemaining);
  });

  it("loses on time when a move arrives after the mover's clock ran out", () => {
    const inTime = fresh();
    expect(play(inTime, "e2e4", START + CHESS_CLOCK_MS - 1)).toBe(Reason.applied);
    expect(inTime.endKind).toBe("");

    const state = fresh();
    const black = 1 - state.whiteSeat;
    expect(play(state, "e2e4", START + CHESS_CLOCK_MS)).toBe(Reason.applied);
    expect(state.endKind).toBe("timeout");
    expect(state.lastKind).toBe("timeout");
    expect(chess.winner(state)).toBe(black);
    expect(state.whiteMsRemaining).toBe(0);
    expect(state.moveCount).toBe(0);
    expect(squares(state)).toEqual(INITIAL_SQUARES);
    expect(chess.deadline(state)).toBe(0);
  });

  it("applies a claimed timeout only after the deadline", () => {
    const state = fresh();
    const black = 1 - state.whiteSeat;
    expect(chess.claim(state, random, START + CHESS_CLOCK_MS - 1)).toBe(false);
    expect(state.endKind).toBe("");
    expect(chess.claim(state, random, START + CHESS_CLOCK_MS)).toBe(true);
    expect(state.endKind).toBe("timeout");
    expect(chess.winner(state)).toBe(black);
    expect(state.whiteMsRemaining).toBe(0);
    expect(chess.claim(state, random, START + CHESS_CLOCK_MS * 2)).toBe(false);

    const blackFlags = fresh();
    expect(play(blackFlags, "e2e4", START + 1000)).toBe(Reason.applied);
    expect(chess.claim(blackFlags, random, chess.deadline(blackFlags) - 1)).toBe(false);
    expect(chess.claim(blackFlags, random, chess.deadline(blackFlags))).toBe(true);
    expect(chess.winner(blackFlags)).toBe(blackFlags.whiteSeat);
    expect(blackFlags.blackMsRemaining).toBe(0);
  });

  it("lets either seat resign at any time", () => {
    const state = fresh();
    const black = 1 - state.whiteSeat;
    expect(chess.act(state, black, action("resign"), random, START + 2000)).toBe(Reason.applied);
    expect(state.endKind).toBe("resign");
    expect(state.lastKind).toBe("resign");
    expect(chess.winner(state)).toBe(state.whiteSeat);
    expect(state.whiteMsRemaining).toBe(CHESS_CLOCK_MS - 2000);
    expect(chess.act(state, state.whiteSeat, action("resign"), random, START + 3000)).toBe(Reason.invalid);

    const resigner = fresh();
    expect(chess.act(resigner, resigner.whiteSeat, action("resign"), random, START)).toBe(Reason.applied);
    expect(chess.winner(resigner)).toBe(1 - resigner.whiteSeat);
  });

  it("ends in desertion when a seat leaves mid-round", () => {
    const state = fresh();
    const black = 1 - state.whiteSeat;
    chess.forfeit(state, state.whiteSeat, START + 500);
    expect(state.endKind).toBe("desertion");
    expect(chess.finished(state)).toBe(true);
    expect(chess.winner(state)).toBe(black);
    expect(state.whiteMsRemaining).toBe(CHESS_CLOCK_MS - 500);

    chess.forfeit(state, black, START + 900);
    expect(chess.winner(state)).toBe(black);
  });

  it("views the lobby before the first round with the roster only", () => {
    const view = chess.view(null, room(2));
    expect(Object.keys(view).sort()).toEqual([...DTO_FIELDS].sort());
    expect(view.roundIndex).toBe(3);
    expect(view.hostUserId).toBe("user-0");
    expect(view.actionCount).toBe(7);
    expect(view.squares).toEqual([]);
    expect(view.whiteSeat).toBe(-1);
    expect(view.winnerSeat).toBe(-1);
    expect(view.endKind).toBe("");
    expect(view.players).toEqual([
      { userId: "user-0", displayName: "Player 0", seat: 0, away: false, wins: 0 },
      { userId: "user-1", displayName: "Player 1", seat: 1, away: true, wins: 1 },
    ]);
  });

  it("views a live round with the DTO fields and lists seats beyond the round", () => {
    const state = fresh();
    playLine(state, ["e2e4"]);
    const view = chess.view(state, room(3));
    expect(Object.keys(view).sort()).toEqual([...DTO_FIELDS].sort());
    expect(view.squares).toHaveLength(64);
    expect(view.blackToMove).toBe(true);
    expect(view.castling).toBe(15);
    expect(view.enPassant).toBe(at("e3"));
    expect(view.halfmoveClock).toBe(0);
    expect(view.whiteSeat).toBe(state.whiteSeat);
    expect(view.whiteMsRemaining).toBe(state.whiteMsRemaining);
    expect(view.blackMsRemaining).toBe(CHESS_CLOCK_MS);
    expect(view.turnStartedAtUnixMs).toBe(START);
    expect(view.lastFrom).toBe(at("e2"));
    expect(view.lastTo).toBe(at("e4"));
    expect(view.inCheck).toBe(false);
    expect(view.moveCount).toBe(1);
    expect(view.lastKind).toBe("move");
    const players = view.players as { seat: number; userId: string }[];
    expect(players).toHaveLength(3);
    expect(players[2]).toMatchObject({ seat: 2, userId: "user-2" });
  });

  it("has no private payload", () => {
    const state = fresh();
    expect(chess.secret(state, 0, room(2))).toBeNull();
    expect(chess.secret(state, 1, room(2))).toBeNull();
  });

  it("accepts exactly the published perft move counts", () => {
    expect(acceptedMoves(fresh())).toHaveLength(20);

    const kiwipete = setup("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R", false, 15);
    expect(acceptedMoves(kiwipete)).toHaveLength(48);

    const endgame = setup("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8");
    const replies = acceptedMoves(endgame);
    expect(replies).toHaveLength(14);
    let total = 0;
    for (const reply of replies) {
      total += acceptedMoves(reply).length;
    }

    expect(total).toBe(191);

    const promotions = setup("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1", false, 12);
    expect(acceptedMoves(promotions)).toHaveLength(6);

    const tricky = setup("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R", false, 3, -1, 1);
    expect(acceptedMoves(tricky)).toHaveLength(44);
  });
});
