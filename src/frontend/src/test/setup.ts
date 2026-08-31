import '@testing-library/jest-dom/vitest'

// Com `globals: true` no vitest.config.ts, a Testing Library registra o
// cleanup automático (afterEach global) — nada mais é necessário aqui.
