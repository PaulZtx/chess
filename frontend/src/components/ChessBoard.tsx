import { useEffect, useMemo, useState, type CSSProperties } from 'react'
import { Chess, type Square } from 'chess.js'
import { Chessboard } from 'react-chessboard'

// Контракт входных параметров: примерно DTO + Func<string, string, string?, bool>.
// Знак ? делает параметр необязательным; без onMove доска работает только для просмотра.
interface ChessBoardProps {
  fen: string
  highlightedSquares?: string[]
  lastMove?: [string, string] | null
  onMove?: (from: string, to: string, promotion?: string) => boolean
  playerColor?: 'white' | 'black'
}

const promotions = [{ piece: 'q', label: 'Ферзь' }, { piece: 'r', label: 'Ладья' }, { piece: 'b', label: 'Слон' }, { piece: 'n', label: 'Конь' }]

// Обёртка над библиотекой: внешний компонент передаёт FEN и решает, принять ли ход.
export function ChessBoard({ fen, highlightedSquares = [], lastMove, onMove, playerColor = 'white' }: ChessBoardProps) {
  const chess = useMemo(() => new Chess(fen), [fen])
  // Здесь хранится только состояние взаимодействия: выбранное поле и ожидающее превращение.
  const [selected, setSelected] = useState<string | null>(null)
  const [promotion, setPromotion] = useState<{ from: string; to: string } | null>(null)
  // Другая позиция отменяет старое выделение и незавершённый выбор превращения.
  useEffect(() => { setSelected(null); setPromotion(null) }, [fen])

  // Record<string, CSSProperties> похож на Dictionary<string, ...>: стиль для каждого поля.
  const squareStyles: Record<string, CSSProperties> = {}
  for (const square of lastMove ?? []) squareStyles[square] = { backgroundColor: 'rgba(246, 246, 105, .5)' }
  for (const square of highlightedSquares) squareStyles[square] = { boxShadow: 'inset 0 0 0 5px rgba(232, 86, 66, .85)' }
  if (selected) {
    squareStyles[selected] = { backgroundColor: 'rgba(246, 246, 105, .7)' }
    for (const move of chess.moves({ square: selected as Square, verbose: true })) {
      squareStyles[move.to] = { backgroundImage: move.captured ? 'radial-gradient(transparent 58%, rgba(25,30,22,.35) 60%)' : 'radial-gradient(rgba(25,30,22,.28) 22%, transparent 24%)' }
    }
  }
  if (chess.isCheck()) {
    const king = chess.board().flat().find((piece) => piece?.type === 'k' && piece.color === chess.turn())
    if (king) squareStyles[king.square] = { boxShadow: 'inset 0 0 30px rgba(214,69,51,.95)' }
  }

  function move(from: string, to: string) {
    if (!onMove || promotion || chess.isGameOver()) return false
    // chess.js проверяет правила; react-chessboard лишь отображает и передаёт события.
    const candidates = chess.moves({ square: from as Square, verbose: true }).filter((candidate) => candidate.to === to)
    if (!candidates.length) return false
    setSelected(null)
    if (candidates.some((candidate) => candidate.promotion)) {
      // Пока фигура не выбрана, возвращаем false: библиотека не должна сама завершать ход.
      setPromotion({ from, to })
      return false
    }
    return onMove(from, to)
  }

  return (
    <div className="board-container">
      <div className="ready-board" role="group" aria-label="Шахматная доска">
        <Chessboard options={{
          position: fen,
          boardOrientation: playerColor,
          darkSquareStyle: { backgroundColor: '#769656' },
          lightSquareStyle: { backgroundColor: '#eeeed2' },
          squareStyles,
          animationDurationInMs: 180,
          allowDragging: !!onMove && !promotion && !chess.isGameOver(),
          allowDragOffBoard: false,
          canDragPiece: ({ piece }) => piece.pieceType[0] === chess.turn(),
          // Drag-and-drop и два клика используют одну проверку move().
          onPieceDrop: ({ sourceSquare, targetSquare }) => targetSquare ? move(sourceSquare, targetSquare) : false,
          onSquareClick: ({ square }) => {
            if (!onMove || promotion || chess.isGameOver()) return
            if (selected && move(selected, square)) return
            const piece = chess.get(square as Square)
            setSelected(piece?.color === chess.turn() && square !== selected ? square : null)
          },
        }} />
      </div>
      {/* Условный рендер: диалог существует только при ожидающем превращении. */}
      {promotion && <div className="promotion-picker" role="dialog" aria-modal="true" aria-label="Превращение пешки">
        <strong>Превратить пешку в…</strong>
        <div>{promotions.map(({ piece, label }, index) => <button autoFocus={index === 0} key={piece} type="button" onClick={() => { onMove?.(promotion.from, promotion.to, piece); setPromotion(null); setSelected(null) }}>{label}</button>)}</div>
        <button type="button" onClick={() => { setPromotion(null); setSelected(null) }}>Отмена</button>
      </div>}
    </div>
  )
}
