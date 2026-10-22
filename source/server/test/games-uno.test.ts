import { describe, expect, it } from "vitest";
import type { GameAction, Random, RoomView } from "../src/games/engine";
import {
  UNO_DECK_SIZE,
  UNO_HAND_SIZE,
  UNO_KIND,
  UNO_MAX_SEATS,
  UNO_RULE_SET_DEFAULT,
  UNO_RULE_SET_HOUSE,
  UNO_TURN_SECONDS,
  UNO_WILD,
  UNO_WILD_DRAW_FOUR,
  type UnoState,
  uno,
} from "../src/games/uno";

const NOW = 1_700_000_000_000;
const TURN_MILLISECONDS = UNO_TURN_SECONDS * 1000;
const CARD_BASE = 48;
const RED = 0;
const YELLOW = 1;
const GREEN = 2;
const BLUE = 3;
const SEVEN = 7;
const SKIP = 10;
const REVERSE = 11;
const DRAW_TWO = 12;

function mulberry32(seed: number): Random {
  let value = seed >>> 0;
  return () => {
    value = (value + 0x6d2b79f5) >>> 0;
    let mixed = value;
    mixed = Math.imul(mixed ^ (mixed >>> 15), mixed | 1);
    mixed ^= mixed + Math.imul(mixed ^ (mixed >>> 7), mixed | 61);
    return ((mixed ^ (mixed >>> 14)) >>> 0) / 4294967296;
  };
}

function card(color: number, rank: number): number {
  return color * 13 + rank;
}

function rankOf(value: number): number {
  return value >= 0 && value < UNO_WILD ? value % 13 : -1;
}

function byNumber(left: number, right: number): number {
  return left - right;
}

function pack(cards: readonly number[]): string {
  let packed = "";
  for (const value of cards) {
    packed += String.fromCharCode(CARD_BASE + value);
  }

  return packed;
}

function unpack(pile: string): number[] {
  const cards: number[] = [];
  for (let index = 0; index < pile.length; index++) {
    cards.push(pile.charCodeAt(index) - CARD_BASE);
  }

  return cards;
}

function fullDeck(): number[] {
  const deck: number[] = [];
  for (let color = 0; color < 4; color++) {
    deck.push(card(color, 0));
    for (let rank = 1; rank <= DRAW_TWO; rank++) {
      deck.push(card(color, rank), card(color, rank));
    }
  }

  for (let copy = 0; copy < 4; copy++) {
    deck.push(UNO_WILD, UNO_WILD_DRAW_FOUR);
  }

  return deck.sort(byNumber);
}

function expectConserved(state: UnoState): void {
  const everyCard = unpack(state.hands.join("") + state.drawPile + state.discardPile).sort(byNumber);
  expect(everyCard).toHaveLength(UNO_DECK_SIZE);
  expect(everyCard).toEqual(fullDeck());
}

// Mirrors GameRoomWire.IsPlayable from the client, so the engine is checked against the client's own rule.
function wirePlayable(value: number, activeColor: number, topCard: number, ruleSet: number, pendingDrawCount: number): boolean {
  if (pendingDrawCount > 0) {
    if (ruleSet !== UNO_RULE_SET_HOUSE) {
      return false;
    }

    if (value === UNO_WILD_DRAW_FOUR) {
      return true;
    }

    return rankOf(topCard) === DRAW_TWO && rankOf(value) === DRAW_TWO;
  }

  if (value === UNO_WILD || value === UNO_WILD_DRAW_FOUR) {
    return true;
  }

  if (Math.floor(value / 13) === activeColor) {
    return true;
  }

  return topCard < UNO_WILD && rankOf(value) === rankOf(topCard);
}

interface TableSetup {
  hands: number[][];
  top: number;
  activeColor?: number;
  drawFirst?: number[];
  emptyDrawPile?: boolean;
  ruleSet?: number;
  turnSeat?: number;
  clockwise?: boolean;
  pendingDrawCount?: number;
}

// Builds a mid-round table from the cards named in the setup; every other card of the deck sits at the bottom of the
// draw pile (or under the discard top when the draw pile is empty), so all 108 cards stay accounted for.
function table(setup: TableSetup): UnoState {
  const remaining = fullDeck();
  const drawFirst = setup.drawFirst ?? [];
  const named = [...setup.hands.flat(), setup.top, ...drawFirst];
  for (const value of named) {
    const index = remaining.indexOf(value);
    expect(index).toBeGreaterThanOrEqual(0);
    remaining.splice(index, 1);
  }

  const drawOrder = [...drawFirst].reverse();
  return {
    ruleSet: setup.ruleSet ?? UNO_RULE_SET_DEFAULT,
    hands: setup.hands.map(pack),
    out: setup.hands.map(() => false),
    drawPile: setup.emptyDrawPile === true ? pack(drawOrder) : pack(remaining) + pack(drawOrder),
    discardPile: setup.emptyDrawPile === true ? pack([...remaining, setup.top]) : pack([setup.top]),
    activeColor: setup.activeColor ?? Math.floor(setup.top / 13),
    clockwise: setup.clockwise ?? true,
    turnSeat: setup.turnSeat ?? 0,
    turnStartedAt: NOW,
    pendingDrawCount: setup.pendingDrawCount ?? 0,
    pendingDrawnCard: -1,
    lastSeat: -1,
    lastKind: "",
    lastCard: -1,
    winnerSeat: -1,
  };
}

