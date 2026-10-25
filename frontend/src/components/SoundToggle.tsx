import { useSyncExternalStore } from 'react'
import { isBoardSoundEnabled, setBoardSoundEnabled, subscribeBoardSound } from '../lib/sounds'

export function SoundToggle() {
  // Настройка живёт вне React. Hook подписывает кнопку на изменения общего звукового модуля.
  // Благодаря этому кнопки на разных экранах показывают одно и то же состояние.
  const enabled = useSyncExternalStore(subscribeBoardSound, isBoardSoundEnabled)
  return <button className="sound-toggle" type="button" aria-label="Звук" aria-pressed={enabled} title={enabled ? 'Выключить звук' : 'Включить звук'} onClick={() => setBoardSoundEnabled(!enabled)}><span aria-hidden="true">{enabled ? '♪' : '♩'}</span>{enabled ? 'Звук вкл.' : 'Звук выкл.'}</button>
}
