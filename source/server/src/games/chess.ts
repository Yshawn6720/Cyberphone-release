import { type GameAction, type GameEngine, type Random, Reason, type RoomView, randomInt, seatPlayers } from "./engine";

export const CHESS_KIND = "games.chess";
// The host hint promises "10 minutes on each clock" (L.cs:9250) and the launch changelog promised Fischer clocks
// (L.cs:4571), so every completed move earns a small increment on top of the ten-minute budget.
export const CHESS_CLOCK_MS = 10 * 60 * 1000;
export const CHESS_INCREMENT_MS = 5 * 1000;

const SEATS = 2;
const SIZE = 8;
const SQUARE_COUNT = SIZE * SIZE;
const BLACK_FLAG = 8;
const TYPE_MASK = 7;
const FIFTY_MOVE_PLIES = 100;
// ChessBoard.Reset (ChessBoard.cs:131): square 0 is a8 on black's back rank, and each square stores the packed
// piece byte of ChessPiece.Make (type | 8 for black) as one hex digit.
const INITIAL_BOARD = "cabdebac" + "9".repeat(SIZE) + "0".repeat(SIZE * 4) + "1".repeat(SIZE) + "42356324";

const PieceType = { pawn: 1, knight: 2, bishop: 3, rook: 4, queen: 5, king: 6 } as const;
const Castling = { whiteKingSide: 1, whiteQueenSide: 2, blackKingSide: 4, blackQueenSide: 8 } as const;
const MoveFlag = { none: 0, capture: 1, doublePush: 2, enPassant: 4, castleKing: 8, castleQueen: 16 } as const;
const PROMOTIONS: readonly number[] = [PieceType.queen, PieceType.rook, PieceType.bishop, PieceType.knight];

type Step = readonly [number, number];
const KNIGHT_STEPS: readonly Step[] = [[-2, -1], [-2, 1], [-1, -2], [-1, 2], [1, -2], [1, 2], [2, -1], [2, 1]];
const KING_STEPS: readonly Step[] = [[-1, -1], [-1, 0], [-1, 1], [0, -1], [0, 1], [1, -1], [1, 0], [1, 1]];
const ROOK_RAYS: readonly Step[] = [[-1, 0], [1, 0], [0, -1], [0, 1]];
const BISHOP_RAYS: readonly Step[] = [[-1, -1], [-1, 1], [1, -1], [1, 1]];

export interface ChessState {
  board: string;
  blackToMove: boolean;
  castling: number;
  enPassant: number;
  halfmoveClock: number;
  whiteSeat: number;
  whiteMsRemaining: number;
  blackMsRemaining: number;
  turnStartedAt: number;
  lastFrom: number;
  lastTo: number;
  inCheck: boolean;
  moveCount: number;
  lastKind: string;
  endKind: string;
  winnerSeat: number;
}

interface Position {
  squares: number[];
  blackToMove: boolean;
  castling: number;
  enPassant: number;
  halfmoveClock: number;
}

interface ChessMove {
  from: number;
  to: number;
  flags: number;
  promotion: number;
}

function piece(type: number, black: boolean): number {
  return type | (black ? BLACK_FLAG : 0);
}

function isColor(occupant: number, black: boolean): boolean {
  return occupant !== 0 && ((occupant & BLACK_FLAG) !== 0) === black;
}

function rowOf(square: number): number {
  return Math.floor(square / SIZE);
}

function shift(square: number, deltaColumn: number, deltaRow: number): number {
  const column = (square % SIZE) + deltaColumn;
  const row = rowOf(square) + deltaRow;
  if (column < 0 || column >= SIZE || row < 0 || row >= SIZE) {
    return -1;
  }

  return row * SIZE + column;
}

function squaresOf(board: string): number[] {
  const squares: number[] = [];
  for (const mark of board) {
    squares.push(Number.parseInt(mark, 16));
  }

  return squares;
}