function room(seatCount: number, actionCount = 1, round = 1): RoomView {
  const seats: RoomView["seats"] = [];
  for (let index = 0; index < seatCount; index++) {
    seats.push({ userId: `user-${index}`, displayName: `Player ${index}`, away: index === 1, wins: index });
  }

  return { round, hostId: "user-0", actionCount, seats };
}

function request(kind: string, value = -1, color = -1): GameAction {
  return { action: kind, card: value, color, from: -1, to: -1, column: -1, cell: -1, target: -1, fleet: null };
}

function send(state: UnoState, seat: number, kind: string, value = -1, color = -1, now = NOW): string {
  return uno.act(state, seat, request(kind, value, color), mulberry32(seat + 11), now);
}

function handOf(state: UnoState, seat: number): number[] {
  return unpack(state.hands[seat]).sort(byNumber);
}

function topOf(state: UnoState): number {
  return state.discardPile.charCodeAt(state.discardPile.length - 1) - CARD_BASE;
}

describe("uno start", () => {
  it("accepts only the default and house rule sets", () => {
    expect(uno.kind).toBe(UNO_KIND);
    expect(UNO_KIND).toBe("games.uno");
    expect(uno.maxSeats).toBe(6);
    expect(uno.startOption(UNO_RULE_SET_DEFAULT)).toBe(0);
    expect(uno.startOption(UNO_RULE_SET_HOUSE)).toBe(1);
    expect(uno.startOption(2)).toBeNull();
    expect(uno.startOption(-1)).toBeNull();
    expect(uno.startOption(0.5)).toBeNull();
  });

  it("deals seven cards each and accounts for all 108 cards", () => {
    for (let seatCount = 2; seatCount <= UNO_MAX_SEATS; seatCount++) {
      const state = uno.start(seatCount, UNO_RULE_SET_DEFAULT, mulberry32(seatCount), NOW);
      expect(state.hands).toHaveLength(seatCount);
      for (const hand of state.hands) {
        expect(hand).toHaveLength(UNO_HAND_SIZE);
      }

      expect(state.discardPile).toHaveLength(1);
      expect(state.drawPile).toHaveLength(UNO_DECK_SIZE - seatCount * UNO_HAND_SIZE - 1);
      expect(state.turnSeat).toBeGreaterThanOrEqual(0);
      expect(state.turnSeat).toBeLessThan(seatCount);
      expect(state.clockwise).toBe(true);
      expect(state.pendingDrawCount).toBe(0);
      expect(uno.deadline(state)).toBe(NOW + TURN_MILLISECONDS);
      expect(uno.finished(state)).toBe(false);
      expect(uno.winner(state)).toBe(-1);
      expectConserved(state);
    }
  });

  it("always opens on a number card and sets its color", () => {
    for (let seed = 1; seed <= 300; seed++) {
      const state = uno.start(2 + (seed % 5), UNO_RULE_SET_HOUSE, mulberry32(seed), NOW);
      const top = topOf(state);
      expect(rankOf(top)).toBeGreaterThanOrEqual(0);
      expect(rankOf(top)).toBeLessThanOrEqual(9);
      expect(state.activeColor).toBe(Math.floor(top / 13));
      expect(state.ruleSet).toBe(UNO_RULE_SET_HOUSE);
      expectConserved(state);
    }
  });

  it("keeps the state small and structured cloneable", () => {
    const state = uno.start(UNO_MAX_SEATS, UNO_RULE_SET_DEFAULT, mulberry32(3), NOW);
    expect(structuredClone(state)).toEqual(state);
    expect(JSON.stringify(state).length).toBeLessThan(1024);
  });
});

