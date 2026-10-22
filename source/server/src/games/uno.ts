import { type GameAction, type GameEngine, type Random, Reason, type RoomView, type Secret, randomInt, seatPlayers } from "./engine";

export const UNO_KIND = "games.uno";
export const UNO_MAX_SEATS = 6;
export const UNO_HAND_SIZE = 7;
export const UNO_DECK_SIZE = 108;
export const UNO_TURN_SECONDS = 30;
export const UNO_RULE_SET_DEFAULT = 0;
export const UNO_RULE_SET_HOUSE = 1;
export const UNO_WILD = 52;
export const UNO_WILD_DRAW_FOUR = 53;
export const UNO_HAND_EVENT = "uno.hand";
export const UNO_PLAY_EVENT = "uno.play";
export const UNO_DRAW_EVENT = "uno.draw";
export const UNO_PASS_EVENT = "uno.pass";
export const UNO_TIMEOUT_EVENT = "uno.timeout";
export const UNO_FORFEIT_EVENT = "uno.forfeit";

const ACTION_PLAY = "play";
const ACTION_DRAW = "draw";
const ACTION_PASS = "pass";
const CARD_BASE = 48;
const COLORS = 4;
const RANKS = 13;
const RANK_ZERO = 0;
const RANK_SEVEN = 7;
const RANK_NINE = 9;
const RANK_SKIP = 10;
const RANK_REVERSE = 11;
const RANK_DRAW_TWO = 12;
const WILD_COPIES = 4;
const DRAW_TWO_PENALTY = 2;
const DRAW_FOUR_PENALTY = 4;

// Piles and hands are strings with one char per card (char code = CARD_BASE + card number, numbering from
// GameRoomWire.cs: color * 13 + rank, 52 wild, 53 wild draw four); the top of each pile is its last char.
export interface UnoState {
  ruleSet: number;
  hands: string[];
  out: boolean[];
  drawPile: string;
  discardPile: string;
  activeColor: number;
  clockwise: boolean;
  turnSeat: number;
  turnStartedAt: number;
  pendingDrawCount: number;
  pendingDrawnCard: number;
  lastSeat: number;
  lastKind: string;
  lastCard: number;
  winnerSeat: number;
}

function cardChar(card: number): string {
  return String.fromCharCode(CARD_BASE + card);
}

function cardAt(pile: string, index: number): number {
  return pile.charCodeAt(index) - CARD_BASE;
}

function colorOf(card: number): number {
  return card >= 0 && card < UNO_WILD ? Math.floor(card / RANKS) : -1;
}

function rankOf(card: number): number {
  return card >= 0 && card < UNO_WILD ? card % RANKS : -1;
}

// The exact rule of GameRoomWire.IsPlayable (src/Aetherphone/Core/Games/GameRoomWire.cs:306), which the table
// uses to lift playable cards, so the server never refuses a card the client offered or accepts one it greyed out.
function isPlayable(card: number, activeColor: number, topCard: number, ruleSet: number, pendingDrawCount: number): boolean {
  if (pendingDrawCount > 0) {
    if (ruleSet !== UNO_RULE_SET_HOUSE) {
      return false;
    }

    if (card === UNO_WILD_DRAW_FOUR) {
      return true;
    }

    return rankOf(topCard) === RANK_DRAW_TWO && rankOf(card) === RANK_DRAW_TWO;
  }

  if (card === UNO_WILD || card === UNO_WILD_DRAW_FOUR) {
    return true;
  }

  if (card < 0 || card >= UNO_WILD) {
    return false;
  }

  if (colorOf(card) === activeColor) {
    return true;
  }

  return topCard < UNO_WILD && rankOf(card) === rankOf(topCard);
}

function standardDeck(): string {
  let deck = "";
  for (let color = 0; color < COLORS; color++) {
    for (let rank = 0; rank < RANKS; rank++) {
      const card = cardChar(color * RANKS + rank);
      deck += rank === RANK_ZERO ? card : card + card;
    }
  }

  return deck + cardChar(UNO_WILD).repeat(WILD_COPIES) + cardChar(UNO_WILD_DRAW_FOUR).repeat(WILD_COPIES);
}