function rayAttack(squares: number[], square: number, rays: readonly Step[], sliderType: number, byBlack: boolean): boolean {
  const slider = piece(sliderType, byBlack);
  const queen = piece(PieceType.queen, byBlack);
  for (const [deltaColumn, deltaRow] of rays) {
    let target = shift(square, deltaColumn, deltaRow);
    while (target >= 0) {
      const occupant = squares[target];
      if (occupant === slider || occupant === queen) {
        return true;
      }

      if (occupant !== 0) {
        break;
      }

      target = shift(target, deltaColumn, deltaRow);
    }
  }

  return false;
}

function isAttacked(squares: number[], square: number, byBlack: boolean): boolean {
  const knight = piece(PieceType.knight, byBlack);
  for (const [deltaColumn, deltaRow] of KNIGHT_STEPS) {
    const from = shift(square, deltaColumn, deltaRow);
    if (from >= 0 && squares[from] === knight) {
      return true;
    }
  }

  const king = piece(PieceType.king, byBlack);
  for (const [deltaColumn, deltaRow] of KING_STEPS) {
    const from = shift(square, deltaColumn, deltaRow);
    if (from >= 0 && squares[from] === king) {
      return true;
    }
  }

  const pawnRow = byBlack ? -1 : 1;
  const pawn = piece(PieceType.pawn, byBlack);
  const pawnLeft = shift(square, -1, pawnRow);
  const pawnRight = shift(square, 1, pawnRow);
  if ((pawnLeft >= 0 && squares[pawnLeft] === pawn) || (pawnRight >= 0 && squares[pawnRight] === pawn)) {
    return true;
  }

  return rayAttack(squares, square, ROOK_RAYS, PieceType.rook, byBlack)
    || rayAttack(squares, square, BISHOP_RAYS, PieceType.bishop, byBlack);
}

function kingAttacked(squares: number[], black: boolean): boolean {
  const king = squares.indexOf(piece(PieceType.king, black));
  return king >= 0 && isAttacked(squares, king, !black);
}

function addPawnTarget(moves: ChessMove[], from: number, to: number, flags: number, promotionRow: number): void {
  if (rowOf(to) !== promotionRow) {
    moves.push({ from, to, flags, promotion: 0 });
    return;
  }

  for (const promotion of PROMOTIONS) {
    moves.push({ from, to, flags, promotion });
  }
}

function addPawnMoves(position: Position, moves: ChessMove[], square: number): void {
  const squares = position.squares;
  const black = position.blackToMove;
  const forward = black ? 1 : -1;
  const startRow = black ? 1 : SIZE - 2;
  const promotionRow = black ? SIZE - 1 : 0;
  const single = shift(square, 0, forward);
  if (single >= 0 && squares[single] === 0) {
    addPawnTarget(moves, square, single, MoveFlag.none, promotionRow);
    if (rowOf(square) === startRow) {
      const doubleStep = shift(square, 0, forward * 2);
      if (doubleStep >= 0 && squares[doubleStep] === 0) {
        moves.push({ from: square, to: doubleStep, flags: MoveFlag.doublePush, promotion: 0 });
      }
    }
  }

  for (let side = -1; side <= 1; side += 2) {
    const target = shift(square, side, forward);
    if (target < 0) {
      continue;
    }

    if (isColor(squares[target], !black)) {
      addPawnTarget(moves, square, target, MoveFlag.capture, promotionRow);
      continue;
    }

    if (squares[target] === 0 && target === position.enPassant) {
      moves.push({ from: square, to: target, flags: MoveFlag.capture | MoveFlag.enPassant, promotion: 0 });
    }
  }
}

function addStepMoves(position: Position, moves: ChessMove[], square: number, steps: readonly Step[]): void {
  for (const [deltaColumn, deltaRow] of steps) {
    const target = shift(square, deltaColumn, deltaRow);
    if (target < 0) {
      continue;
    }

    const occupant = position.squares[target];
    if (isColor(occupant, position.blackToMove)) {
      continue;
    }

    moves.push({ from: square, to: target, flags: occupant !== 0 ? MoveFlag.capture : MoveFlag.none, promotion: 0 });
  }
}

