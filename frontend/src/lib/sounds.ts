export type BoardSound = 'move' | 'capture' | 'check' | 'checkmate'

const preferenceKey = 'chess-trainer-sound'
const soundFiles: Record<BoardSound, string> = {
  move: 'Move.mp3',
  capture: 'Capture.mp3',
  check: 'OutOfBound.ogg',
  checkmate: 'GenericNotify.mp3',
}
// Кэш объектов Audio на уровне модуля: общий для всех компонентов этой страницы.
const players = new Map<BoardSound, HTMLAudioElement>()
let enabled = readPreference()
// Подписчики работают по принципу C# event: уведомляем их при смене настройки.
const listeners = new Set<() => void>()

function readPreference() {
  try { return localStorage.getItem(preferenceKey) !== 'off' }
  catch { return true }
}

export function isBoardSoundEnabled() { return enabled }

export function subscribeBoardSound(listener: () => void) {
  listeners.add(listener)
  // React вызывает эту функцию при отписке компонента.
  return () => { listeners.delete(listener) }
}

export function setBoardSoundEnabled(value: boolean) {
  enabled = value
  if (!value) stopSounds()
  try { localStorage.setItem(preferenceKey, value ? 'on' : 'off') }
  catch { /* Sound remains usable when browser storage is unavailable. */ }
  for (const listener of listeners) listener()
}

function stopSounds() {
  for (const audio of players.values()) {
    audio.pause()
    audio.currentTime = 0
  }
}

export function playBoardSound(kind: BoardSound) {
  if (!enabled) return
  // Audio is optional; playback errors must never affect an accepted move.
  try {
    stopSounds()
    let audio = players.get(kind)
    if (!audio) {
      // Файлы из public доступны по URL. BASE_URL учитывает размещение сайта в подпапке.
      audio = new Audio(`${import.meta.env.BASE_URL}sound/standard/${soundFiles[kind]}`)
      audio.preload = 'auto'
      audio.volume = .65
      players.set(kind, audio)
    }
    // play() возвращает Promise (похож на Task). Блокировку аудио браузером обрабатываем отдельно.
    void audio.play().catch(() => undefined)
  } catch { /* Unsupported or blocked audio: continue silently. */ }
}
