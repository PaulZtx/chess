import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './styles.css'

// Точка входа: привязываем React к div#root из index.html, как запуск приложения в Program.cs.
// StrictMode в разработке повторяет часть вызовов, чтобы выявить нечистый рендер и ошибки cleanup.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
