import { useRef, useState, type ChangeEvent, type DragEvent } from 'react'
import type { ChessGame } from '../types'
import { parsePgn } from '../lib/pgn'
import { UploadIcon } from './Icons'

interface UploadCardProps {
  onUploaded: (game: ChessGame) => void
}

export function UploadCard({ onUploaded }: UploadCardProps) {
  // Здесь ref хранит DOM-элемент: кнопка открывает скрытый стандартный выбор файла.
  const inputRef = useRef<HTMLInputElement>(null)
  const [isDragging, setIsDragging] = useState(false)
  const [error, setError] = useState('')

  async function readFile(file: File | undefined) {
    if (!file) return

    if (!file.name.toLowerCase().endsWith('.pgn')) {
      setError('Нужен файл в формате .pgn')
      return
    }

    try {
      // await аналогичен C#: ждём Promise чтения файла, затем уведомляем родителя через callback.
      const text = await file.text()
      onUploaded(parsePgn(text, file.name))
      setError('')
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Не удалось прочитать PGN.')
    }
  }

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    void readFile(event.target.files?.[0])
    event.target.value = ''
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setIsDragging(false)
    void readFile(event.dataTransfer.files[0])
  }

  return (
    <div
      className={`upload-card ${isDragging ? 'upload-card--dragging' : ''}`}
      onDragEnter={() => setIsDragging(true)}
      onDragLeave={() => setIsDragging(false)}
      onDragOver={(event) => event.preventDefault()}
      onDrop={handleDrop}
    >
      <div className="upload-card__icon"><UploadIcon /></div>
      <div>
        <strong>Загрузить PGN</strong>
        <p>Файл с одной партией</p>
        {error && <span className="form-error" role="alert">{error}</span>}
      </div>
      <button className="button button--ghost" onClick={() => inputRef.current?.click()} type="button">
        Выбрать файл
      </button>
      <input ref={inputRef} type="file" accept=".pgn,application/x-chess-pgn" onChange={handleChange} hidden />
    </div>
  )
}