function addRayMoves(position: Position, moves: ChessMove[], square: number, rays: readonly Step[]): void {
  for (const [deltaColumn, deltaRow] of rays) {
    let target = shift(square, deltaColumn, deltaRow);
    while (target >= 0) {
      const occupant = position.squares[target];
      if (occupant === 0) {
        moves.push({ from: square, to: target, flags: MoveFlag.none, promotion: 0 });
        target = shift(target, deltaColumn, deltaRow);
        continue;
      }

      if (!isColor(occupant, position.blackToMove)) {
        moves.push({ from: square, to: target, flags: MoveFlag.capture, promotion: 0 });
      }

      break;
    }
  }
}

function addCastleMoves(position: Position, moves: ChessMove[], square: number): void {
  const squares = position.squares;
  const black = position.blackToMove;
  const homeSquare = black ? 4 : 60;
  if (square !== homeSquare || isAttacked(squares, square, !black)) {
    return;
  }

  const kingRight = black ? Castling.blackKingSide : Castling.whiteKingSide;
  const queenRight = black ? Castling.blackQueenSide : Castling.whiteQueenSide;
  if ((position.castling & kingRight) !== 0 && squares[square + 1] === 0 && squares[square + 2] === 0
    && !isAttacked(squares, square + 1, !black) && !isAttacked(squares, square + 2, !black)) {
    moves.push({ from: square, to: square + 2, flags: MoveFlag.castleKing, promotion: 0 });
  }

  if ((position.castling & queenRight) !== 0 && squares[square - 1] === 0 && squares[square - 2] === 0
    && squares[square - 3] === 0 && !isAttacked(squares, square - 1, !black) && !isAttacked(squares, square - 2, !black)) {
    moves.push({ from: square, to: square - 2, flags: MoveFlag.castleQueen, promotion: 0 });
  }
}

function pseudoMoves(position: Position): ChessMove[] {
  const moves: ChessMove[] = [];
  for (let square = 0; square < SQUARE_COUNT; square++) {
    const occupant = position.squares[square];
    if (!isColor(occupant, position.blackToMove)) {
      continue;
    }

    switch (occupant & TYPE_MASK) {
      case PieceType.pawn:
        addPawnMoves(position, moves, square);
        break;
      case PieceType.knight:
        addStepMoves(position, moves, square, KNIGHT_STEPS);
        break;
      case PieceType.bishop:
        addRayMoves(position, moves, square, BISHOP_RAYS);
        break;
      case PieceType.rook:
        addRayMoves(position, moves, square, ROOK_RAYS);
        break;
      case PieceType.queen:
        addRayMoves(position, moves, square, ROOK_RAYS);
        addRayMoves(position, moves, square, BISHOP_RAYS);
        break;
      case PieceType.king:
        addStepMoves(position, moves, square, KING_STEPS);
        addCastleMoves(position, moves, square);
        break;
    }
  }

  return moves;
}

// ChessBoard.RightsForSquare (ChessBoard.cs:728): a move from or onto a rook's home corner spends that castling right.
function cornerRights(square: number): number {
  switch (square) {
    case 0:
      return Castling.blackQueenSide;
    case 7:
      return Castling.blackKingSide;
    case 56:
      return Castling.whiteQueenSide;
    case 63:
      return Castling.whiteKingSide;
    default:
      return 0;
  }
}

function applyMove(position: Position, move: ChessMove): void {
  const squares = position.squares;
  const moved = squares[move.from];
  const captured = squares[move.to];
  const type = moved & TYPE_MASK;
  const black = (moved & BLACK_FLAG) !== 0;
  squares[move.from] = 0;
  squares[move.to] = move.promotion !== 0 ? piece(move.promotion, black) : moved;
  if ((move.flags & MoveFlag.enPassant) !== 0) {
    squares[black ? move.to - SIZE : move.to + SIZE] = 0;
  }

  if ((move.flags & MoveFlag.castleKing) !== 0) {
    squares[move.to + 1] = 0;
    squares[move.to - 1] = piece(PieceType.rook, black);
  } else if ((move.flags & MoveFlag.castleQueen) !== 0) {
    squares[move.to - 2] = 0;
    squares[move.to + 1] = piece(PieceType.rook, black);
  }

  position.halfmoveClock = type === PieceType.pawn || captured !== 0 ? 0 : position.halfmoveClock + 1;
  position.enPassant = (move.flags & MoveFlag.doublePush) !== 0 ? (move.from + move.to) / 2 : -1;
  if (type === PieceType.king) {
    position.castling &= ~(black
      ? Castling.blackKingSide | Castling.blackQueenSide
      : Castling.whiteKingSide | Castling.whiteQueenSide);
  }

  position.castling &= ~(cornerRights(move.from) | cornerRights(move.to));
  position.blackToMove = !position.blackToMove;
}

