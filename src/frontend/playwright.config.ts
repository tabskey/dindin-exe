import { defineConfig } from '@playwright/test'

// E2E (ADR 0005): fluxos completos no navegador. O app roda no dev server
// (proxy /api → Docker :80); a API precisa estar no ar e com seed carregado
// para os fluxos que autenticam.
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  use: {
    baseURL: 'http://localhost:5173',
  },
  // HTML report sempre gerado (playwright-report/) para consulta com `npx playwright show-report`.
  reporter: [['list'], ['html', { open: 'never' }]],
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 60_000,
  },
})