describe("uno plays", () => {
  it("plays a card that matches the color, then one that matches the rank", () => {
    const state = table({ hands: [[card(RED, 5), card(BLUE, 9)], [card(BLUE, 5), card(YELLOW, 2)], [card(GREEN, 1)]], top: card(RED, 3) });
    expect(send(state, 0, "play", card(RED, 5))).toBe("");
    expect(topOf(state)).toBe(card(RED, 5));
    expect(state.activeColor).toBe(RED);
    expect(state.turnSeat).toBe(1);
    expect(state.lastKind).toBe("uno.play");
    expect(state.lastSeat).toBe(0);
    expect(state.lastCard).toBe(card(RED, 5));
    expect(handOf(state, 0)).toEqual([card(BLUE, 9)]);

    expect(send(state, 1, "play", card(BLUE, 5), -1, NOW + 4000)).toBe("");
    expect(state.activeColor).toBe(BLUE);
    expect(state.turnSeat).toBe(2);
    expect(uno.deadline(state)).toBe(NOW + 4000 + TURN_MILLISECONDS);
    expectConserved(state);
  });

  it("refuses unplayable cards and cards the seat does not hold without touching the state", () => {
    const state = table({ hands: [[card(BLUE, 9), card(RED, 5)], [card(GREEN, 4)]], top: card(RED, 3) });
    const before = structuredClone(state);
    expect(send(state, 0, "play", card(BLUE, 9))).toBe("invalid");
    expect(send(state, 0, "play", card(GREEN, 4))).toBe("invalid");
    expect(send(state, 0, "play", 99)).toBe("invalid");
    expect(send(state, 0, "play", -1)).toBe("invalid");
    expect(send(state, 0, "play", 1.5)).toBe("invalid");
    expect(state).toEqual(before);
  });

  it("refuses a seat out of turn and unknown actions", () => {
    const state = table({ hands: [[card(RED, 5)], [card(RED, 6)]], top: card(RED, 3) });
    const before = structuredClone(state);
    expect(send(state, 1, "play", card(RED, 6))).toBe("not_your_turn");
    expect(send(state, 1, "draw")).toBe("not_your_turn");
    expect(send(state, 1, "pass")).toBe("not_your_turn");
    expect(send(state, 0, "move")).toBe("invalid");
    expect(send(state, 0, "resign")).toBe("invalid");
    expect(state).toEqual(before);
  });

  it("sets the chosen color for a wild and refuses a missing color", () => {
    const state = table({ hands: [[UNO_WILD, card(RED, 1)], [card(YELLOW, 1), card(BLUE, 1)]], top: card(GREEN, 3) });
    expect(send(state, 0, "play", UNO_WILD)).toBe("invalid");
    expect(send(state, 0, "play", UNO_WILD, 4)).toBe("invalid");
    expect(send(state, 0, "play", UNO_WILD, YELLOW)).toBe("");
    expect(topOf(state)).toBe(UNO_WILD);
    expect(state.activeColor).toBe(YELLOW);
    expect(state.lastCard).toBe(UNO_WILD);
    expect(state.turnSeat).toBe(1);
    expect(send(state, 1, "play", card(BLUE, 1))).toBe("invalid");
    expect(send(state, 1, "play", card(YELLOW, 1))).toBe("");
    expectConserved(state);
  });

  it("skips the next seat", () => {
    const three = table({ hands: [[card(RED, SKIP), card(RED, 1)], [card(BLUE, 1)], [card(GREEN, 1)]], top: card(RED, 3) });
    expect(send(three, 0, "play", card(RED, SKIP))).toBe("");
    expect(three.turnSeat).toBe(2);

    const two = table({ hands: [[card(RED, SKIP), card(RED, 1)], [card(BLUE, 1)]], top: card(RED, 3) });
    expect(send(two, 0, "play", card(RED, SKIP))).toBe("");
    expect(two.turnSeat).toBe(0);
    expectConserved(two);
  });

  it("reverses the direction of play with three or more seats", () => {
    const state = table({
      hands: [[card(RED, 2), card(GREEN, 1)], [card(RED, REVERSE), card(RED, 1)], [card(BLUE, 1)], [card(YELLOW, 1)]],
      top: card(RED, 3),
      turnSeat: 1,
    });
    expect(send(state, 1, "play", card(RED, REVERSE))).toBe("");
    expect(state.clockwise).toBe(false);
    expect(state.turnSeat).toBe(0);
    expect(send(state, 0, "play", card(RED, 2))).toBe("");
    expect(state.turnSeat).toBe(3);
    expectConserved(state);
  });

  it("treats a reverse as a skip with two seats left", () => {
    const two = table({ hands: [[card(RED, REVERSE), card(RED, 1)], [card(BLUE, 1)]], top: card(RED, 3) });
    expect(send(two, 0, "play", card(RED, REVERSE))).toBe("");
    expect(two.clockwise).toBe(false);
    expect(two.turnSeat).toBe(0);

    const three = table({ hands: [[card(RED, REVERSE), card(RED, 1)], [card(BLUE, 1)], [card(GREEN, 1)]], top: card(RED, 3) });
    uno.forfeit(three, 2, NOW);
    expect(send(three, 0, "play", card(RED, REVERSE))).toBe("");
    expect(three.turnSeat).toBe(0);
    expectConserved(three);
  });
});

