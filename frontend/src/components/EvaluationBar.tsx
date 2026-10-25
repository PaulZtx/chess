// Backend will supply a Stockfish score here. No client-side position evaluation.
export function EvaluationBar() {
  return (
    <div className="evaluation-bar" role="img" aria-label="Шкала оценки: нет данных Stockfish" title="Оценка появится после подключения Stockfish">
      <div className="evaluation-bar__black" />
      <div className="evaluation-bar__white" />
      <span className="evaluation-bar__score">—</span>
      <span className="evaluation-bar__label">DEMO</span>
    </div>
  )
}
