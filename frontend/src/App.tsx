import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Chess, type Square } from 'chess.js'
import { ChessBoard } from './components/ChessBoard'
import { LocalGame } from './components/LocalGame'
import { localGames } from './data/localGames'
import { EvaluationBar } from './components/EvaluationBar'
import { SoundToggle } from './components/SoundToggle'
import { playBoardSound } from './lib/sounds'
import { GameCard } from './components/GameCard'
import { BackIcon } from './components/Icons'
import { UploadCard } from './components/UploadCard'
import { demoGames } from './data/mock'
import { createPosition, type CoordinateMove } from './lib/chess'
import { registerWebMcpTools } from './lib/webmcp'
import type { ChessGame } from './types'

type Page = 'games' | 'play' | 'review'
type ReviewTab = 'game' | 'analysis'
// Корневой компонент владеет навигацией и списком партий и передаёт их дочерним компонентам.
function App() {
// useState возвращает пару [значение, setter]. Setter запрашивает новый рендер.
  const [page, setPage] = useState<Page>('games')
  // Функция-инициализатор читает хранилище при создании компонента, а не при каждом рендере.
  const [games, setGames] = useState<ChessGame[]>(() => [...localGames.list(), ...demoGames])
  const [selectedGame, setSelectedGame] = useState<ChessGame>(demoGames[0])
  const [username, setUsername] = useState('')
  const [searching, setSearching] = useState(false)
  const [notice, setNotice] = useState('')

  function showGames() {
    setPage('games')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function showPlay() {
    setPage('play')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function openGame(game: ChessGame) {
    setSelectedGame(game)
    setPage('review')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  // Функциональный setter получает актуальный список; создаём новый массив вместо мутации.
  function handleUploaded(game: ChessGame) {
    setGames((current) => [game, ...current])
    setNotice(`Добавлена партия ${game.white} — ${game.black}`)
  }

  function handleChessCom(event: FormEvent) {
    event.preventDefault()
    if (!username.trim()) return
    setSearching(true)
    setNotice('')
    window.setTimeout(() => {
      setSearching(false)
      setNotice(`Показаны демо-партии для ${username.trim()}. API пока не подключён.`)
    }, 700)
  }

  // Необязательная интеграция с браузером. Возвращённая функция снимает регистрацию
  // перед повторным эффектом и при удалении компонента; [games] — зависимости эффекта.
  useEffect(() => registerWebMcpTools([
    {
      name: 'list_chess_games',
      title: 'Показать партии',
      description: 'Возвращает партии, которые сейчас доступны в ChessTrainer.',
      inputSchema: { type: 'object', properties: {}, additionalProperties: false },
      annotations: { readOnlyHint: true, untrustedContentHint: false },
      execute: () => games.map(({ id, white, black, result, playedAt }) => ({ id, white, black, result, playedAt })),
    },
    {
      name: 'open_chess_game',
      title: 'Открыть партию',
      description: 'Открывает вкладку партии по её идентификатору.',
      inputSchema: { type: 'object', properties: { gameId: { type: 'string' } }, required: ['gameId'], additionalProperties: false },
      annotations: { readOnlyHint: false, untrustedContentHint: false },
      execute: (input) => {
        const gameId = typeof input === 'object' && input !== null && 'gameId' in input ? String(input.gameId) : ''
        const game = games.find((candidate) => candidate.id === gameId)
        if (!game) throw new Error('Партия не найдена.')
        openGame(game)
        return { opened: true, gameId }
      },
    },
  ]), [games])

  return (
    <div className="app-shell">
      <Sidebar active={page === 'review' ? 'game' : page} onGames={showGames} onPlay={showPlay} />
      <div className="app-main">
        {/* Скрываем LocalGame, но не удаляем: state партии живёт при переключении вкладок. */}
        <div hidden={page !== 'play'}><LocalGame onSaved={(game) => setGames((current) => [game, ...current.filter((item) => item.id !== game.id)])} /></div>
        {/* Условный JSX выбирает экран. key у Review сбрасывает его state для другой партии. */}
        {page === 'games' ? (
          <GamesPage
            games={games}
            notice={notice}
            onChessCom={handleChessCom}
            onOpenGame={openGame}
            onUploaded={handleUploaded}
            onUsernameChange={setUsername}
            searching={searching}
            username={username}
          />
        ) : page === 'play' ? null : <Review key={selectedGame.id} game={selectedGame} onBack={showGames} />}
      </div>
    </div>
  )
}

function Sidebar({ active, onGames, onPlay }: { active: 'games' | 'play' | 'game'; onGames: () => void; onPlay: () => void }) {
  return (
    <aside className="sidebar">
      <button className="brand" type="button" onClick={onGames} aria-label="ChessTrainer — к партиям">
        <span className="brand__mark">♞</span><span>ChessTrainer</span>
      </button>
      <nav className="side-nav" aria-label="Основная навигация">
        <button className={active === 'games' ? 'is-active' : ''} onClick={onGames} type="button"><span className="nav-glyph">♟</span><span>Партии</span></button>
        <button className={active === 'play' ? 'is-active' : ''} onClick={onPlay} type="button"><span className="nav-glyph">▶</span><span>Играть</span></button>
        <button className={active === 'game' ? 'is-active' : ''} type="button"><span className="nav-glyph">▦</span><span>Разбор</span></button>
        <button disabled type="button" title="Появится позже"><span className="nav-glyph">◫</span><span>Тренировки</span><small>скоро</small></button>
        <button disabled type="button" title="Появится позже"><span className="nav-glyph">▥</span><span>Прогресс</span></button>
      </nav>
      <div className="sidebar__bottom">
        <button type="button"><span className="nav-glyph">⚙</span><span>Настройки</span></button>
        <div className="user-row"><span>Г</span><div><strong>Гость</strong><small>Локальный режим</small></div></div>
      </div>
    </aside>
  )
}

interface GamesPageProps {
  games: ChessGame[]
  username: string
  searching: boolean
  notice: string
  onUsernameChange: (value: string) => void
  onChessCom: (event: FormEvent) => void
  onUploaded: (game: ChessGame) => void
  onOpenGame: (game: ChessGame) => void
}

function GamesPage(props: GamesPageProps) {
  return (
    <main className="page games-page">
      <header className="page-header">
        <div><h1>Партии</h1><p>Открой партию или добавь новую для разбора.</p></div>
        <span className="connection-status"><i /> Backend не подключён · партии Chess.com — демо</span>
      </header>
      <section className="import-panel" aria-label="Добавить партии">
        <form className="username-import" onSubmit={props.onChessCom}>
          <div className="source-logo">♙</div>
          <div className="source-copy"><strong>Chess.com</strong><span>Последние партии по нику</span></div>
          <label className="username-field">
            <span className="sr-only">Имя пользователя Chess.com</span>
            <input value={props.username} onChange={(event) => props.onUsernameChange(event.target.value)} placeholder="Имя пользователя" />
            <button type="submit" disabled={props.searching || !props.username.trim()}>{props.searching ? <span className="spinner" /> : 'Найти'}</button>
          </label>
        </form>
        <UploadCard onUploaded={props.onUploaded} />
      </section>
      {props.notice && <div className="notice" role="status">{props.notice}</div>}
      <section className="games-panel">
        <header>
          <div><h2>Недавние</h2><span>{props.games.length} партии</span></div>
          <div className="list-filters" aria-label="Фильтр партий"><button className="is-active" type="button">Все</button><button type="button">Без разбора</button><button type="button">Разобраны</button></div>
        </header>
        <div className="games-table-head" aria-hidden="true"><span>Дата</span><span>Игроки</span><span>Партия</span><span>Источник</span><span>Результат</span></div>
        <div className="games-list">{props.games.map((game) => <GameCard game={game} onOpen={props.onOpenGame} key={game.id} />)}</div>
      </section>
    </main>
  )
}

// Props game/onBack приходят от App; выбранный полуход и пробный вариант принадлежат Review.
function Review({ game, onBack }: { game: ChessGame; onBack: () => void }) {
  const [tab, setTab] = useState<ReviewTab>('game')
  const [selectedPly, setSelectedPly] = useState(game.moves.length)
  const [variation, setVariation] = useState<CoordinateMove[]>([])
  // Позиция вычисляется из ходов. Не храним ещё одну копию FEN в state.
  const position = useMemo(() => createPosition(game.moves, selectedPly, variation), [game.moves, selectedPly, variation])
  // Ref позволяет зарегистрированному ранее WebMCP callback прочитать актуальный FEN.
  // Обычное замыкание иначе могло бы удерживать значение из старого рендера.
  const positionFenRef = useRef(position.fen)
  positionFenRef.current = position.fen
  const moveRows = useMemo(() => {
    const rows: Array<{ number: number; white?: string; black?: string }> = []
    for (let index = 0; index < game.moves.length; index += 2) rows.push({ number: index / 2 + 1, white: game.moves[index], black: game.moves[index + 1] })
    return rows
  }, [game.moves])
  function goToPly(ply: number) {
    setSelectedPly(ply)
    setVariation([])
  }

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.target instanceof HTMLInputElement) return
      if (event.key === 'ArrowLeft') goToPly(Math.max(0, selectedPly - 1))
      if (event.key === 'ArrowRight') goToPly(Math.min(game.moves.length, selectedPly + 1))
    }
    // Подписка на внешний источник событий требует симметричной отписки.
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [game.moves.length, selectedPly])

  function makeMove(from: string, to: string, promotion = 'q') {
    try {
      // Изолированная копия позиции: пробный ход не изменяет исходную партию.
      const testPosition = new Chess(positionFenRef.current)
      const move = testPosition.move({ from: from as Square, to: to as Square, promotion })
      setVariation((current) => [...current, { from: move.from, to: move.to, promotion: move.promotion }])
      playBoardSound(testPosition.isCheckmate() ? 'checkmate' : testPosition.isCheck() ? 'check' : move.captured ? 'capture' : 'move')
      return true
    } catch { return false }
  }

  function switchTab(nextTab: ReviewTab) {
    setTab(nextTab)
  }

  useEffect(() => registerWebMcpTools([
    {
      name: 'go_to_game_ply',
      title: 'Перейти к ходу',
      description: 'Показывает позицию после указанного полухода текущей партии.',
      inputSchema: { type: 'object', properties: { ply: { type: 'integer', minimum: 0, maximum: game.moves.length } }, required: ['ply'], additionalProperties: false },
      annotations: { readOnlyHint: false, untrustedContentHint: false },
      execute: (input) => {
        const ply = typeof input === 'object' && input !== null && 'ply' in input ? Number(input.ply) : Number.NaN
        if (!Number.isInteger(ply) || ply < 0 || ply > game.moves.length) throw new Error('Недопустимый номер полухода.')
        goToPly(ply)
        return { gameId: game.id, ply, move: ply === 0 ? null : game.moves[ply - 1] }
      },
    },
    {
      name: 'play_variation_move',
      title: 'Сделать ход в варианте',
      description: 'Делает легальный ход на текущей доске и добавляет его в пользовательский вариант.',
      inputSchema: { type: 'object', properties: { from: { type: 'string', pattern: '^[a-h][1-8]$' }, to: { type: 'string', pattern: '^[a-h][1-8]$' } }, required: ['from', 'to'], additionalProperties: false },
      annotations: { readOnlyHint: false, untrustedContentHint: false },
      execute: (input) => {
        const value = typeof input === 'object' && input !== null ? input as { from?: unknown; to?: unknown } : {}
        const from = String(value.from ?? '')
        const to = String(value.to ?? '')
        if (!/^[a-h][1-8]$/.test(from) || !/^[a-h][1-8]$/.test(to) || !makeMove(from, to)) throw new Error('Этот ход недопустим в текущей позиции.')
        return { gameId: game.id, from, to, accepted: true }
      },
    },
  ]), [game.id, game.moves])

  const moveLabel = variation.length > 0 ? `Вариант: ${position.variationSans.join(' ')}` : selectedPly === 0 ? 'Начальная позиция' : `Ход ${Math.ceil(selectedPly / 2)}${selectedPly % 2 === 0 ? '…' : '.'} ${game.moves[selectedPly - 1]}`

  return (
    <main className="review-page">
      <header className="review-topbar">
        <button className="back-button" onClick={onBack} type="button"><BackIcon /> Партии</button>
        <div className="review-title"><strong>{game.white} <small>{game.whiteRating}</small></strong><span>{game.result}</span><strong>{game.black} <small>{game.blackRating}</small></strong></div>
        <div className="review-meta"><span>{game.timeControl} · {game.opening}</span><SoundToggle /></div>
      </header>
      <div className="review-workspace">
        <section className="board-area">
          <PlayerBar name={game.black} rating={game.blackRating} clock="05:18" color="black" />
          <div className="board-with-evaluation"><EvaluationBar /><ChessBoard fen={position.fen} lastMove={position.lastMove} onMove={makeMove} /></div>
          <PlayerBar name={game.white} rating={game.whiteRating} clock="03:42" color="white" />
          <div className="board-toolbar">
            <button type="button" onClick={() => goToPly(0)} aria-label="В начало">⏮</button><button type="button" onClick={() => goToPly(Math.max(0, selectedPly - 1))} aria-label="Предыдущий ход">◀</button><span>{moveLabel}</span><button type="button" onClick={() => goToPly(Math.min(game.moves.length, selectedPly + 1))} aria-label="Следующий ход">▶</button><button type="button" onClick={() => goToPly(game.moves.length)} aria-label="В конец">⏭</button>
          </div>
          <div className="board-hint"><span><i /> {position.chess.turn() === 'w' ? 'Ход белых' : 'Ход чёрных'} · фигуры можно двигать</span>{variation.length > 0 && <button type="button" onClick={() => setVariation([])}>Сбросить вариант</button>}</div>
        </section>
        <aside className="game-panel">
          <div className="review-tabs" role="tablist" aria-label="Режим просмотра"><button className={tab === 'game' ? 'is-active' : ''} onClick={() => switchTab('game')} role="tab" type="button">Партия</button><button className={tab === 'analysis' ? 'is-active' : ''} onClick={() => switchTab('analysis')} role="tab" type="button">Анализ</button></div>
          {tab === 'game' ? <GameTab game={game} moveRows={moveRows} onSelectPly={goToPly} selectedPly={selectedPly} variationMoves={position.variationSans} /> : <div className="analysis-empty"><span>♜</span><h2>Анализ пока недоступен</h2><p>После подключения backend Stockfish проверит ходы и выделит ключевые ошибки. Тренер появится на следующем этапе.</p><button className="primary-button" disabled type="button">Начать анализ</button></div>}

        </aside>
      </div>
    </main>
  )
}

