export type Random = () => number;

export interface FleetShip {
  ship: number;
  cell: number;
  across: boolean;
}

export interface GameAction {
  action: string;
  card: number;
  color: number;
  from: number;
  to: number;
  column: number;
  cell: number;
  target: number;
  fleet: FleetShip[] | null;
}

export interface SeatView {
  userId: string;
  displayName: string;
  away: boolean;
  wins: number;
}

export interface RoomView {
  round: number;
  hostId: string;
  actionCount: number;
  seats: SeatView[];
}

export interface Secret {
  eventKind: string;
  payload: Record<string, unknown>;
}

export const Reason = {
  applied: "",
  notYourTurn: "not_your_turn",
  invalid: "invalid",
} as const;

// Engines are pure: the room hands them a seat count, the acting seat and the clock, and keeps the state they
// return inside the host's WebSocket attachment, so a state must stay a small plain object (no class instances,
// no Map or Set) and every random draw goes through the room's Random.
export interface GameEngine<State> {
  readonly kind: string;
  readonly maxSeats: number;
  startOption(option: number): number | null;
  start(seatCount: number, option: number, random: Random, now: number): State;
  act(state: State, seat: number, action: GameAction, random: Random, now: number): string;
  claim(state: State, random: Random, now: number): boolean;
  forfeit(state: State, seat: number, now: number): void;
  deadline(state: State): number;
  winner(state: State): number;
  finished(state: State): boolean;
  view(state: State | null, room: RoomView): Record<string, unknown>;
  secret(state: State, seat: number, room: RoomView): Secret | null;
}

export function seatPlayers(room: RoomView): Record<string, unknown>[] {
  return room.seats.map((seat, index) => ({
    userId: seat.userId,
    displayName: seat.displayName,
    seat: index,
    away: seat.away,
    wins: seat.wins,
  }));
}

export function randomInt(random: Random, bound: number): number {
  return Math.min(bound - 1, Math.floor(random() * bound));
}
