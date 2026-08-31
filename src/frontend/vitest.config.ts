import react from '@vitejs/plugin-react'
import { configDefaults, defineConfig } from 'vitest/config'

// Configuração de testes (Vitest + Testing Library) — ADR 0005.
// Ambiente jsdom para componentes React; cobertura exportada em lcov para o
// SonarQube (meta >= 80% de linhas, verificada nas fases finais).
// Provider istanbul: no Windows, o v8 duplica entradas no lcov por case do path.
export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    // e2e/ é do Playwright — o Vitest roda só os testes de unidade/componente.
    exclude: [...configDefaults.exclude, 'e2e/**'],
    coverage: {
      provider: 'istanbul',
      // `all: true` (padrão) crasha no istanbul/Windows ao coletar a lista de
      // arquivos; com `all: false` medimos só os arquivos exercitados pelos
      // testes — por arquivo o percentual é fiel (lcov sem duplicatas).
      all: false,
      reporter: ['text', 'lcov'],
      include: ['src/**/*.ts', 'src/**/*.tsx'],
      exclude: ['src/main.tsx', 'src/test/**', 'src/assets/**', '**/*.test.ts', '**/*.test.tsx'],
    },
  },
})
