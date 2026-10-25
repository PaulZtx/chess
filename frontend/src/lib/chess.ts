import { Chess, type Square } from 'chess.js'

export interface CoordinateMove {
  from: string
  to: string
  promotion?: string
}

export function createCoordinatePosition(moves: CoordinateMove[]) {
  const chess = new Chess()
  const sans: string[] = []
  let lastMove: [string, string] | null = null

  for (const candidate of moves) {
    try {
      const move = chess.move({
        from: candidate.from as Square,
        to: candidate.to as Square,
        promotion: candidate.promotion ?? 'q',
      })
      sans.push(move.san)
      lastMove = [move.from, move.to]
    } catch {
      break
    }
  }

  return { chess, fen: chess.fen(), lastMove, sans }
}

// Для разбора воспроизводим первые ply полуходов, затем пользовательский пробный вариант.
// SAN — запись хода вроде Nf3; FEN — строка текущей позиции и её служебных параметров.
export function createPosition(gameMoves: string[], ply: number, variation: CoordinateMove[] = []) {
  const chess = new Chess()
  let lastMove: [string, string] | null = null
  let validPly = 0

  for (let index = 0; index < Math.min(ply, gameMoves.length); index += 1) {
    try {
      const move = chess.move(gameMoves[index])
      lastMove = [move.from, move.to]
      validPly = index + 1
    } catch {
      break
    }
  }

  const variationSans: string[] = []
  for (const candidate of variation) {
    try {
      const move = chess.move({
        from: candidate.from as Square,
        to: candidate.to as Square,
        promotion: candidate.promotion ?? 'q',
      })
      lastMove = [move.from, move.to]
      variationSans.push(move.san)
    } catch {
      break
    }
  }

  return { chess, fen: chess.fen(), lastMove, validPly, variationSans }
}
