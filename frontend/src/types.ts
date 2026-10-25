export type GameSource = 'Chess.com' | 'PGN' | 'Локально'

export interface ChessGame {
  id: string
  source: GameSource
  white: string
  black: string
  whiteRating?: number
  blackRating?: number
  result: string
  playedAt: string
  timeControl: string
  opening: string
  moves: string[]
  pgn?: string
}

export interface Mistake {
  id: string
  ply: number
  move: string
  bestMove: string
  evaluationBefore: string
  evaluationAfter: string
  centipawnLoss: number
  category: string
  fen: string
  title: string
  explanation: {
    whyBad: string
    missed: string
    principle: string
  }
}
