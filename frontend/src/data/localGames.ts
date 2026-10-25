import type { ChessGame } from '../types'
import type { CoordinateMove } from '../lib/chess'
import { replayGame } from '../lib/localGame'
import { parsePgn } from '../lib/pgn'

const gamesKey = 'chess-trainer-local-games-v1'
const draftKey = 'chess-trainer-draft-v1'

// Локальный адаптер хранения, аналог простого инфраструктурного сервиса.
// Компоненты обращаются сюда; серверное сохранение позже подключается на этой границе.
export const localGames = {
  list(): ChessGame[] {
    try {
      // JSON из браузера не гарантирует тип: TypeScript не проверяет данные во время выполнения.
      const records: unknown = JSON.parse(localStorage.getItem(gamesKey) ?? '[]')
      if (!Array.isArray(records)) return []
      return records.flatMap((record) => {
        try {
          if (typeof record?.id !== 'string' || typeof record?.pgn !== 'string') return []
          return [{ ...parsePgn(record.pgn, 'Ручная партия.pgn'), id: record.id, source: 'Локально' as const }]
        } catch { return [] }
      })
    } catch { return [] }
  },
  save(id: string, pgn: string): ChessGame {
    const game: ChessGame = { ...parsePgn(pgn, 'Ручная партия.pgn'), id, source: 'Локально' }
    const games = this.list()
    // Upsert по id: новое сохранение заменяет прежнюю версию этой же партии.
    localStorage.setItem(gamesKey, JSON.stringify([game, ...games.filter((item) => item.id !== id)]))
    return game
  },
  readDraft(): { id: string; moves: CoordinateMove[] } {
    const empty = { id: `local-${crypto.randomUUID()}`, moves: [] as CoordinateMove[] }
    try {
      const record = JSON.parse(localStorage.getItem(draftKey) ?? 'null')
      // Совместимость со старым черновиком, где сохранялся только массив ходов.
      const moves = Array.isArray(record) ? record : record?.moves
      if (!Array.isArray(moves)) return empty
      // Воспроизведение одновременно проверяет, что последовательность ходов допустима.
      replayGame(moves)
      return { id: typeof record?.id === 'string' ? record.id : empty.id, moves }
    } catch { return empty }
  },
  writeDraft(moves: CoordinateMove[], id: string) {
    localStorage.setItem(draftKey, JSON.stringify({ id, moves }))
  },
}
