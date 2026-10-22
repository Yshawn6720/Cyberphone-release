import { type FleetShip, type GameAction, type GameEngine, type Random, Reason, type RoomView, type Secret, randomInt, seatPlayers } from "./engine";

export const BROADSIDE_KIND = "games.broadside";
export const BROADSIDE_SIZE = 10;
export const BROADSIDE_CELL_COUNT = BROADSIDE_SIZE * BROADSIDE_SIZE;
// L.cs OnlineBroadsideHostHint promises "30 seconds a shot".
export const BROADSIDE_TURN_SECONDS = 30;
export const BROADSIDE_PLACE_SECONDS = 90;
export const BROADSIDE_MISSED_LIMIT = 3;
export const BROADSIDE_FLEET_EVENT = "broadside.fleet";
export const BROADSIDE_MARK_MISS = 1;
export const BROADSIDE_MARK_HIT = 2;

// BroadsideFleet.cs Lengths: dreadnought, cruiser, frigate, corvette, skiff.
const SHIP_LENGTHS: readonly number[] = [5, 4, 3, 3, 2];
const SHIP_COUNT = SHIP_LENGTHS.length;
const SEATS = 2;
const NO_SHIP = -1;
const OPEN = "0";
const MISS = String(BROADSIDE_MARK_MISS);
const HIT = String(BROADSIDE_MARK_HIT);

export interface BroadsideState {
  placing: boolean;
  placeStartedAt: number;
  fleets: FleetShip[][];
  marks: string[];
  missed: number[];
  firstSeat: number;
  turnSeat: number;
  turnStartedAt: number;
  lastSeat: number;
  lastCell: number;
  lastResult: string;
  lastShip: number;
  shotCount: number;
  lastKind: string;
  endKind: string;
  winnerSeat: number;
}

function shipCells(ship: FleetShip): number[] {
  const cells: number[] = [];
  const column = ship.cell % BROADSIDE_SIZE;
  const row = Math.floor(ship.cell / BROADSIDE_SIZE);
  for (let step = 0; step < SHIP_LENGTHS[ship.ship]; step++) {
    cells.push(ship.across ? row * BROADSIDE_SIZE + column + step : (row + step) * BROADSIDE_SIZE + column);
  }

  return cells;
}

// BroadsideFleet.Fits: the bow is the top-left cell and the hull runs right (across) or down.
function fits(ship: number, cell: number, across: boolean): boolean {
  const length = SHIP_LENGTHS[ship];
  const column = cell % BROADSIDE_SIZE;
  const row = Math.floor(cell / BROADSIDE_SIZE);
  return across ? column + length <= BROADSIDE_SIZE : row + length <= BROADSIDE_SIZE;
}

function occupancy(fleet: readonly FleetShip[]): number[] {
  const occupant = new Array<number>(BROADSIDE_CELL_COUNT).fill(NO_SHIP);
  for (const ship of fleet) {
    for (const cell of shipCells(ship)) {
      occupant[cell] = ship.ship;
    }
  }

  return occupant;
}

// BroadsideFleet.CanPlace only refuses overlap, so ships may touch.
function validFleet(fleet: FleetShip[] | null): FleetShip[] | null {
  if (!Array.isArray(fleet) || fleet.length !== SHIP_COUNT) {
    return null;
  }

  const ordered: (FleetShip | null)[] = new Array<FleetShip | null>(SHIP_COUNT).fill(null);
  const occupied = new Array<boolean>(BROADSIDE_CELL_COUNT).fill(false);
  for (const entry of fleet) {
    if (typeof entry !== "object" || entry === null) {
      return null;
    }

    const { ship, cell, across } = entry;
    if (!Number.isInteger(ship) || ship < 0 || ship >= SHIP_COUNT || ordered[ship] !== null
      || !Number.isInteger(cell) || cell < 0 || cell >= BROADSIDE_CELL_COUNT
      || typeof across !== "boolean" || !fits(ship, cell, across)) {
      return null;
    }

    const normalized: FleetShip = { ship, cell, across };
    for (const covered of shipCells(normalized)) {
      if (occupied[covered]) {
        return null;
      }

      occupied[covered] = true;
    }

    ordered[ship] = normalized;
  }

  const result: FleetShip[] = [];
  for (const ship of ordered) {
    if (ship === null) {
      return null;
    }

    result.push(ship);
  }

  return result;
}

