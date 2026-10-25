import type { ChessGame, Mistake } from '../types'

export const demoGames: ChessGame[] = [
  {
    id: 'demo-1',
    source: 'Chess.com',
    white: 'quiet_knight',
    black: 'tactical_fox',
    whiteRating: 1284,
    blackRating: 1311,
    result: '0–1',
    playedAt: 'Сегодня, 18:42',
    timeControl: '10+0',
    opening: 'Итальянская партия',
    moves: [
      'e4', 'e5', 'Nf3', 'Nc6', 'Bc4', 'Nf6', 'd3', 'Bc5', 'O-O', 'd6',
      'c3', 'O-O', 'Re1', 'a6', 'Bb3', 'Ba7', 'Nbd2', 'Re8', 'Nf1', 'h6',
      'Ng3', 'Be6', 'Bc2', 'd5', 'exd5', 'Bxd5', 'h3', 'Qd7', 'Nh4', 'Rad8',
      'Nhf5', 'Be6', 'Qf3', 'Bd5', 'Nxh6+', 'gxh6',
    ],
  },
  {
    id: 'demo-2',
    source: 'Chess.com',
    white: 'endgame_owl',
    black: 'quiet_knight',
    whiteRating: 1267,
    blackRating: 1292,
    result: '½–½',
    playedAt: 'Вчера, 21:15',
    timeControl: '15+10',
    opening: 'Ферзевый гамбит',
    moves: ['d4', 'd5', 'c4', 'e6', 'Nc3', 'Nf6', 'Bg5', 'Be7', 'e3', 'O-O'],
  },
  {
    id: 'demo-3',
    source: 'Chess.com',
    white: 'quiet_knight',
    black: 'pawnstorm77',
    whiteRating: 1276,
    blackRating: 1254,
    result: '1–0',
    playedAt: '28 сентября',
    timeControl: '10+5',
    opening: 'Итальянская партия',
    moves: ['e4', 'e5', 'Nf3', 'Nc6', 'Bc4', 'Bc5', 'c3', 'Nf6', 'd3', 'd6'],
  },
]

export const demoMistake: Mistake = {
  id: 'mistake-demo-1',
  ply: 35,
  move: 'Nxh6+?',
  bestMove: 'Be3',
  evaluationBefore: '+0.4',
  evaluationAfter: '−1.8',
  centipawnLoss: 221,
  category: 'Преждевременная жертва',
  title: 'Атака началась без достаточного числа фигур',
  fen: '3rr1k1/bppq1pp1/p1n2n1N/3bp3/8/2PP1QNP/PPB2PP1/R1B1R1K1 b - - 0 18',
  explanation: {
    whyBad:
      'Шах выглядит активно, но после ответа gxh6 конь исчезает, а продолжения атаки нет. Ферзь и слон пока не создают форсированных угроз королю.',
    missed:
      'После первого вынужденного ответа соперника нужно было проверить, есть ли следующий шах, взятие или прямая угроза. Здесь такой цепочки нет.',
    principle:
      'Перед жертвой назови как минимум два конкретных продолжения атаки, а не только первый эффектный ход.',
  },
}

export const initialFen = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1'
