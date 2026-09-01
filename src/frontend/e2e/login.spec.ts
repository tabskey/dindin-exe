import { expect, test, type Page } from '@playwright/test'

// E2E (ADR 0005, Fase 6): fluxos completos no navegador contra a API no Docker
// (com seed). O webServer do playwright.config.ts sobe o Vite (:5173), que
// repassa /api para o nginx do Docker (:80).
//
// Requisitos: `docker compose up -d --build` com o seed carregado.

const SEED_CPF = '111.111.111-11'
const SEED_PASSWORD = 'senha123'

// CPF com 11 dígitos únicos por execução (evita 409 no fluxo de criação).
function uniqueCpf(): string {
  const digits = Array.from({ length: 11 }, () => Math.floor(Math.random() * 10)).join('')
  return digits.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4')
}

// Lê o saldo exibido ("R$ 1.250,50") e devolve o valor numérico (reais).
async function readBalance(page: Page): Promise<number> {
  const text = await page.getByTestId('balance-value').innerText()
  return Number(text.replace(/[^\d]/g, '')) / 100
}

async function login(page: Page) {
  await page.goto('/login')
  await page.getByPlaceholder('000.000.000-00').fill(SEED_CPF)
  await page.getByPlaceholder('Sua senha').fill(SEED_PASSWORD)
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page.getByTestId('balance-value')).toBeVisible()
}

test('smoke: a tela de login renderiza', async ({ page }) => {
  await page.goto('/login')
  await expect(page.getByRole('heading', { name: 'DinDin.EXE' })).toBeVisible()
  await expect(page.getByPlaceholder('000.000.000-00')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible()
})

test('login → extrato (conta do seed)', async ({ page }) => {
  await login(page)
  await expect(page).toHaveURL(/\/extrato/)
  await expect(page.getByText(/Olá,/)).toBeVisible()
  await expect(page.getByText('Ana Teste')).toBeVisible()
  await expect(page.getByTestId('balance-value')).toContainText('R$')
  await expect(page.getByRole('button', { name: 'Nova movimentação' })).toBeVisible()
})

test('depósito: o saldo aumenta na boca do caixa', async ({ page }) => {
  await login(page)
  const before = await readBalance(page)

  await page.getByRole('button', { name: 'Nova movimentação' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByPlaceholder('0,00').fill('50,00')
  await dialog.getByRole('button', { name: 'Depositar' }).click()

  await expect(dialog.getByText('Depósito realizado')).toBeVisible()
  await dialog.getByRole('button', { name: 'Concluir' }).click()
  await expect(dialog).not.toBeVisible()

  await expect
    .poll(() => readBalance(page), { timeout: 10_000 })
    .toBe(before + 50)
})

test('saque: o saldo diminui', async ({ page }) => {
  await login(page)
  const before = await readBalance(page)

  await page.getByRole('button', { name: 'Nova movimentação' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('button', { name: 'Saque' }).click()
  await dialog.getByPlaceholder('0,00').fill('20,00')
  await dialog.getByRole('button', { name: 'Sacar' }).click()

  await expect(dialog.getByText('Saque realizado')).toBeVisible()
  await dialog.getByRole('button', { name: 'Concluir' }).click()
  await expect(dialog).not.toBeVisible()

  await expect
    .poll(() => readBalance(page), { timeout: 10_000 })
    .toBe(before - 20)
})

test('transferência: o valor sai do remetente e cai no destinatário', async ({ page }) => {
  // Baseline do destinatário (Bruno do seed).
  await page.goto('/login')
  await page.getByPlaceholder('000.000.000-00').fill('222.222.222-22')
  await page.getByPlaceholder('Sua senha').fill(SEED_PASSWORD)
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page.getByTestId('balance-value')).toBeVisible()
  const brunoBefore = await readBalance(page)
  await page.getByRole('button', { name: 'Sair' }).click()

  // Ana transfere R$ 10,00 para Bruno por CPF.
  await login(page)
  const anaBefore = await readBalance(page)
  await page.getByRole('button', { name: 'Nova movimentação' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByPlaceholder('000.000.000-00').fill('222.222.222-22')
  await dialog.getByPlaceholder('0,00').fill('10,00')
  await dialog.getByRole('button', { name: 'Depositar' }).click()

  await expect(dialog.getByText('Transferência realizada')).toBeVisible()
  await expect(dialog.getByText(/Para BRUNO TESTE 222-22 CC/)).toBeVisible()
  await dialog.getByRole('button', { name: 'Concluir' }).click()
  await expect(dialog).not.toBeVisible()

  await expect
    .poll(() => readBalance(page), { timeout: 10_000 })
    .toBe(anaBefore - 10)

  // Confere o crédito na conta do destinatário.
  await page.getByRole('button', { name: 'Sair' }).click()
  await page.getByPlaceholder('000.000.000-00').fill('222.222.222-22')
  await page.getByPlaceholder('Sua senha').fill(SEED_PASSWORD)
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page.getByTestId('balance-value')).toBeVisible()
  await expect
    .poll(() => readBalance(page), { timeout: 10_000 })
    .toBe(brunoBefore + 10)
})

test('criar conta → login com CPF preenchido', async ({ page }) => {
  const cpf = uniqueCpf()

  await page.goto('/login')
  await page.getByRole('button', { name: 'Criar conta' }).click()
  const dialog = page.getByRole('dialog', { name: 'Criar conta' })
  await dialog.getByPlaceholder('Seu nome').fill('Usuário E2E')
  await dialog.getByPlaceholder('000.000.000-00').fill(cpf)
  await dialog.getByPlaceholder('Mínimo 6 caracteres').fill('senha123')
  await dialog.getByRole('button', { name: 'Criar' }).click()

  await expect(dialog).not.toBeVisible()
  await expect(page.getByPlaceholder('000.000.000-00')).toHaveValue(cpf)

  await page.getByPlaceholder('Sua senha').fill('senha123')
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page).toHaveURL(/\/extrato/)
  await expect(page.getByTestId('balance-value')).toBeVisible()
})
