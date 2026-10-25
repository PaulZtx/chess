import { useEffect, useMemo, useRef, useState } from 'react'
import type { Square } from 'chess.js'
import { ChessBoard } from './ChessBoard'
import { EvaluationBar } from './EvaluationBar'
import { SoundToggle } from './SoundToggle'
import { playBoardSound } from '../lib/sounds'
import { localGames } from '../data/localGames'
import { exportGame, replayGame } from '../lib/localGame'
import type { CoordinateMove } from '../lib/chess'
import type { ChessGame } from '../types'

// onSaved — callback родителя (по смыслу Action<ChessGame>), а не HTTP-запрос.
export function LocalGame({ onSaved }: { onSaved: (game: ChessGame) => void }) {
  // Ленивое чтение черновика; дальнейшие изменения UI проходят через state.
  const [draft] = useState(() => localGames.readDraft())
  const [moves, setMoves] = useState<CoordinateMove[]>(draft.moves)
  const [notice, setNotice] = useState('')
  const [orientation, setOrientation] = useState<'white' | 'black'>('white')
  const [confirmReset, setConfirmReset] = useState(false)
  // Ref сохраняет значение между рендерами, но изменение .current само не обновляет UI.
  // id связывает повторные сохранения одной партии. movesRef нужен обработчикам ходов.
  const gameId = useRef(draft.id)
  const movesRef = useRef(moves)
  movesRef.current = moves
  // Единственный источник текущей партии — moves. Позицию и SAN получаем воспроизведением.
  // useMemo позволяет переиспользовать расчёт, пока ссылка на moves не изменилась.
  const chess = useMemo(() => replayGame(moves), [moves])
  const history = chess.history({ verbose: true })
  const last = history.at(-1)
  const moveRows = history.filter((_, index) => index % 2 === 0).map((move, index) => ({ number: index + 1, white: move.san, black: history[index * 2 + 1]?.san }))

  // Запись в браузер — побочный эффект: выполняем после обновления UI, когда изменились moves.
  useEffect(() => {
    try { localGames.writeDraft(moves, gameId.current) }
    catch { setNotice('Не удалось сохранить черновик в браузере. Скачайте PGN, чтобы не потерять ходы.') }
  }, [moves])

  // Доска предлагает ход, а этот обработчик принимает его или возвращает false.
  function makeMove(from: string, to: string, promotion?: string) {
    try {
      const current = replayGame(movesRef.current)
      if (current.isGameOver()) return false
      const move = current.move({ from: from as Square, to: to as Square, promotion })
      // Spread создаёт новый массив. moves.push(...) не уведомил бы React об изменении.
      const next = [...movesRef.current, { from: move.from, to: move.to, promotion: move.promotion }]
      // Ref обновляем сразу: следующий callback увидит новый ход ещё до следующего рендера.
      movesRef.current = next
      setMoves(next)
      setNotice('')
      setConfirmReset(false)
      // Более важное событие заменяет обычный звук: мат > шах > взятие > ход.
      playBoardSound(current.isCheckmate() ? 'checkmate' : current.isCheck() ? 'check' : move.captured ? 'capture' : 'move')
      return true
    } catch { return false }
  }

  // Один элемент — полуход одной стороны. Отмена удаляет его, позиция пересчитывается.
  function undo() {
    const next = movesRef.current.slice(0, -1)
    movesRef.current = next
    setMoves(next)
    setNotice('')
    setConfirmReset(false)
  }

  function newGame() {
    if (moves.length && !confirmReset) { setConfirmReset(true); return }
    movesRef.current = []
    setMoves([])
    gameId.current = `local-${crypto.randomUUID()}`
    setNotice('')
    setConfirmReset(false)
  }

  // Адаптер хранит PGN локально; уведомляем App, чтобы обновить список партий.
  function save() {
    try {
      const game = localGames.save(gameId.current, exportGame(movesRef.current, new Date()))
      onSaved(game)
      setNotice('Партия сохранена в этом браузере и добавлена в «Партии». Серверное сохранение пока не подключено.')
    } catch { setNotice('Не удалось сохранить партию в браузере. Скачайте PGN.') }
  }

  function download() {
    // Blob — файл в памяти. Временный URL позволяет браузеру скачать его без сервера.
    const url = URL.createObjectURL(new Blob([exportGame(movesRef.current, new Date())], { type: 'application/x-chess-pgn;charset=utf-8' }))
    const link = document.createElement('a')
    link.href = url
    link.download = 'chess-trainer.pgn'
    document.body.append(link)
    link.click()
    link.remove()
    // После запуска скачивания освобождаем ресурс временного URL.
    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  }

  let status = chess.turn() === 'w' ? 'Ход белых' : 'Ход чёрных'
  if (chess.isCheckmate()) status = chess.turn() === 'w' ? 'Мат. Победили чёрные' : 'Мат. Победили белые'
  else if (chess.isStalemate()) status = 'Пат. Ничья'
  else if (chess.isDraw()) status = 'Ничья'
  else if (chess.isCheck()) status += ' · Шах'

  return (
    <main className="play-page">
      <header className="play-topbar">
        <div><strong>Игра за обе стороны</strong><span>Двигайте фигуры по обычным правилам</span></div>
        <div className="play-topbar__actions"><SoundToggle /><button className="button button--ghost" onClick={newGame} type="button">Новая партия</button></div>
      </header>
      {confirmReset && <div className="reset-confirmation"><p>Начать новую партию? Текущий черновик будет сброшен. Сначала сохраните партию или скачайте PGN.</p><button className="button button--ghost" onClick={newGame} type="button">Начать заново</button><button className="button button--ghost" onClick={() => setConfirmReset(false)} type="button">Продолжить партию</button></div>}
        
      <div className="play-workspace">
        <section className="board-area">
          <Player color={orientation === 'white' ? 'black' : 'white'} />
          <div className="board-with-evaluation"><EvaluationBar /><ChessBoard fen={chess.fen()} lastMove={last ? [last.from, last.to] : null} onMove={makeMove} playerColor={orientation} /></div>
          <Player color={orientation} />
          <div className={`play-status ${chess.isGameOver() ? 'play-status--finished' : ''}`} role="status"><i />{status}</div>
          <div className="local-game-controls">
            <button className="button button--ghost" disabled={!moves.length} onClick={undo} type="button">↶ Отменить ход</button>
            <button className="button button--ghost" onClick={() => setOrientation((current) => current === 'white' ? 'black' : 'white')} type="button">Перевернуть доску</button>
          </div>
          </section>
        <aside className="play-side-panel">
          <section className="local-game-summary"><h2>Ручная партия</h2><p>Вы ходите за белых и чёрных. Автоматического соперника нет.</p><span>Оценка Stockfish пока недоступна</span></section>
          <section className="live-moves">
            <header><h2>Ходы</h2><span>{moves.length} полуходов</span></header>
            <div className="move-list" aria-label="Ходы ручной партии">{moveRows.length === 0 ? <p className="moves-empty">Сделайте первый ход</p> : moveRows.map((row) => <div className="move-row" key={row.number}><span>{row.number}.</span><strong>{row.white}</strong><strong>{row.black ?? '—'}</strong></div>)}</div>
          </section>
          <section className="local-game-save">
            <button className="primary-button" disabled={!moves.length} onClick={save} type="button">Сохранить партию</button>
            <button className="button button--ghost" disabled={!moves.length} onClick={download} type="button">Скачать PGN</button>
            <small>Черновик восстанавливается в этом браузере. Backend не подключён.</small>
            {notice && <p role="status">{notice}</p>}
          </section>
          <section className="coach-slot"><span className="coach-slot__icon">✦</span><div><h2>Тренер</h2><p>Анализ и объяснения появятся после подключения backend.</p></div><button disabled type="button">Спросить тренера</button></section>
        </aside>
      </div>
    </main>
  )
}

function Player({ color }: { color: 'white' | 'black' }) {
  return <div className="player-bar"><span className={`player-avatar player-avatar--${color}`} /><div><strong>{color === 'white' ? 'Белые' : 'Чёрные'}</strong><small>Ручное управление</small></div></div>
}
