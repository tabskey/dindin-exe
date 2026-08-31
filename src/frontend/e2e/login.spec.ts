import { expect, test } from '@playwright/test'

// Smoke de Fase 3: a tela de login renderiza. Fluxos completos (login →
// extrato → movimentação) entram na Fase 6, contra a API com seed no Docker.
test('smoke: a tela de login renderiza', async ({ page }) => {
  await page.goto('/login')
  await expect(page.getByRole('heading', { name: 'DinDin.EXE' })).toBeVisible()
  await expect(page.getByPlaceholder('000.000.000-00')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible()
})