describe("uno draw penalties", () => {
  it("default rules: a draw two makes the next seat draw two and lose the turn", () => {
    const state = table({
      hands: [[card(RED, DRAW_TWO), card(RED, 1)], [card(BLUE, 1)], [card(GREEN, 1)]],
      top: card(RED, 3),
      drawFirst: [card(YELLOW, 8), card(YELLOW, 9)],
    });
    expect(send(state, 0, "play", card(RED, DRAW_TWO))).toBe("");
    expect(handOf(state, 1)).toEqual([card(BLUE, 1), card(YELLOW, 8), card(YELLOW, 9)].sort(byNumber));
    expect(state.turnSeat).toBe(2);
    expect(state.pendingDrawCount).toBe(0);
    expect(state.lastKind).toBe("uno.play");
    expectConserved(state);
  });

  it("default rules: a draw two with two seats returns the turn to the player", () => {
    const state = table({ hands: [[card(RED, DRAW_TWO), card(RED, 1)], [card(BLUE, 1)]], top: card(RED, 3) });
    expect(send(state, 0, "play", card(RED, DRAW_TWO))).toBe("");
    expect(state.hands[1]).toHaveLength(3);
    expect(state.turnSeat).toBe(0);
  });

  it("default rules: a wild draw four sets the color, deals four and skips", () => {
    const state = table({ hands: [[UNO_WILD_DRAW_FOUR, card(RED, 1)], [card(BLUE, 1)], [card(GREEN, 1)]], top: card(RED, 3) });
    expect(send(state, 0, "play", UNO_WILD_DRAW_FOUR, BLUE)).toBe("");
    expect(state.activeColor).toBe(BLUE);
    expect(state.hands[1]).toHaveLength(5);
    expect(state.turnSeat).toBe(2);
    expect(state.pendingDrawCount).toBe(0);
    expect(send(state, 2, "play", card(GREEN, 1))).toBe("invalid");
    expectConserved(state);
  });

  it("house rules: draw twos and wild draw fours stack until a seat draws the whole stack", () => {
    const state = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [
        [card(RED, DRAW_TWO), card(RED, 1)],
        [card(BLUE, DRAW_TWO), card(BLUE, 1), card(YELLOW, 5)],
        [UNO_WILD_DRAW_FOUR, card(GREEN, 1), card(GREEN, 2)],
      ],
      top: card(RED, 3),
    });
    expect(send(state, 0, "play", card(RED, DRAW_TWO))).toBe("");
    expect(state.pendingDrawCount).toBe(2);
    expect(state.turnSeat).toBe(1);
    expect(state.hands[1]).toHaveLength(3);

    expect(send(state, 1, "play", card(BLUE, 1))).toBe("invalid");
    expect(send(state, 1, "play", card(BLUE, DRAW_TWO))).toBe("");
    expect(state.pendingDrawCount).toBe(4);
    expect(state.turnSeat).toBe(2);

    expect(send(state, 2, "play", card(GREEN, 1))).toBe("invalid");
    expect(send(state, 2, "play", UNO_WILD_DRAW_FOUR, GREEN)).toBe("");
    expect(state.pendingDrawCount).toBe(8);
    expect(state.activeColor).toBe(GREEN);
    expect(state.turnSeat).toBe(0);
    expect(uno.view(state, room(3)).pendingDrawCount).toBe(8);

    expect(send(state, 0, "play", card(RED, 1))).toBe("invalid");
    expect(send(state, 0, "draw")).toBe("");
    expect(state.hands[0]).toHaveLength(9);
    expect(state.pendingDrawCount).toBe(0);
    expect(state.pendingDrawnCard).toBe(-1);
    expect(state.lastKind).toBe("uno.draw");
    expect(state.lastSeat).toBe(0);
    expect(state.lastCard).toBe(-1);
    expect(state.turnSeat).toBe(1);
    expect(uno.view(state, room(3)).pendingDraw).toBe(false);
    expectConserved(state);
  });

  it("house rules: a draw two does not stack on a wild draw four", () => {
    const state = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [[card(RED, DRAW_TWO), UNO_WILD_DRAW_FOUR, card(RED, 1)], [card(BLUE, 1)]],
      top: UNO_WILD_DRAW_FOUR,
      activeColor: RED,
      pendingDrawCount: 4,
    });
    expect(send(state, 0, "play", card(RED, DRAW_TWO))).toBe("invalid");
    expect(send(state, 0, "play", UNO_WILD_DRAW_FOUR, RED)).toBe("");
    expect(state.pendingDrawCount).toBe(8);
    expectConserved(state);
  });

  it("house rules: a timeout with a pending stack draws the whole stack", () => {
    const state = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [[card(RED, 1)], [card(BLUE, 1)], [card(GREEN, 1)]],
      top: card(RED, DRAW_TWO),
      pendingDrawCount: 6,
    });
    expect(uno.claim(state, mulberry32(5), NOW + TURN_MILLISECONDS)).toBe(true);
    expect(state.hands[0]).toHaveLength(7);
    expect(state.pendingDrawCount).toBe(0);
    expect(state.lastKind).toBe("uno.timeout");
    expect(state.turnSeat).toBe(1);
    expectConserved(state);
  });
});

