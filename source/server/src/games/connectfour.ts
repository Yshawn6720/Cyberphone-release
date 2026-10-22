import { type GameAction, type GameEngine, type Random, Reason, type RoomView, randomInt, seatPlayers } from "./engine";

export const CONNECT_FOUR_KIND = "games.connectfour";
export const CONNECT_FOUR_COLUMNS = 7;
export const CONNECT_FOUR_ROWS = 6;
export const CONNECT_FOUR_TURN_SECONDS = 30;

const EMPTY = ".";
const SEATS = 2;
const LINE = 4;
const DIRECTIONS: readonly (readonly [number, number])[] = [[1, 0], [0, 1], [1, 1], [1, -1]];

export interface ConnectFourState {
  cells: string;
  turnSeat: number;
  turnStartedAt: number;
  lastColumn: number;
  lastRow: number;
  lastSeat: number;
  lastKind: string;
  endKind: string;
  winnerSeat: number;
}

function cellAt(state: ConnectFourState, column: number, row: number): number {
  const mark = state.cells[row * CONNECT_FOUR_COLUMNS + column];
  return mark === EMPTY ? -1 : Number(mark);
}

function connects(state: ConnectFourState, column: number, row: number, seat: number): boolean {
  for (const [stepColumn, stepRow] of DIRECTIONS) {
    let run = 1;
    for (const sign of [1, -1]) {
      let nextColumn = column + stepColumn * sign;
      let nextRow = row + stepRow * sign;
      while (nextColumn >= 0 && nextColumn < CONNECT_FOUR_COLUMNS && nextRow >= 0 && nextRow < CONNECT_FOUR_ROWS
        && cellAt(state, nextColumn, nextRow) === seat) {
        run++;
        nextColumn += stepColumn * sign;
        nextRow += stepRow * sign;
      }
    }

    if (run >= LINE) {
      return true;
    }
  }

  return false;
}

function finish(state: ConnectFourState, endKind: string, winnerSeat: number): void {
  state.endKind = endKind;
  state.winnerSeat = winnerSeat;
  state.turnSeat = -1;
}

function live(state: ConnectFourState): boolean {
  return state.endKind.length === 0;
}

function drop(state: ConnectFourState, seat: number, column: number, now: number): string {
  if (!Number.isInteger(column) || column < 0 || column >= CONNECT_FOUR_COLUMNS) {
    return Reason.invalid;
  }

  let row = CONNECT_FOUR_ROWS - 1;
  while (row >= 0 && cellAt(state, column, row) >= 0) {
    row--;
  }

  if (row < 0) {
    return Reason.invalid;
  }

  const index = row * CONNECT_FOUR_COLUMNS + column;
  state.cells = state.cells.slice(0, index) + String(seat) + state.cells.slice(index + 1);
  state.lastColumn = column;
  state.lastRow = row;
  state.lastSeat = seat;
  state.lastKind = "drop";
  if (connects(state, column, row, seat)) {
    finish(state, "connect", seat);
    return Reason.applied;
  }

  if (!state.cells.includes(EMPTY)) {
    finish(state, "draw", -1);
    return Reason.applied;
  }

  state.turnSeat = 1 - seat;
  state.turnStartedAt = now;
  return Reason.applied;
}

export const connectFour: GameEngine<ConnectFourState> = {
  kind: CONNECT_FOUR_KIND,
  maxSeats: SEATS,

  startOption(): number {
    return 0;
  },

  start(_seatCount: number, _option: number, random: Random, now: number): ConnectFourState {
    return {
      cells: EMPTY.repeat(CONNECT_FOUR_COLUMNS * CONNECT_FOUR_ROWS),
      turnSeat: randomInt(random, SEATS),
      turnStartedAt: now,
      lastColumn: -1,
      lastRow: -1,
      lastSeat: -1,
      lastKind: "",
      endKind: "",
      winnerSeat: -1,
    };
  },

  act(state: ConnectFourState, seat: number, action: GameAction, _random: Random, now: number): string {
    if (!live(state)) {
      return Reason.invalid;
    }

    if (action.action === "resign") {
      state.lastKind = "resign";
      state.lastSeat = seat;
      finish(state, "resign", 1 - seat);
      return Reason.applied;
    }

    if (action.action !== "drop") {
      return Reason.invalid;
    }

    return seat === state.turnSeat ? drop(state, seat, action.column, now) : Reason.notYourTurn;
  },

  claim(state: ConnectFourState, _random: Random, now: number): boolean {
    if (!live(state) || now < this.deadline(state)) {
      return false;
    }

    state.lastKind = "timeout";
    state.lastSeat = state.turnSeat;
    finish(state, "timeout", 1 - state.turnSeat);
    return true;
  },

  forfeit(state: ConnectFourState, seat: number): void {
    if (live(state)) {
      finish(state, "desertion", 1 - seat);
    }
  },

  deadline(state: ConnectFourState): number {
    return live(state) ? state.turnStartedAt + CONNECT_FOUR_TURN_SECONDS * 1000 : 0;
  },

  winner(state: ConnectFourState): number {
    return state.winnerSeat;
  },

  finished(state: ConnectFourState): boolean {
    return !live(state);
  },

  view(state: ConnectFourState | null, room: RoomView): Record<string, unknown> {
    const cells: number[] = [];
    if (state !== null) {
      for (const mark of state.cells) {
        cells.push(mark === EMPTY ? -1 : Number(mark));
      }
    }

    return {
      roundIndex: room.round,
      hostUserId: room.hostId,
      players: seatPlayers(room),
      cells,
      turnSeat: state?.turnSeat ?? -1,
      turnSeconds: CONNECT_FOUR_TURN_SECONDS,
      lastColumn: state?.lastColumn ?? -1,
      lastRow: state?.lastRow ?? -1,
      lastSeat: state?.lastSeat ?? -1,
      actionCount: room.actionCount,
      lastKind: state?.lastKind ?? "",
      endKind: state?.endKind ?? "",
      winnerSeat: state?.winnerSeat ?? -1,
    };
  },

  secret(): null {
    return null;
  },
};
