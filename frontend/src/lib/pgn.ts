import { Chess } from 'chess.js'
import type { ChessGame } from '../types'

// Клиентский preview PGN. При подключении API каноническая валидация будет на backend.
export function parsePgn(text: string, fileName: string): ChessGame {
  const chess = new Chess()

  try {
    chess.loadPgn(text)
  } catch {
    throw new Error('PGN повреждён или содержит недопустимые ходы.')
  }

  const tags = chess.getHeaders()
  const moves = chess.history()
  if (moves.length === 0) throw new Error('В PGN не найдено ни одного хода.')
  const result = tags.Result ?? '*'

  return {
    id: `pgn-${crypto.randomUUID()}`,
    source: 'PGN',
    white: tags.White ?? 'Белые',
    black: tags.Black ?? 'Чёрные',
    whiteRating: parseRating(tags.WhiteElo),
    blackRating: parseRating(tags.BlackElo),
    result: result === '1/2-1/2' ? '½–½' : result.replace('-', '–'),
    playedAt: formatDate(tags.Date),
    timeControl: tags.TimeControl ?? 'Не указан',
    opening: tags.Opening ?? fileName.replace(/\.pgn$/i, ''),
    moves,
    pgn: text,
  }
}

function parseRating(value: string | undefined) {
  if (!value) return undefined
  const rating = Number.parseInt(value, 10)
  return Number.isFinite(rating) ? rating : undefined
}

function formatDate(value: string | undefined) {
  if (!value) return 'Дата не указана'
  return value.replaceAll('.', '/')
}
