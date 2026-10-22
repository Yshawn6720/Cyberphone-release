import { describe, expect, it } from "vitest";
import {
  BROADSIDE_CELL_COUNT,
  BROADSIDE_KIND,
  BROADSIDE_MISSED_LIMIT,
  BROADSIDE_PLACE_SECONDS,
  BROADSIDE_TURN_SECONDS,
  type BroadsideState,
  broadside,
} from "../src/games/broadside";
import type { FleetShip, GameAction, Random, RoomView } from "../src/games/engine";

const NOW = 1_000_000;

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

function action(fields: Partial<GameAction>): GameAction {
  return { action: "", card: -1, color: -1, from: -1, to: -1, column: -1, cell: -1, target: -1, fleet: null, ...fields };
}

function room(seatCount = 2, actionCount = 7): RoomView {
  const seats = [];
  for (let index = 0; index < seatCount; index++) {
    seats.push({ userId: `user${index}`, displayName: `Player ${index}`, away: false, wins: index });
  }

  return { round: 3, hostId: "user0", actionCount, seats };
}

// Ship n lies across row n * 2 from column 0, so every hull is on its own row.
function rowFleet(): FleetShip[] {
  return [0, 1, 2, 3, 4].map((ship) => ({ ship, cell: ship * 20, across: true }));
}

const LENGTHS = [5, 4, 3, 3, 2];

function hullCells(ship: number): number[] {
  const cells: number[] = [];
  for (let step = 0; step < LENGTHS[ship]; step++) {
    cells.push(ship * 20 + step);
  }

  return cells;
}

function started(): BroadsideState {
  return broadside.start(2, 0, mulberry32(1), NOW);
}

function firing(firstSeat = 0): BroadsideState {
  const state = started();
  state.firstSeat = firstSeat;
  expect(broadside.act(state, 0, action({ action: "place", fleet: rowFleet() }), mulberry32(2), NOW)).toBe("");
  expect(broadside.act(state, 1, action({ action: "place", fleet: rowFleet() }), mulberry32(2), NOW + 10)).toBe("");
  return state;
}

function fire(state: BroadsideState, seat: number, cell: number, now = NOW + 100): string {
  return broadside.act(state, seat, action({ action: "fire", cell }), mulberry32(3), now);
}

function players(state: BroadsideState | null, view = room()): Record<string, unknown>[] {
  return broadside.view(state, view).players as Record<string, unknown>[];
}

function fleetIsValid(fleet: unknown): boolean {
  const state = started();
  return broadside.act(state, 0, action({ action: "place", fleet: fleet as FleetShip[] }), mulberry32(4), NOW) === "";
}

describe("broadside start", () => {
  it("opens in placement with an empty board and a placement deadline", () => {
    const state = started();
    expect(broadside.kind).toBe(BROADSIDE_KIND);
    expect(broadside.maxSeats).toBe(2);
    expect(broadside.startOption(5)).toBe(0);
    expect(state.placing).toBe(true);
    expect(state.turnSeat).toBe(-1);
    expect(state.firstSeat === 0 || state.firstSeat === 1).toBe(true);
    expect(broadside.deadline(state)).toBe(NOW + BROADSIDE_PLACE_SECONDS * 1000);
    expect(broadside.finished(state)).toBe(false);
    expect(broadside.winner(state)).toBe(-1);
    const view = broadside.view(state, room());
    expect(view.placing).toBe(true);
    expect(view.turnSeconds).toBe(BROADSIDE_PLACE_SECONDS);
  });
});

