import type { ChessGame } from '../types'

interface GameCardProps {
  game: ChessGame
  onOpen: (game: ChessGame) => void
}

export function GameCard({ game, onOpen }: GameCardProps) {
  const resultClass = game.result === '1–0' ? 'win' : game.result === '0–1' ? 'loss' : 'draw'

  return (
    <button className="game-card" onClick={() => onOpen(game)} type="button">
      <div className="game-card__date">{game.playedAt}</div>
      <div className="game-card__players">
        <span>
          <i className="color-dot color-dot--white" />
          {game.white}
          {game.whiteRating && <small>{game.whiteRating}</small>}
        </span>
        <span>
          <i className="color-dot color-dot--black" />
          {game.black}
          {game.blackRating && <small>{game.blackRating}</small>}
        </span>
      </div>
      <div className="game-card__meta">
        <strong>{game.opening}</strong>
        <span>{game.timeControl} · {game.moves.length} полуходов</span>
      </div>
      <span className="source-tag">{game.source}</span>
      <div className={`result-badge result-badge--${resultClass}`}>{game.result}</div>
    </button>
  )
}