describe("uno draws", () => {
  it("lets the seat play only the card it drew when that card is playable", () => {
    const state = table({ hands: [[card(RED, 9)], [card(YELLOW, 4)]], top: card(RED, 3), drawFirst: [card(RED, 7)] });
    expect(send(state, 0, "draw", -1, -1, NOW + 5000)).toBe("");
    expect(handOf(state, 0)).toEqual([card(RED, 7), card(RED, 9)]);
    expect(state.turnSeat).toBe(0);
    expect(state.lastKind).toBe("uno.draw");
    expect(uno.deadline(state)).toBe(NOW + 5000 + TURN_MILLISECONDS);
    expect(uno.view(state, room(2)).pendingDraw).toBe(true);
    expect(uno.secret(state, 0, room(2))?.payload).toMatchObject({ pendingDrawnPlayable: true, pendingDrawnCard: card(RED, 7) });
    expect(uno.secret(state, 1, room(2))?.payload).toMatchObject({ pendingDrawnPlayable: false, pendingDrawnCard: -1 });

    expect(send(state, 0, "draw")).toBe("invalid");
    expect(send(state, 0, "play", card(RED, 9))).toBe("invalid");
    expect(send(state, 0, "play", card(RED, 7))).toBe("");
    expect(state.turnSeat).toBe(1);
    expect(uno.view(state, room(2)).pendingDraw).toBe(false);
    expectConserved(state);
  });

  it("passes after drawing a playable card", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)]], top: card(RED, 3), drawFirst: [card(GREEN, 3)] });
    expect(send(state, 0, "draw")).toBe("");
    expect(state.pendingDrawnCard).toBe(card(GREEN, 3));
    expect(send(state, 0, "pass")).toBe("");
    expect(state.lastKind).toBe("uno.pass");
    expect(state.lastSeat).toBe(0);
    expect(state.turnSeat).toBe(1);
    expect(state.pendingDrawnCard).toBe(-1);
    expect(handOf(state, 0)).toEqual([card(GREEN, 3), card(BLUE, 9)].sort(byNumber));
    expectConserved(state);
  });

  it("passes the turn at once when the drawn card is not playable", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)]], top: card(RED, 3), drawFirst: [card(GREEN, 8)] });
    expect(send(state, 0, "draw")).toBe("");
    expect(state.hands[0]).toHaveLength(2);
    expect(state.turnSeat).toBe(1);
    expect(state.pendingDrawnCard).toBe(-1);
    expect(state.lastKind).toBe("uno.draw");
    expect(send(state, 1, "pass")).toBe("invalid");
    expectConserved(state);
  });

  it("refuses a pass without a drawn card", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)]], top: card(RED, 3) });
    expect(send(state, 0, "pass")).toBe("invalid");
  });

  it("reshuffles the discard pile except its top card when the draw pile runs out", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)]], top: card(RED, 3), emptyDrawPile: true });
    expect(state.drawPile).toHaveLength(0);
    expect(send(state, 0, "draw")).toBe("");
    expect(state.discardPile).toBe(pack([card(RED, 3)]));
    expect(state.drawPile).toHaveLength(UNO_DECK_SIZE - 2 - 1 - 1);
    expect(state.hands[0]).toHaveLength(2);
    expectConserved(state);
  });

  it("passes the turn when no card is left to draw", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)]], top: card(RED, 3) });
    state.drawPile = "";
    expect(send(state, 0, "draw")).toBe("");
    expect(state.hands[0]).toHaveLength(1);
    expect(state.turnSeat).toBe(1);
  });
});

describe("uno house rule seven and zero", () => {
  it("swaps hands with the target seat passed in color", () => {
    const state = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [[card(RED, SEVEN), card(RED, 1), card(RED, 2)], [card(BLUE, 1)], [card(GREEN, 1), card(GREEN, 2), card(GREEN, 4)]],
      top: card(RED, 3),
    });
    const before = structuredClone(state);
    expect(send(state, 0, "play", card(RED, SEVEN), 0)).toBe("invalid");
    expect(send(state, 0, "play", card(RED, SEVEN), 3)).toBe("invalid");
    expect(send(state, 0, "play", card(RED, SEVEN), -1)).toBe("invalid");
    expect(state).toEqual(before);

    expect(send(state, 0, "play", card(RED, SEVEN), 2)).toBe("");
    expect(handOf(state, 0)).toEqual([card(GREEN, 1), card(GREEN, 2), card(GREEN, 4)]);
    expect(handOf(state, 2)).toEqual([card(RED, 1), card(RED, 2)]);
    expect(handOf(state, 1)).toEqual([card(BLUE, 1)]);
    expect(state.lastCard).toBe(card(RED, SEVEN));
    expect(state.turnSeat).toBe(1);
    expectConserved(state);
  });

  it("refuses a swap with a seat that left", () => {
    const state = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [[card(RED, SEVEN), card(RED, 1)], [card(BLUE, 1)], [card(GREEN, 1)]],
      top: card(RED, 3),
    });
    uno.forfeit(state, 2, NOW);
    expect(send(state, 0, "play", card(RED, SEVEN), 2)).toBe("invalid");
    expect(send(state, 0, "play", card(RED, SEVEN), 1)).toBe("");
    expect(handOf(state, 0)).toEqual([card(BLUE, 1)]);
  });

  it("rotates every active hand in the direction of play", () => {
    const clockwise = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [[card(RED, 0), card(RED, 1)], [card(BLUE, 1), card(BLUE, 2)], [card(GREEN, 1), card(GREEN, 2), card(GREEN, 4)]],
      top: card(RED, 3),
    });
    expect(send(clockwise, 0, "play", card(RED, 0))).toBe("");
    expect(handOf(clockwise, 0)).toEqual([card(GREEN, 1), card(GREEN, 2), card(GREEN, 4)]);
    expect(handOf(clockwise, 1)).toEqual([card(RED, 1)]);
    expect(handOf(clockwise, 2)).toEqual([card(BLUE, 1), card(BLUE, 2)]);
    expect(clockwise.turnSeat).toBe(1);
    expectConserved(clockwise);

    const counter = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [[card(RED, 0), card(RED, 1)], [card(BLUE, 1)], [card(GREEN, 1)], [card(YELLOW, 1), card(YELLOW, 2)]],
      top: card(RED, 3),
      clockwise: false,
    });
    uno.forfeit(counter, 2, NOW);
    expect(send(counter, 0, "play", card(RED, 0))).toBe("");
    expect(handOf(counter, 3)).toEqual([card(RED, 1)]);
    expect(handOf(counter, 1)).toEqual([card(YELLOW, 1), card(YELLOW, 2)]);
    expect(handOf(counter, 0)).toEqual([card(BLUE, 1)]);
    expect(counter.hands[2]).toBe("");
    expect(counter.turnSeat).toBe(3);
    expectConserved(counter);
  });

  it("leaves sevens and zeros plain under the default rules", () => {
    const state = table({
      hands: [[card(RED, SEVEN), card(RED, 0), card(RED, 1)], [card(RED, 2), card(BLUE, 1)], [card(RED, 4), card(GREEN, 1)]],
      top: card(RED, 3),
    });
    expect(send(state, 0, "play", card(RED, SEVEN))).toBe("");
    expect(handOf(state, 0)).toEqual([card(RED, 0), card(RED, 1)]);
    expect(send(state, 1, "play", card(RED, 2))).toBe("");
    expect(send(state, 2, "play", card(RED, 4))).toBe("");
    expect(send(state, 0, "play", card(RED, 0))).toBe("");
    expect(handOf(state, 0)).toEqual([card(RED, 1)]);
    expect(handOf(state, 1)).toEqual([card(BLUE, 1)]);
  });
});