function shuffle(cards: string, random: Random): string {
  const deck = cards.split("");
  for (let index = deck.length - 1; index > 0; index--) {
    const swap = randomInt(random, index + 1);
    [deck[index], deck[swap]] = [deck[swap], deck[index]];
  }

  return deck.join("");
}

function topCard(state: UnoState): number {
  return state.discardPile.length > 0 ? cardAt(state.discardPile, state.discardPile.length - 1) : -1;
}

function live(state: UnoState): boolean {
  return state.winnerSeat < 0;
}

function activeSeatCount(state: UnoState): number {
  let count = 0;
  for (const gone of state.out) {
    if (!gone) {
      count++;
    }
  }

  return count;
}

function nextSeat(state: UnoState, seat: number): number {
  const seatCount = state.hands.length;
  const step = state.clockwise ? 1 : seatCount - 1;
  let candidate = seat;
  for (let tried = 0; tried < seatCount; tried++) {
    candidate = (candidate + step) % seatCount;
    if (!state.out[candidate]) {
      return candidate;
    }
  }

  return seat;
}

function drawCard(state: UnoState, random: Random): number {
  if (state.drawPile.length === 0) {
    const top = state.discardPile.length - 1;
    if (top <= 0) {
      return -1;
    }

    state.drawPile = shuffle(state.discardPile.slice(0, top), random);
    state.discardPile = state.discardPile.slice(top);
  }

  const card = cardAt(state.drawPile, state.drawPile.length - 1);
  state.drawPile = state.drawPile.slice(0, -1);
  return card;
}

function giveCards(state: UnoState, seat: number, count: number, random: Random): void {
  for (let drawn = 0; drawn < count; drawn++) {
    const card = drawCard(state, random);
    if (card < 0) {
      return;
    }

    state.hands[seat] += cardChar(card);
  }
}

function beginTurn(state: UnoState, seat: number, now: number): void {
  state.turnSeat = seat;
  state.turnStartedAt = now;
  state.pendingDrawnCard = -1;
}

function finish(state: UnoState, winnerSeat: number): void {
  state.winnerSeat = winnerSeat;
  state.turnSeat = -1;
  state.pendingDrawCount = 0;
  state.pendingDrawnCard = -1;
}

function recordEvent(state: UnoState, kind: string, seat: number, card: number): void {
  state.lastKind = kind;
  state.lastSeat = seat;
  state.lastCard = card;
}

function play(state: UnoState, seat: number, action: GameAction, random: Random, now: number): string {
  const card = action.card;
  if (!Number.isInteger(card) || card < 0 || card > UNO_WILD_DRAW_FOUR) {
    return Reason.invalid;
  }

  const hand = state.hands[seat];
  const position = hand.indexOf(cardChar(card));
  if (position < 0) {
    return Reason.invalid;
  }

  if (state.pendingDrawnCard >= 0 && card !== state.pendingDrawnCard) {
    return Reason.invalid;
  }

  if (!isPlayable(card, state.activeColor, topCard(state), state.ruleSet, state.pendingDrawCount)) {
    return Reason.invalid;
  }

  const wild = card === UNO_WILD || card === UNO_WILD_DRAW_FOUR;
  if (wild && (!Number.isInteger(action.color) || action.color < 0 || action.color >= COLORS)) {
    return Reason.invalid;
  }

  const house = state.ruleSet === UNO_RULE_SET_HOUSE;
  const rank = rankOf(card);
  const target = action.color;
  const swapsHands = house && rank === RANK_SEVEN;
  if (swapsHands && (!Number.isInteger(target) || target < 0 || target >= state.hands.length || target === seat
    || state.out[target])) {
    return Reason.invalid;
  }

  state.hands[seat] = hand.slice(0, position) + hand.slice(position + 1);
  state.discardPile += cardChar(card);
  state.activeColor = wild ? action.color : colorOf(card);
  state.pendingDrawnCard = -1;
  recordEvent(state, UNO_PLAY_EVENT, seat, card);
  if (state.hands[seat].length === 0) {
    finish(state, seat);
    return Reason.applied;
  }

  let steps = 1;
  if (card === UNO_WILD_DRAW_FOUR || rank === RANK_DRAW_TWO) {
    const penalty = card === UNO_WILD_DRAW_FOUR ? DRAW_FOUR_PENALTY : DRAW_TWO_PENALTY;
    if (house) {
      state.pendingDrawCount += penalty;
    } else {
      giveCards(state, nextSeat(state, seat), penalty, random);
      steps = 2;
    }
  } else if (rank === RANK_SKIP) {
    steps = 2;
  } else if (rank === RANK_REVERSE) {
    state.clockwise = !state.clockwise;
    steps = activeSeatCount(state) === 2 ? 2 : 1;
  } else if (swapsHands) {
    [state.hands[seat], state.hands[target]] = [state.hands[target], state.hands[seat]];
  } else if (house && rank === RANK_ZERO) {
    const rotated = state.hands.slice();
    for (let from = 0; from < state.hands.length; from++) {
      if (!state.out[from]) {
        rotated[nextSeat(state, from)] = state.hands[from];
      }
    }

    state.hands = rotated;
  }

  let next = seat;
  for (let step = 0; step < steps; step++) {
    next = nextSeat(state, next);
  }

  beginTurn(state, next, now);
  return Reason.applied;
}