describe("broadside fleet validation", () => {
  it("accepts a legal fleet in any order and touching hulls", () => {
    expect(fleetIsValid([...rowFleet()].reverse())).toBe(true);
    const touching: FleetShip[] = [
      { ship: 0, cell: 0, across: true },
      { ship: 1, cell: 10, across: true },
      { ship: 2, cell: 20, across: true },
      { ship: 3, cell: 30, across: true },
      { ship: 4, cell: 40, across: true },
    ];
    expect(fleetIsValid(touching)).toBe(true);
    const vertical: FleetShip[] = [
      { ship: 0, cell: 50, across: false },
      { ship: 1, cell: 1, across: false },
      { ship: 2, cell: 2, across: false },
      { ship: 3, cell: 9, across: false },
      { ship: 4, cell: 88, across: false },
    ];
    expect(fleetIsValid(vertical)).toBe(true);
  });

  it("refuses overlapping hulls", () => {
    const fleet = rowFleet();
    fleet[1] = { ship: 1, cell: 2, across: false };
    expect(fleetIsValid(fleet)).toBe(false);
  });

  it("refuses hulls off the board", () => {
    const across = rowFleet();
    across[0] = { ship: 0, cell: 6, across: true };
    expect(fleetIsValid(across)).toBe(false);
    const down = rowFleet();
    down[4] = { ship: 4, cell: 95, across: false };
    expect(fleetIsValid(down)).toBe(false);
    const outside = rowFleet();
    outside[4] = { ship: 4, cell: BROADSIDE_CELL_COUNT, across: true };
    expect(fleetIsValid(outside)).toBe(false);
    const negative = rowFleet();
    negative[4] = { ship: 4, cell: -1, across: true };
    expect(fleetIsValid(negative)).toBe(false);
  });

  it("refuses the wrong ship count", () => {
    expect(fleetIsValid(rowFleet().slice(0, 4))).toBe(false);
    expect(fleetIsValid([...rowFleet(), { ship: 4, cell: 98, across: false }])).toBe(false);
    expect(fleetIsValid(null)).toBe(false);
    expect(fleetIsValid([])).toBe(false);
  });

  it("refuses a duplicate or unknown ship and malformed entries", () => {
    const duplicate = rowFleet();
    duplicate[4] = { ship: 3, cell: 80, across: true };
    expect(fleetIsValid(duplicate)).toBe(false);
    const unknown = rowFleet();
    unknown[4] = { ship: 5, cell: 80, across: true };
    expect(fleetIsValid(unknown)).toBe(false);
    const fractional = rowFleet();
    fractional[4] = { ship: 4, cell: 80.5, across: true };
    expect(fleetIsValid(fractional)).toBe(false);
    expect(fleetIsValid([...rowFleet().slice(0, 4), { ship: 4, cell: 80, across: "yes" }])).toBe(false);
    expect(fleetIsValid([...rowFleet().slice(0, 4), null])).toBe(false);
  });
});

describe("broadside placement then firing", () => {
  it("waits for both fleets, refuses a second placement and firing early", () => {
    const state = started();
    state.firstSeat = 1;
    expect(fire(state, 0, 0)).toBe("invalid");
    expect(broadside.act(state, 0, action({ action: "place", fleet: rowFleet() }), mulberry32(5), NOW)).toBe("");
    expect(state.placing).toBe(true);
    expect(state.lastKind).toBe("place");
    expect(players(state).map((player) => player.ready)).toEqual([true, false]);
    expect(broadside.act(state, 0, action({ action: "place", fleet: rowFleet() }), mulberry32(5), NOW)).toBe("invalid");
    expect(broadside.act(state, 1, action({ action: "place", fleet: rowFleet() }), mulberry32(5), NOW + 500)).toBe("");
    expect(state.placing).toBe(false);
    expect(state.turnSeat).toBe(1);
    expect(broadside.deadline(state)).toBe(NOW + 500 + BROADSIDE_TURN_SECONDS * 1000);
    const view = broadside.view(state, room());
    expect(view.placing).toBe(false);
    expect(view.turnSeat).toBe(1);
    expect(view.turnSeconds).toBe(BROADSIDE_TURN_SECONDS);
    expect(broadside.act(state, 0, action({ action: "place", fleet: rowFleet() }), mulberry32(5), NOW)).toBe("invalid");
  });

  it("refuses unknown actions", () => {
    const state = firing();
    expect(broadside.act(state, 0, action({ action: "drop" }), mulberry32(5), NOW)).toBe("invalid");
  });
});