// Picks uniformly among every free spot for each ship in turn, so it always terminates; a greedy fleet of
// 17 cells can never box the next ship out of a 10x10 board.
function randomFleet(random: Random): FleetShip[] {
  const fleet: FleetShip[] = [];
  const occupied = new Array<boolean>(BROADSIDE_CELL_COUNT).fill(false);
  for (let ship = 0; ship < SHIP_COUNT; ship++) {
    const candidates: FleetShip[] = [];
    for (let cell = 0; cell < BROADSIDE_CELL_COUNT; cell++) {
      for (const across of [true, false]) {
        if (!fits(ship, cell, across)) {
          continue;
        }

        const candidate: FleetShip = { ship, cell, across };
        if (shipCells(candidate).every((covered) => !occupied[covered])) {
          candidates.push(candidate);
        }
      }
    }

    const chosen = candidates[randomInt(random, candidates.length)];
    for (const covered of shipCells(chosen)) {
      occupied[covered] = true;
    }

    fleet.push(chosen);
  }

  return fleet;
}

function finish(state: BroadsideState, endKind: string, winnerSeat: number): void {
  state.endKind = endKind;
  state.winnerSeat = winnerSeat;
  state.turnSeat = -1;
}

function live(state: BroadsideState): boolean {
  return state.endKind.length === 0;
}

function sunkShips(state: BroadsideState, seat: number): FleetShip[] {
  const marks = state.marks[seat];
  return state.fleets[seat].filter((ship) => shipCells(ship).every((cell) => marks[cell] === HIT));
}

function beginFiring(state: BroadsideState, now: number): void {
  state.placing = false;
  state.turnSeat = state.firstSeat;
  state.turnStartedAt = now;
}

function place(state: BroadsideState, seat: number, fleet: FleetShip[] | null, now: number): string {
  if (!state.placing || state.fleets[seat].length > 0) {
    return Reason.invalid;
  }

  const valid = validFleet(fleet);
  if (valid === null) {
    return Reason.invalid;
  }

  state.fleets[seat] = valid;
  state.lastKind = "place";
  if (state.fleets.every((placed) => placed.length > 0)) {
    beginFiring(state, now);
  }

  return Reason.applied;
}

// lastSeat, lastCell, lastResult and lastShip only ever describe the latest shot: the table replays it
// from them and, on a rejoin or a skipped frame, pins the last-shot marker on the board of 1 - lastSeat
// (OnlineBroadsideTable.cs ResetRound and Settle), so a timeout or resign must leave them alone.
function fire(state: BroadsideState, seat: number, cell: number, now: number): string {
  if (state.placing) {
    return Reason.invalid;
  }

  if (seat !== state.turnSeat) {
    return Reason.notYourTurn;
  }

  const target = 1 - seat;
  const marks = state.marks[target];
  if (!Number.isInteger(cell) || cell < 0 || cell >= BROADSIDE_CELL_COUNT || marks[cell] !== OPEN) {
    return Reason.invalid;
  }

  const ship = occupancy(state.fleets[target])[cell];
  state.marks[target] = marks.slice(0, cell) + (ship === NO_SHIP ? MISS : HIT) + marks.slice(cell + 1);
  state.missed[seat] = 0;
  state.shotCount++;
  state.lastSeat = seat;
  state.lastCell = cell;
  state.lastKind = "fire";
  state.lastShip = NO_SHIP;
  state.lastResult = "miss";
  if (ship !== NO_SHIP) {
    const hull = state.fleets[target][ship];
    const sunk = shipCells(hull).every((covered) => state.marks[target][covered] === HIT);
    state.lastResult = sunk ? "sunk" : "hit";
    state.lastShip = sunk ? ship : NO_SHIP;
  }

  if (sunkShips(state, target).length === SHIP_COUNT) {
    finish(state, "fleet", seat);
    return Reason.applied;
  }

  // BroadsideBoard.Fire hands the turn over after every shot, hit or not.
  state.turnSeat = target;
  state.turnStartedAt = now;
  return Reason.applied;
}

