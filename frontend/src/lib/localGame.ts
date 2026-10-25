import { Chess, type Square } from 'chess.js'
import type { CoordinateMove } from './chess'

// Независимая от React логика, которую можно тестировать без браузера.
// Вся история сохраняется: одного FEN недостаточно для PGN и проверки повторения позиций.
export function replayGame(moves: CoordinateMove[]) {
  const chess = new Chess()
  for (const move of moves) chess.move({ ...move, from: move.from as Square, to: move.to as Square })
  return chess
}

export function gameResult(chess: Chess) {
  // После матового хода очередь у проигравшей стороны.
  if (chess.isCheckmate()) return chess.turn() === 'w' ? '0-1' : '1-0'
  if (chess.isDraw()) return '1/2-1/2'
  // Стандартный PGN-маркер незавершённой партии.
  return '*'
}

// PGN содержит теги и SAN-ходы (например Nf3, O-O), а не только координаты полей.
export function exportGame(moves: CoordinateMove[], date: Date) {
  const chess = replayGame(moves)
  chess.setHeader('Event', 'Ручная партия ChessTrainer')
  chess.setHeader('Site', 'ChessTrainer — локально')
  chess.setHeader('Date', [date.getFullYear(), String(date.getMonth() + 1).padStart(2, '0'), String(date.getDate()).padStart(2, '0')].join('.'))
  chess.setHeader('Round', '-')
  chess.setHeader('White', 'Белые')
  chess.setHeader('Black', 'Чёрные')
  chess.setHeader('Result', gameResult(chess))
  return chess.pgn({ maxWidth: 80 })
}