describe("broadside firing", () => {
  it("enforces turn order and alternates after a hit or a miss", () => {
    const state = firing(0);
    expect(fire(state, 1, 50)).toBe("not_your_turn");
    expect(fire(state, 0, 0)).toBe("");
    expect(state.lastResult).toBe("hit");
    expect(state.turnSeat).toBe(1);
    expect(fire(state, 0, 1)).toBe("not_your_turn");
    expect(fire(state, 1, 9)).toBe("");
    expect(state.lastResult).toBe("miss");
    expect(state.turnSeat).toBe(0);
  });

  it("refuses the same cell twice and cells off the board", () => {
    const state = firing(0);
    expect(fire(state, 0, 9)).toBe("");
    expect(fire(state, 1, 9)).toBe("");
    expect(fire(state, 0, 9)).toBe("invalid");
    expect(fire(state, 0, -1)).toBe("invalid");
    expect(fire(state, 0, BROADSIDE_CELL_COUNT)).toBe("invalid");
    expect(fire(state, 0, 2.5)).toBe("invalid");
    expect(state.turnSeat).toBe(0);
    expect(state.shotCount).toBe(2);
  });

  it("reports miss, hit and sunk with marks on the board that was shot", () => {
    const state = firing(0);
    expect(fire(state, 0, 9)).toBe("");
    expect(state).toMatchObject({ lastSeat: 0, lastCell: 9, lastResult: "miss", lastShip: -1, shotCount: 1, lastKind: "fire" });
    expect(fire(state, 1, 80)).toBe("");
    expect(state).toMatchObject({ lastSeat: 1, lastCell: 80, lastResult: "hit", lastShip: -1, shotCount: 2 });
    expect(fire(state, 0, 99)).toBe("");
    expect(fire(state, 1, 81)).toBe("");
    expect(state).toMatchObject({ lastSeat: 1, lastCell: 81, lastResult: "sunk", lastShip: 4, shotCount: 4 });

    const view = broadside.view(state, room());
    expect(view).toMatchObject({ lastSeat: 1, lastCell: 81, lastResult: "sunk", lastShip: 4, shotCount: 4 });
    const [first, second] = players(state);
    const firstMarks = first.marks as number[];
    const secondMarks = second.marks as number[];
    expect(firstMarks).toHaveLength(100);
    expect(firstMarks[80]).toBe(2);
    expect(firstMarks[81]).toBe(2);
    expect(firstMarks.filter((mark) => mark !== 0)).toHaveLength(2);
    expect(secondMarks[9]).toBe(1);
    expect(secondMarks[99]).toBe(1);
    expect(secondMarks.filter((mark) => mark !== 0)).toHaveLength(2);
    expect(first.sunk).toEqual([{ ship: 4, cell: 80, across: true }]);
    expect(second.sunk).toEqual([]);
  });

  it("wins by sinking the whole fleet", () => {
    const state = firing(0);
    const targets = [0, 1, 2, 3, 4].flatMap(hullCells);
    const spares = [1, 3].flatMap((row) => Array.from({ length: 10 }, (_, column) => row * 10 + column));
    for (let index = 0; index < targets.length; index++) {
      expect(fire(state, 0, targets[index])).toBe("");
      if (index === targets.length - 1) {
        break;
      }

      expect(fire(state, 1, spares[index])).toBe("");
      expect(state.lastResult).toBe("miss");
    }

    expect(state.lastResult).toBe("sunk");
    expect(state.lastShip).toBe(4);
    expect(broadside.finished(state)).toBe(true);
    expect(broadside.winner(state)).toBe(0);
    expect(state.endKind).toBe("fleet");
    expect(state.turnSeat).toBe(-1);
    expect(broadside.deadline(state)).toBe(0);
    expect(fire(state, 1, 50)).toBe("invalid");
    const [first, second] = players(state);
    expect(second.sunk).toHaveLength(5);
    expect(first.sunk).toEqual(rowFleet());
  });
});