function legalMoves(position: Position): ChessMove[] {
  const legal: ChessMove[] = [];
  for (const candidate of pseudoMoves(position)) {
    const next: Position = { ...position, squares: position.squares.slice() };
    applyMove(next, candidate);
    if (!kingAttacked(next.squares, position.blackToMove)) {
      legal.push(candidate);
    }
  }

  return legal;
}

// ChessBoard.HasInsufficientMaterial (ChessBoard.cs:406): bare kings, a single minor piece, or two bishops on the
// same square colour cannot force mate.
function insufficientMaterial(squares: number[]): boolean {
  let minorCount = 0;
  let bishopSquare = -1;
  let secondBishopSquare = -1;
  for (let square = 0; square < SQUARE_COUNT; square++) {
    const type = squares[square] & TYPE_MASK;
    if (type === 0 || type === PieceType.king) {
      continue;
    }

    if (type === PieceType.bishop) {
      minorCount++;
      if (bishopSquare < 0) {
        bishopSquare = square;
      } else {
        secondBishopSquare = square;
      }

      continue;
    }

    if (type === PieceType.knight) {
      minorCount++;
      continue;
    }

    return false;
  }

  if (minorCount <= 1) {
    return true;
  }

  if (minorCount !== 2 || secondBishopSquare < 0) {
    return false;
  }

  const bishopColor = (rowOf(bishopSquare) + (bishopSquare % SIZE)) & 1;
  return bishopColor === ((rowOf(secondBishopSquare) + (secondBishopSquare % SIZE)) & 1);
}

function live(state: ChessState): boolean {
  return state.endKind.length === 0;
}

function moverSeat(state: ChessState): number {
  return state.blackToMove ? 1 - state.whiteSeat : state.whiteSeat;
}

function deadline(state: ChessState): number {
  if (!live(state)) {
    return 0;
  }

  return state.turnStartedAt + (state.blackToMove ? state.blackMsRemaining : state.whiteMsRemaining);
}

function finish(state: ChessState, endKind: string, winnerSeat: number): void {
  state.endKind = endKind;
  state.winnerSeat = winnerSeat;
}

function stopClock(state: ChessState, now: number): void {
  const elapsed = Math.max(0, now - state.turnStartedAt);
  if (state.blackToMove) {
    state.blackMsRemaining = Math.max(0, state.blackMsRemaining - elapsed);
  } else {
    state.whiteMsRemaining = Math.max(0, state.whiteMsRemaining - elapsed);
  }

  state.turnStartedAt = now;
}

function flag(state: ChessState, now: number): void {
  const loser = moverSeat(state);
  stopClock(state, now);
  state.lastKind = "timeout";
  finish(state, "timeout", 1 - loser);
}