describe("uno round end", () => {
  it("ends the round when a seat plays its last card", () => {
    const state = table({ hands: [[card(RED, 5)], [card(BLUE, 1)], [card(GREEN, 1)]], top: card(RED, 3) });
    expect(send(state, 0, "play", card(RED, 5))).toBe("");
    expect(uno.finished(state)).toBe(true);
    expect(uno.winner(state)).toBe(0);
    expect(state.winnerSeat).toBe(0);
    expect(state.turnSeat).toBe(-1);
    expect(uno.deadline(state)).toBe(0);
    expect(uno.claim(state, mulberry32(1), NOW + 10 * TURN_MILLISECONDS)).toBe(false);
    expect(send(state, 1, "draw")).toBe("invalid");
    const before = structuredClone(state);
    uno.forfeit(state, 1, NOW);
    expect(state).toEqual(before);
    expect(uno.view(state, room(3)).winnerSeat).toBe(0);
    expectConserved(state);
  });

  it("wins on a last draw two without dealing the penalty", () => {
    const state = table({ hands: [[card(RED, DRAW_TWO)], [card(BLUE, 1)]], top: card(RED, 3) });
    expect(send(state, 0, "play", card(RED, DRAW_TWO))).toBe("");
    expect(uno.winner(state)).toBe(0);
    expect(state.hands[1]).toHaveLength(1);
  });
});

describe("uno timeout claim", () => {
  it("does nothing before the deadline and draws one card for the mover after it", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)], [card(GREEN, 4)]], top: card(RED, 3) });
    const before = structuredClone(state);
    expect(uno.claim(state, mulberry32(2), NOW + TURN_MILLISECONDS - 1)).toBe(false);
    expect(state).toEqual(before);

    expect(uno.claim(state, mulberry32(2), NOW + TURN_MILLISECONDS)).toBe(true);
    expect(state.hands[0]).toHaveLength(2);
    expect(state.lastKind).toBe("uno.timeout");
    expect(state.lastSeat).toBe(0);
    expect(state.lastCard).toBe(-1);
    expect(state.turnSeat).toBe(1);
    expect(uno.deadline(state)).toBe(NOW + 2 * TURN_MILLISECONDS);
    expectConserved(state);
  });

  it("keeps a drawn playable card and still draws one on timeout", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)]], top: card(RED, 3), drawFirst: [card(RED, 7), card(GREEN, 8)] });
    expect(send(state, 0, "draw")).toBe("");
    expect(state.pendingDrawnCard).toBe(card(RED, 7));
    expect(uno.claim(state, mulberry32(2), NOW + TURN_MILLISECONDS)).toBe(true);
    expect(handOf(state, 0)).toEqual([card(RED, 7), card(GREEN, 8), card(BLUE, 9)].sort(byNumber));
    expect(state.pendingDrawnCard).toBe(-1);
    expect(state.turnSeat).toBe(1);
    expectConserved(state);
  });
});