function PlayerBar({ name, rating, clock, color }: { name: string; rating?: number; clock: string; color: 'white' | 'black' }) {
  return <div className="player-bar"><span className={`player-avatar player-avatar--${color}`}>{color === 'white' ? '♙' : '♟'}</span><div><strong>{name}</strong><small>{rating ?? '—'}</small></div><time>{clock}</time></div>
}

interface MoveRow { number: number; white?: string; black?: string }

function GameTab({ game, moveRows, selectedPly, variationMoves, onSelectPly }: { game: ChessGame; moveRows: MoveRow[]; selectedPly: number; variationMoves: string[]; onSelectPly: (ply: number) => void }) {
  return (
    <div className="game-tab">
      <div className="opening-row"><div><small>Дебют</small><strong>{game.opening}</strong></div><span>{game.timeControl}</span></div>
      <div className="move-list" aria-label="Ходы партии">
        {moveRows.map((row) => <div className="move-row" key={row.number}><span>{row.number}.</span><button className={selectedPly === row.number * 2 - 1 ? 'is-selected' : ''} onClick={() => onSelectPly(row.number * 2 - 1)} type="button">{row.white}</button><button className={selectedPly === row.number * 2 ? 'is-selected' : ''} onClick={() => onSelectPly(row.number * 2)} type="button">{row.black ?? '—'}</button></div>)}
        {variationMoves.length > 0 && <div className="variation-line"><span>Вариант</span><strong>{variationMoves.join(' ')}</strong></div>}
      </div>
      <div className="game-tab__footer"><button className="primary-button" disabled type="button">Анализировать партию</button><small>Ожидает подключения backend и Stockfish</small></div>
    </div>
  )
}

export default App