function draw(state: UnoState, seat: number, random: Random, now: number): string {
  if (state.pendingDrawnCard >= 0) {
    return Reason.invalid;
  }

  recordEvent(state, UNO_DRAW_EVENT, seat, -1);
  if (state.pendingDrawCount > 0) {
    giveCards(state, seat, state.pendingDrawCount, random);
    state.pendingDrawCount = 0;
    beginTurn(state, nextSeat(state, seat), now);
    return Reason.applied;
  }

  const card = drawCard(state, random);
  if (card < 0) {
    beginTurn(state, nextSeat(state, seat), now);
    return Reason.applied;
  }

  state.hands[seat] += cardChar(card);
  if (isPlayable(card, state.activeColor, topCard(state), state.ruleSet, 0)) {
    state.pendingDrawnCard = card;
    state.turnStartedAt = now;
    return Reason.applied;
  }

  beginTurn(state, nextSeat(state, seat), now);
  return Reason.applied;
}

function pass(state: UnoState, seat: number, now: number): string {
  if (state.pendingDrawnCard < 0) {
    return Reason.invalid;
  }

  recordEvent(state, UNO_PASS_EVENT, seat, -1);
  beginTurn(state, nextSeat(state, seat), now);
  return Reason.applied;
}

export const uno: GameEngine<UnoState> = {
  kind: UNO_KIND,
  maxSeats: UNO_MAX_SEATS,

  startOption(option: number): number | null {
    return option === UNO_RULE_SET_DEFAULT || option === UNO_RULE_SET_HOUSE ? option : null;
  },

  start(seatCount: number, option: number, random: Random, now: number): UnoState {
    const deck = shuffle(standardDeck(), random);
    const hands: string[] = [];
    const out: boolean[] = [];
    for (let seat = 0; seat < seatCount; seat++) {
      hands.push(deck.slice(seat * UNO_HAND_SIZE, (seat + 1) * UNO_HAND_SIZE));
      out.push(false);
    }

    // The starter is the first number card from the top, so a round never opens on a wild, a penalty or a
    // skip; the action cards flipped past it go back under the pile.
    const rest = deck.slice(seatCount * UNO_HAND_SIZE);
    let starter = rest.length - 1;
    while (rankOf(cardAt(rest, starter)) < 0 || rankOf(cardAt(rest, starter)) > RANK_NINE) {
      starter--;
    }

    const starterCard = cardAt(rest, starter);
    return {
      ruleSet: option,
      hands,
      out,
      drawPile: rest.slice(starter + 1) + rest.slice(0, starter),
      discardPile: cardChar(starterCard),
      activeColor: colorOf(starterCard),
      clockwise: true,
      turnSeat: randomInt(random, seatCount),
      turnStartedAt: now,
      pendingDrawCount: 0,
      pendingDrawnCard: -1,
      lastSeat: -1,
      lastKind: "",
      lastCard: -1,
      winnerSeat: -1,
    };
  },

  act(state: UnoState, seat: number, action: GameAction, random: Random, now: number): string {
    if (!live(state)) {
      return Reason.invalid;
    }

    if (action.action !== ACTION_PLAY && action.action !== ACTION_DRAW && action.action !== ACTION_PASS) {
      return Reason.invalid;
    }

    if (seat !== state.turnSeat) {
      return Reason.notYourTurn;
    }

    if (action.action === ACTION_PLAY) {
      return play(state, seat, action, random, now);
    }

    return action.action === ACTION_DRAW ? draw(state, seat, random, now) : pass(state, seat, now);
  },

  claim(state: UnoState, random: Random, now: number): boolean {
    if (!live(state) || now < this.deadline(state)) {
      return false;
    }

    const seat = state.turnSeat;
    giveCards(state, seat, Math.max(1, state.pendingDrawCount), random);
    state.pendingDrawCount = 0;
    recordEvent(state, UNO_TIMEOUT_EVENT, seat, -1);
    beginTurn(state, nextSeat(state, seat), now);
    return true;
  },

  forfeit(state: UnoState, seat: number, now: number): void {
    if (!live(state) || seat < 0 || seat >= state.hands.length || state.out[seat]) {
      return;
    }

    state.out[seat] = true;
    state.drawPile = state.hands[seat] + state.drawPile;
    state.hands[seat] = "";
    recordEvent(state, UNO_FORFEIT_EVENT, seat, -1);
    if (activeSeatCount(state) < 2) {
      finish(state, state.out.indexOf(false));
      return;
    }

    if (state.turnSeat === seat) {
      state.pendingDrawCount = 0;
      beginTurn(state, nextSeat(state, seat), now);
    }
  },

  deadline(state: UnoState): number {
    return live(state) ? state.turnStartedAt + UNO_TURN_SECONDS * 1000 : 0;
  },

  winner(state: UnoState): number {
    return state.winnerSeat;
  },

  finished(state: UnoState): boolean {
    return !live(state);
  },

  view(state: UnoState | null, room: RoomView): Record<string, unknown> {
    const players = seatPlayers(room).map((player, index) => ({
      ...player,
      cardCount: state !== null && index < state.hands.length ? state.hands[index].length : 0,
    }));

    return {
      ruleSet: state?.ruleSet ?? UNO_RULE_SET_DEFAULT,
      pendingDrawCount: state?.pendingDrawCount ?? 0,
      roundIndex: room.round,
      hostUserId: room.hostId,
      players,
      maxSeats: UNO_MAX_SEATS,
      discardTop: state === null ? -1 : topCard(state),
      activeColor: state?.activeColor ?? -1,
      clockwise: state?.clockwise ?? true,
      turnSeat: state?.turnSeat ?? -1,
      pendingDraw: state !== null && state.pendingDrawnCard >= 0,
      drawPileCount: state?.drawPile.length ?? 0,
      actionCount: room.actionCount,
      turnSeconds: UNO_TURN_SECONDS,
      lastSeat: state?.lastSeat ?? -1,
      lastKind: state?.lastKind ?? "",
      lastCard: state?.lastCard ?? -1,
      winnerSeat: state?.winnerSeat ?? -1,
    };
  },

  // Seats outside the round still get an empty hand: the client keeps asking for its private lane until one
  // arrives at the current action count (src/Aetherphone/Core/Games/GameRoomsStore.cs:445).
  secret(state: UnoState, seat: number, room: RoomView): Secret {
    const inRound = seat >= 0 && seat < state.hands.length;
    const hand: number[] = [];
    if (inRound) {
      const held = state.hands[seat];
      for (let index = 0; index < held.length; index++) {
        hand.push(cardAt(held, index));
      }

      hand.sort((left, right) => left - right);
    }

    const holdsDrawn = inRound && seat === state.turnSeat && state.pendingDrawnCard >= 0;
    return {
      eventKind: UNO_HAND_EVENT,
      payload: {
        seat,
        hand,
        pendingDrawnPlayable: holdsDrawn,
        pendingDrawnCard: holdsDrawn ? state.pendingDrawnCard : -1,
        actionCount: room.actionCount,
      },
    };
  },
};