describe("uno forfeit", () => {
  it("takes a seat that is not moving out of the round", () => {
    const state = table({
      hands: [[card(RED, 5), card(RED, 6)], [card(RED, 7), card(RED, 8)], [card(GREEN, 1), card(GREEN, 2)]],
      top: card(RED, 3),
    });
    uno.forfeit(state, 2, NOW + 10);
    expect(state.out).toEqual([false, false, true]);
    expect(state.hands[2]).toBe("");
    expect(state.drawPile.startsWith(pack([card(GREEN, 1), card(GREEN, 2)]))).toBe(true);
    expect(state.turnSeat).toBe(0);
    expect(uno.deadline(state)).toBe(NOW + TURN_MILLISECONDS);
    expect(state.lastKind).toBe("uno.forfeit");
    expect(uno.finished(state)).toBe(false);
    expectConserved(state);

    const players = uno.view(state, room(3)).players;
    expect(players).toEqual([
      expect.objectContaining({ seat: 0, cardCount: 2 }),
      expect.objectContaining({ seat: 1, cardCount: 2 }),
      expect.objectContaining({ seat: 2, cardCount: 0 }),
    ]);

    expect(send(state, 0, "play", card(RED, 5))).toBe("");
    expect(state.turnSeat).toBe(1);
    expect(send(state, 1, "play", card(RED, 7))).toBe("");
    expect(state.turnSeat).toBe(0);
  });

  it("passes the turn and drops the stack when the mover leaves", () => {
    const state = table({
      ruleSet: UNO_RULE_SET_HOUSE,
      hands: [[card(RED, 5)], [card(BLUE, 1)], [card(GREEN, 1)]],
      top: card(RED, DRAW_TWO),
      turnSeat: 1,
      pendingDrawCount: 4,
    });
    uno.forfeit(state, 1, NOW + 10);
    expect(state.turnSeat).toBe(2);
    expect(state.pendingDrawCount).toBe(0);
    expect(uno.deadline(state)).toBe(NOW + 10 + TURN_MILLISECONDS);
    expectConserved(state);
  });

  it("clears a drawn card when its holder leaves", () => {
    const state = table({ hands: [[card(BLUE, 9)], [card(YELLOW, 4)], [card(GREEN, 4)]], top: card(RED, 3), drawFirst: [card(RED, 7)] });
    expect(send(state, 0, "draw")).toBe("");
    uno.forfeit(state, 0, NOW + 10);
    expect(state.pendingDrawnCard).toBe(-1);
    expect(state.turnSeat).toBe(1);
    expect(uno.view(state, room(3)).pendingDraw).toBe(false);
    expectConserved(state);
  });

  it("ends the round with the last active seat as the winner", () => {
    const three = table({ hands: [[card(RED, 5)], [card(BLUE, 1)], [card(GREEN, 1)]], top: card(RED, 3) });
    uno.forfeit(three, 0, NOW);
    expect(uno.finished(three)).toBe(false);
    uno.forfeit(three, 1, NOW);
    expect(uno.finished(three)).toBe(true);
    expect(uno.winner(three)).toBe(2);
    expect(uno.deadline(three)).toBe(0);
    expectConserved(three);

    const two = table({ hands: [[card(RED, 5)], [card(BLUE, 1)]], top: card(RED, 3) });
    uno.forfeit(two, 0, NOW);
    expect(uno.winner(two)).toBe(1);
    expect(two.turnSeat).toBe(-1);
  });

  it("ignores a seat that already left or never sat in the round", () => {
    const state = table({ hands: [[card(RED, 5)], [card(BLUE, 1)], [card(GREEN, 1)]], top: card(RED, 3) });
    uno.forfeit(state, 2, NOW);
    const before = structuredClone(state);
    uno.forfeit(state, 2, NOW + 5);
    uno.forfeit(state, 5, NOW + 5);
    uno.forfeit(state, -1, NOW + 5);
    expect(state).toEqual(before);
  });
});

describe("uno secret", () => {
  it("gives every seat its own sorted hand", () => {
    const state = table({ hands: [[card(BLUE, 9), card(RED, 5), UNO_WILD], [card(YELLOW, 4)], [card(GREEN, 1), card(GREEN, 2)]], top: card(RED, 3) });
    const view = room(4, 7);
    expect(uno.secret(state, 0, view)).toEqual({
      eventKind: "uno.hand",
      payload: { seat: 0, hand: [card(RED, 5), card(BLUE, 9), UNO_WILD], pendingDrawnPlayable: false, pendingDrawnCard: -1, actionCount: 7 },
    });
    expect(uno.secret(state, 1, view)?.payload).toEqual({
      seat: 1,
      hand: [card(YELLOW, 4)],
      pendingDrawnPlayable: false,
      pendingDrawnCard: -1,
      actionCount: 7,
    });
    expect(uno.secret(state, 2, view)?.payload.hand).toEqual([card(GREEN, 1), card(GREEN, 2)]);
    expect(uno.secret(state, 3, view)?.payload).toEqual({
      seat: 3,
      hand: [],
      pendingDrawnPlayable: false,
      pendingDrawnCard: -1,
      actionCount: 7,
    });

    uno.forfeit(state, 2, NOW);
    expect(uno.secret(state, 2, view)?.payload.hand).toEqual([]);
  });
});

describe("uno view", () => {
  it("mirrors UnoRoomStateDto and UnoPlayerDto", () => {
    const state = table({
      hands: [[card(BLUE, 9), card(RED, 5)], [card(YELLOW, 4)], [card(GREEN, 1), card(GREEN, 2), card(GREEN, 3)]],
      top: card(RED, 3),
      turnSeat: 1,
    });
    state.lastKind = "uno.play";
    state.lastSeat = 0;
    state.lastCard = card(RED, 3);
    expect(uno.view(state, room(3, 9, 4))).toEqual({
      ruleSet: UNO_RULE_SET_DEFAULT,
      pendingDrawCount: 0,
      roundIndex: 4,
      hostUserId: "user-0",
      players: [
        { userId: "user-0", displayName: "Player 0", seat: 0, away: false, wins: 0, cardCount: 2 },
        { userId: "user-1", displayName: "Player 1", seat: 1, away: true, wins: 1, cardCount: 1 },
        { userId: "user-2", displayName: "Player 2", seat: 2, away: false, wins: 2, cardCount: 3 },
      ],
      maxSeats: UNO_MAX_SEATS,
      discardTop: card(RED, 3),
      activeColor: RED,
      clockwise: true,
      turnSeat: 1,
      pendingDraw: false,
      drawPileCount: UNO_DECK_SIZE - 6 - 1,
      actionCount: 9,
      turnSeconds: UNO_TURN_SECONDS,
      lastSeat: 0,
      lastKind: "uno.play",
      lastCard: card(RED, 3),
      winnerSeat: -1,
    });
  });

  it("lists the roster in the lobby before any round", () => {
    expect(uno.view(null, room(2, 3, 0))).toEqual({
      ruleSet: UNO_RULE_SET_DEFAULT,
      pendingDrawCount: 0,
      roundIndex: 0,
      hostUserId: "user-0",
      players: [
        { userId: "user-0", displayName: "Player 0", seat: 0, away: false, wins: 0, cardCount: 0 },
        { userId: "user-1", displayName: "Player 1", seat: 1, away: true, wins: 1, cardCount: 0 },
      ],
      maxSeats: UNO_MAX_SEATS,
      discardTop: -1,
      activeColor: -1,
      clockwise: true,
      turnSeat: -1,
      pendingDraw: false,
      drawPileCount: 0,
      actionCount: 3,
      turnSeconds: UNO_TURN_SECONDS,
      lastSeat: -1,
      lastKind: "",
      lastCard: -1,
      winnerSeat: -1,
    });
  });

  it("gives seats that joined after the round neutral values", () => {
    const state = uno.start(2, UNO_RULE_SET_HOUSE, mulberry32(9), NOW);
    const board = uno.view(state, room(4, 2));
    expect(board.ruleSet).toBe(UNO_RULE_SET_HOUSE);
    expect(board.players).toEqual([
      expect.objectContaining({ seat: 0, cardCount: UNO_HAND_SIZE }),
      expect.objectContaining({ seat: 1, cardCount: UNO_HAND_SIZE }),
      expect.objectContaining({ seat: 2, cardCount: 0, userId: "user-2" }),
      expect.objectContaining({ seat: 3, cardCount: 0, userId: "user-3" }),
    ]);
  });
});