function move(state: ChessState, seat: number, action: GameAction, now: number): string {
  const position: Position = {
    squares: squaresOf(state.board),
    blackToMove: state.blackToMove,
    castling: state.castling,
    enPassant: state.enPassant,
    halfmoveClock: state.halfmoveClock,
  };
  let chosen: ChessMove | null = null;
  for (const candidate of legalMoves(position)) {
    // A promotion move needs the exact piece the picker sent (OnlineChessTable.cs:122); 0 only fits a plain move.
    if (candidate.from === action.from && candidate.to === action.to && candidate.promotion === action.color) {
      chosen = candidate;
      break;
    }
  }

  if (chosen === null) {
    return Reason.invalid;
  }

  stopClock(state, now);
  if (state.blackToMove) {
    state.blackMsRemaining += CHESS_INCREMENT_MS;
  } else {
    state.whiteMsRemaining += CHESS_INCREMENT_MS;
  }

  applyMove(position, chosen);
  let board = "";
  for (const occupant of position.squares) {
    board += occupant.toString(16);
  }

  state.board = board;
  state.blackToMove = position.blackToMove;
  state.castling = position.castling;
  state.enPassant = position.enPassant;
  state.halfmoveClock = position.halfmoveClock;
  state.lastFrom = chosen.from;
  state.lastTo = chosen.to;
  state.moveCount++;
  state.lastKind = "move";
  state.inCheck = kingAttacked(position.squares, position.blackToMove);
  // Same precedence as ChessBoard.Evaluate (ChessBoard.cs:370): mate or stalemate first, then fifty moves, then material.
  if (legalMoves(position).length === 0) {
    finish(state, state.inCheck ? "checkmate" : "stalemate", state.inCheck ? seat : -1);
  } else if (position.halfmoveClock >= FIFTY_MOVE_PLIES) {
    finish(state, "fifty", -1);
  } else if (insufficientMaterial(position.squares)) {
    finish(state, "material", -1);
  }

  return Reason.applied;
}

export const chess: GameEngine<ChessState> = {
  kind: CHESS_KIND,
  maxSeats: SEATS,

  startOption(): number {
    return 0;
  },

  start(_seatCount: number, _option: number, random: Random, now: number): ChessState {
    return {
      board: INITIAL_BOARD,
      blackToMove: false,
      castling: Castling.whiteKingSide | Castling.whiteQueenSide | Castling.blackKingSide | Castling.blackQueenSide,
      enPassant: -1,
      halfmoveClock: 0,
      whiteSeat: randomInt(random, SEATS),
      whiteMsRemaining: CHESS_CLOCK_MS,
      blackMsRemaining: CHESS_CLOCK_MS,
      turnStartedAt: now,
      lastFrom: -1,
      lastTo: -1,
      inCheck: false,
      moveCount: 0,
      lastKind: "",
      endKind: "",
      winnerSeat: -1,
    };
  },

  act(state: ChessState, seat: number, action: GameAction, _random: Random, now: number): string {
    if (!live(state) || seat < 0 || seat >= SEATS || (action.action !== "move" && action.action !== "resign")) {
      return Reason.invalid;
    }

    if (now >= deadline(state)) {
      flag(state, now);
      return Reason.applied;
    }

    if (action.action === "resign") {
      stopClock(state, now);
      state.lastKind = "resign";
      finish(state, "resign", 1 - seat);
      return Reason.applied;
    }

    return seat === moverSeat(state) ? move(state, seat, action, now) : Reason.notYourTurn;
  },

  claim(state: ChessState, _random: Random, now: number): boolean {
    if (!live(state) || now < deadline(state)) {
      return false;
    }

    flag(state, now);
    return true;
  },

  forfeit(state: ChessState, seat: number, now: number): void {
    if (!live(state)) {
      return;
    }

    stopClock(state, now);
    finish(state, "desertion", 1 - seat);
  },

  deadline(state: ChessState): number {
    return deadline(state);
  },

  winner(state: ChessState): number {
    return state.winnerSeat;
  },

  finished(state: ChessState): boolean {
    return !live(state);
  },

  view(state: ChessState | null, room: RoomView): Record<string, unknown> {
    return {
      roundIndex: room.round,
      hostUserId: room.hostId,
      players: seatPlayers(room),
      squares: state === null ? [] : squaresOf(state.board),
      blackToMove: state?.blackToMove ?? false,
      castling: state?.castling ?? 0,
      enPassant: state?.enPassant ?? -1,
      halfmoveClock: state?.halfmoveClock ?? 0,
      whiteSeat: state?.whiteSeat ?? -1,
      whiteMsRemaining: state?.whiteMsRemaining ?? CHESS_CLOCK_MS,
      blackMsRemaining: state?.blackMsRemaining ?? CHESS_CLOCK_MS,
      turnStartedAtUnixMs: state?.turnStartedAt ?? 0,
      lastFrom: state?.lastFrom ?? -1,
      lastTo: state?.lastTo ?? -1,
      inCheck: state?.inCheck ?? false,
      moveCount: state?.moveCount ?? 0,
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