export const broadside: GameEngine<BroadsideState> = {
  kind: BROADSIDE_KIND,
  maxSeats: SEATS,

  startOption(): number {
    return 0;
  },

  start(_seatCount: number, _option: number, random: Random, now: number): BroadsideState {
    return {
      placing: true,
      placeStartedAt: now,
      fleets: [[], []],
      marks: [OPEN.repeat(BROADSIDE_CELL_COUNT), OPEN.repeat(BROADSIDE_CELL_COUNT)],
      missed: [0, 0],
      firstSeat: randomInt(random, SEATS),
      turnSeat: -1,
      turnStartedAt: now,
      lastSeat: -1,
      lastCell: -1,
      lastResult: "",
      lastShip: NO_SHIP,
      shotCount: 0,
      lastKind: "",
      endKind: "",
      winnerSeat: -1,
    };
  },

  act(state: BroadsideState, seat: number, action: GameAction, _random: Random, now: number): string {
    if (!live(state) || seat < 0 || seat >= SEATS) {
      return Reason.invalid;
    }

    if (action.action === "resign") {
      state.lastKind = "resign";
      finish(state, "resign", 1 - seat);
      return Reason.applied;
    }

    if (action.action === "place") {
      return place(state, seat, action.fleet, now);
    }

    if (action.action === "fire") {
      return fire(state, seat, action.cell, now);
    }

    return Reason.invalid;
  },

  claim(state: BroadsideState, random: Random, now: number): boolean {
    if (!live(state) || now < this.deadline(state)) {
      return false;
    }

    if (state.placing) {
      for (let seat = 0; seat < SEATS; seat++) {
        if (state.fleets[seat].length === 0) {
          state.fleets[seat] = randomFleet(random);
        }
      }

      state.lastKind = "autoplace";
      beginFiring(state, now);
      return true;
    }

    const mover = state.turnSeat;
    state.missed[mover]++;
    state.lastKind = "timeout";
    if (state.missed[mover] >= BROADSIDE_MISSED_LIMIT) {
      finish(state, "timeout", 1 - mover);
      return true;
    }

    state.turnSeat = 1 - mover;
    state.turnStartedAt = now;
    return true;
  },

  forfeit(state: BroadsideState, seat: number): void {
    if (live(state)) {
      finish(state, "desertion", 1 - seat);
    }
  },

  deadline(state: BroadsideState): number {
    if (!live(state)) {
      return 0;
    }

    return state.placing
      ? state.placeStartedAt + BROADSIDE_PLACE_SECONDS * 1000
      : state.turnStartedAt + BROADSIDE_TURN_SECONDS * 1000;
  },

  winner(state: BroadsideState): number {
    return state.winnerSeat;
  },

  finished(state: BroadsideState): boolean {
    return !live(state);
  },

  // A finished round lists every ship in sunk: the table draws an enemy hull it was never told about only
  // from that list, and fades the ones its marks do not show as sunk (OnlineBroadsideTable.cs DrawBoard,
  // revealed = !live).
  view(state: BroadsideState | null, room: RoomView): Record<string, unknown> {
    const players = seatPlayers(room).map((player, index) => {
      if (state === null || index >= SEATS) {
        return { ...player, missed: 0, ready: false, marks: [], sunk: [] };
      }

      const marks: number[] = [];
      for (const mark of state.marks[index]) {
        marks.push(Number(mark));
      }

      const sunk = live(state) ? sunkShips(state, index) : state.fleets[index];
      return {
        ...player,
        missed: state.missed[index],
        ready: state.fleets[index].length > 0,
        marks,
        sunk: sunk.map((ship) => ({ ship: ship.ship, cell: ship.cell, across: ship.across })),
      };
    });

    return {
      roundIndex: room.round,
      hostUserId: room.hostId,
      players,
      placing: state?.placing ?? false,
      turnSeat: state?.turnSeat ?? -1,
      turnSeconds: state?.placing === true ? BROADSIDE_PLACE_SECONDS : BROADSIDE_TURN_SECONDS,
      lastSeat: state?.lastSeat ?? -1,
      lastCell: state?.lastCell ?? -1,
      lastResult: state?.lastResult ?? "",
      lastShip: state?.lastShip ?? NO_SHIP,
      shotCount: state?.shotCount ?? 0,
      actionCount: room.actionCount,
      lastKind: state?.lastKind ?? "",
      endKind: state?.endKind ?? "",
      winnerSeat: state?.winnerSeat ?? -1,
    };
  },

  secret(state: BroadsideState, seat: number, room: RoomView): Secret | null {
    if (seat < 0 || seat >= SEATS) {
      return null;
    }

    return {
      eventKind: BROADSIDE_FLEET_EVENT,
      payload: {
        seat,
        ships: state.fleets[seat].map((ship) => ({ ship: ship.ship, cell: ship.cell, across: ship.across })),
        actionCount: room.actionCount,
      },
    };
  },
};