describe("broadside timers", () => {
  it("auto-places missing fleets only after the placement deadline", () => {
    const state = started();
    state.firstSeat = 0;
    expect(broadside.act(state, 1, action({ action: "place", fleet: rowFleet() }), mulberry32(6), NOW)).toBe("");
    const deadline = broadside.deadline(state);
    expect(broadside.claim(state, mulberry32(7), deadline - 1)).toBe(false);
    expect(state.placing).toBe(true);
    expect(broadside.claim(state, mulberry32(7), deadline)).toBe(true);
    expect(state.placing).toBe(false);
    expect(state.lastKind).toBe("autoplace");
    expect(state.turnSeat).toBe(0);
    expect(state.fleets[1]).toEqual(rowFleet());
    const placed = state.fleets[0];
    const roundTrip = started();
    expect(broadside.act(roundTrip, 0, action({ action: "place", fleet: placed }), mulberry32(8), NOW)).toBe("");
    expect(broadside.deadline(state)).toBe(deadline + BROADSIDE_TURN_SECONDS * 1000);
  });

  it("auto-places a valid fleet for many seeds", () => {
    for (let seed = 0; seed < 200; seed++) {
      const state = broadside.start(2, 0, mulberry32(seed), NOW);
      expect(broadside.claim(state, mulberry32(seed), broadside.deadline(state))).toBe(true);
      for (const fleet of state.fleets) {
        expect(fleetIsValid(fleet)).toBe(true);
      }
    }
  });

  it("auto-places even with a degenerate random source", () => {
    const state = started();
    expect(broadside.claim(state, () => 0, broadside.deadline(state))).toBe(true);
    expect(fleetIsValid(state.fleets[0])).toBe(true);
    const high = started();
    expect(broadside.claim(high, () => 0.999999, broadside.deadline(high))).toBe(true);
    expect(fleetIsValid(high.fleets[1])).toBe(true);
  });

  it("counts a missed turn and passes it after the firing deadline", () => {
    const state = firing(0);
    const deadline = broadside.deadline(state);
    expect(broadside.claim(state, mulberry32(9), deadline - 1)).toBe(false);
    expect(broadside.claim(state, mulberry32(9), deadline)).toBe(true);
    expect(state.missed).toEqual([1, 0]);
    expect(state.turnSeat).toBe(1);
    expect(state.lastKind).toBe("timeout");
    expect(state.shotCount).toBe(0);
    expect(state.lastSeat).toBe(-1);
    expect(broadside.deadline(state)).toBe(deadline + BROADSIDE_TURN_SECONDS * 1000);
    expect(players(state).map((player) => player.missed)).toEqual([1, 0]);
    expect(fire(state, 1, 0, deadline + 10)).toBe("");
    expect(fire(state, 0, 0, deadline + 20)).toBe("");
    expect(state.missed).toEqual([0, 0]);
  });

  it("loses on time after consecutive missed turns", () => {
    const state = firing(0);
    for (let turn = 0; turn < BROADSIDE_MISSED_LIMIT - 1; turn++) {
      expect(broadside.claim(state, mulberry32(9), broadside.deadline(state))).toBe(true);
      expect(state.turnSeat).toBe(1);
      expect(fire(state, 1, turn * 10 + 9, broadside.deadline(state) - 1000)).toBe("");
    }

    expect(state.missed).toEqual([BROADSIDE_MISSED_LIMIT - 1, 0]);
    expect(broadside.claim(state, mulberry32(9), broadside.deadline(state))).toBe(true);
    expect(broadside.finished(state)).toBe(true);
    expect(state.endKind).toBe("timeout");
    expect(broadside.winner(state)).toBe(1);
    expect(broadside.deadline(state)).toBe(0);
    expect(broadside.claim(state, mulberry32(9), NOW * 10)).toBe(false);
  });
});