describe("uno simulated rounds", () => {
  it("never loses a card and agrees with the client playability rule", () => {
    let emptyHandWins = 0;
    let reshuffles = 0;
    let largestStack = 0;
    for (const ruleSet of [UNO_RULE_SET_DEFAULT, UNO_RULE_SET_HOUSE]) {
      for (let seed = 1; seed <= 24; seed++) {
        const random = mulberry32(seed * 101 + ruleSet);
        const chooser = mulberry32(seed * 7919 + ruleSet);
        const seatCount = 2 + (seed % 5);
        const state = uno.start(seatCount, ruleSet, random, NOW);
        let now = NOW;
        for (let step = 0; step < 4000 && !uno.finished(state); step++) {
          now += 1000;
          const mover = state.turnSeat;
          const pending = state.pendingDrawnCard;
          const top = topOf(state);
          const hand = unpack(state.hands[mover]);
          const legal = hand.filter((value) => wirePlayable(value, state.activeColor, top, state.ruleSet, state.pendingDrawCount)
            && (pending < 0 || value === pending));
          const illegal = hand.filter((value) => !legal.includes(value));
          if (illegal.length > 0) {
            const before = structuredClone(state);
            expect(uno.act(state, mover, request("play", illegal[0], RED), random, now)).toBe("invalid");
            expect(state).toEqual(before);
          }

          const other = (mover + 1) % seatCount;
          if (!state.out[other]) {
            expect(uno.act(state, other, request("draw"), random, now)).toBe("not_your_turn");
          }

          const discardBefore = state.discardPile.length;
          const roll = chooser();
          if (roll < 0.004) {
            uno.forfeit(state, Math.floor(chooser() * seatCount), now);
          } else if (roll < 0.04) {
            expect(uno.claim(state, random, uno.deadline(state) - 1)).toBe(false);
            expect(uno.claim(state, random, uno.deadline(state))).toBe(true);
          } else if (legal.length > 0 && roll < 0.85) {
            const chosen = legal[Math.floor(chooser() * legal.length)];
            let color = -1;
            if (chosen >= UNO_WILD) {
              color = Math.floor(chooser() * 4);
            } else if (ruleSet === UNO_RULE_SET_HOUSE && rankOf(chosen) === SEVEN) {
              const targets: number[] = [];
              for (let seat = 0; seat < seatCount; seat++) {
                if (seat !== mover && !state.out[seat]) {
                  targets.push(seat);
                }
              }

              color = targets[Math.floor(chooser() * targets.length)];
            }

            expect(uno.act(state, mover, request("play", chosen, color), random, now)).toBe("");
          } else if (pending >= 0) {
            expect(uno.act(state, mover, request("pass"), random, now)).toBe("");
          } else {
            expect(uno.act(state, mover, request("draw"), random, now)).toBe("");
          }

          expectConserved(state);
          if (state.discardPile.length < discardBefore) {
            reshuffles++;
          }

          largestStack = Math.max(largestStack, state.pendingDrawCount);
          if (ruleSet === UNO_RULE_SET_DEFAULT) {
            expect(state.pendingDrawCount).toBe(0);
          }

          for (let seat = 0; seat < seatCount; seat++) {
            if (state.out[seat]) {
              expect(state.hands[seat]).toBe("");
            }
          }

          if (!uno.finished(state)) {
            expect(state.out[state.turnSeat]).toBe(false);
          }
        }

        expect(uno.finished(state)).toBe(true);
        expect(state.out[uno.winner(state)]).toBe(false);
        if (state.hands[uno.winner(state)].length === 0) {
          emptyHandWins++;
        }
      }
    }

    expect(emptyHandWins).toBeGreaterThan(24);
    expect(reshuffles).toBeGreaterThan(0);
    expect(largestStack).toBeGreaterThanOrEqual(4);
  });
});
