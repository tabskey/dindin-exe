import '@testing-library/jest-dom/vitest'

// Com `globals: true` no vitest.config.ts, a Testing Library registra o
// cleanup automático (afterEach global) — nada mais é necessário aqui.

// jsdom não implementa matchMedia; o useTheme consulta prefers-color-scheme
// no tema inicial (disparado quando o ThemeToggle é renderizado em testes).
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as MediaQueryList,
})