describe("broadside endings", () => {
  it("ends on resign during placement or firing", () => {
    const placing = started();
    expect(broadside.act(placing, 0, action({ action: "resign" }), mulberry32(10), NOW)).toBe("");
    expect(placing.endKind).toBe("resign");
    expect(broadside.winner(placing)).toBe(1);

    const state = firing(0);
    expect(fire(state, 0, 9)).toBe("");
    expect(broadside.act(state, 0, action({ action: "resign" }), mulberry32(10), NOW)).toBe("");
    expect(state.endKind).toBe("resign");
    expect(state.lastKind).toBe("resign");
    expect(broadside.winner(state)).toBe(1);
    expect(state.lastSeat).toBe(0);
    expect(state.lastCell).toBe(9);
    expect(broadside.act(state, 1, action({ action: "resign" }), mulberry32(10), NOW)).toBe("invalid");
  });

  it("ends on desertion with the other seat winning", () => {
    const state = firing(0);
    broadside.forfeit(state, 0, NOW);
    expect(broadside.finished(state)).toBe(true);
    expect(state.endKind).toBe("desertion");
    expect(broadside.winner(state)).toBe(1);
    broadside.forfeit(state, 1, NOW);
    expect(broadside.winner(state)).toBe(1);

    const placing = started();
    broadside.forfeit(placing, 1, NOW);
    expect(placing.endKind).toBe("desertion");
    expect(broadside.winner(placing)).toBe(0);
  });
});

describe("broadside view and secrets", () => {
  it("lists the roster in the lobby with neutral fields", () => {
    const view = broadside.view(null, room());
    expect(view).toEqual({
      roundIndex: 3,
      hostUserId: "user0",
      players: [
        { userId: "user0", displayName: "Player 0", seat: 0, away: false, wins: 0, missed: 0, ready: false, marks: [], sunk: [] },
        { userId: "user1", displayName: "Player 1", seat: 1, away: false, wins: 1, missed: 0, ready: false, marks: [], sunk: [] },
      ],
      placing: false,
      turnSeat: -1,
      turnSeconds: BROADSIDE_TURN_SECONDS,
      lastSeat: -1,
      lastCell: -1,
      lastResult: "",
      lastShip: -1,
      shotCount: 0,
      actionCount: 7,
      lastKind: "",
      endKind: "",
      winnerSeat: -1,
    });
  });

  it("gives extra seats beyond the round neutral values and hides live fleets", () => {
    const state = firing(0);
    const view = broadside.view(state, room(3, 12));
    expect(view.actionCount).toBe(12);
    const list = view.players as Record<string, unknown>[];
    expect(list).toHaveLength(3);
    expect(list[2]).toEqual({ userId: "user2", displayName: "Player 2", seat: 2, away: false, wins: 2, missed: 0, ready: false, marks: [], sunk: [] });
    expect(list[0].sunk).toEqual([]);
    expect(list[1].sunk).toEqual([]);
    expect(list[0].ready).toBe(true);
    expect(JSON.stringify(view)).not.toContain("fleets");
  });

  it("sends each seat its own fleet, empty until placed", () => {
    const state = started();
    const before = broadside.secret(state, 0, room(2, 4));
    expect(before).toEqual({ eventKind: "broadside.fleet", payload: { seat: 0, ships: [], actionCount: 4 } });
    const shuffled = [...rowFleet()].reverse();
    expect(broadside.act(state, 0, action({ action: "place", fleet: shuffled }), mulberry32(11), NOW)).toBe("");
    expect(broadside.secret(state, 0, room(2, 5))).toEqual({
      eventKind: "broadside.fleet",
      payload: { seat: 0, ships: rowFleet(), actionCount: 5 },
    });
    expect(broadside.secret(state, 1, room(2, 5))).toEqual({
      eventKind: "broadside.fleet",
      payload: { seat: 1, ships: [], actionCount: 5 },
    });
    expect(broadside.secret(state, 2, room(3, 5))).toBeNull();
  });

  it("keeps the state small enough for the room attachment", () => {
    const state = firing(0);
    expect(JSON.stringify(state).length).toBeLessThan(1500);
    expect(structuredClone(state)).toEqual(state);
  });
});
