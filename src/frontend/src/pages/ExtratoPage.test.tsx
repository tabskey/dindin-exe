import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExtratoPage } from './ExtratoPage'

vi.mock('../lib/api', () => ({
  getBalance: vi.fn(),
  getMovements: vi.fn(),
  getAvatar: vi.fn(),
  updateAvatar: vi.fn(),
  createMovement: vi.fn(),
}))
vi.mock('../context/auth', () => ({
  useAuth: vi.fn(),
}))
vi.mock('react-router-dom', () => ({
  useNavigate: vi.fn(),
}))

import { getAvatar, getBalance, getMovements, type BalanceDto, type MovementDto } from '../lib/api'
import { useAuth } from '../context/auth'
import { useNavigate } from 'react-router-dom'

const mockGetAvatar = vi.mocked(getAvatar)
const mockGetBalance = vi.mocked(getBalance)
const mockGetMovements = vi.mocked(getMovements)
const mockUseAuth = vi.mocked(useAuth)
const mockNavigate = vi.mocked(useNavigate)

const account = {
  id: 1,
  accountNumber: '00315-41',
  name: 'Ana Teste',
  cpf: '111.222.333-44',
  accountType: 0,
  createdAt: '2026-08-01T00:00:00Z',
}

const movements: MovementDto[] = [
  { id: 1, accountId: 1, type: 0, amount: 10050, timestamp: '2026-08-30T15:00:00Z', counterparty: 'João' },
  { id: 2, accountId: 1, type: 0, amount: 25000, timestamp: '2026-08-29T15:00:00Z', counterparty: null },
  { id: 3, accountId: 1, type: 0, amount: 4025, timestamp: '2026-08-28T15:00:00Z', counterparty: 'Maria' },
  { id: 4, accountId: 1, type: 0, amount: 86000, timestamp: '2026-08-27T15:00:00Z', counterparty: null },
  { id: 5, accountId: 1, type: 1, amount: 3590, timestamp: '2026-08-26T15:00:00Z', counterparty: 'Padaria' },
  { id: 6, accountId: 1, type: 1, amount: 12000, timestamp: '2026-08-25T15:00:00Z', counterparty: 'Mercado' },
  { id: 7, accountId: 1, type: 1, amount: 1275, timestamp: '2026-08-24T15:00:00Z', counterparty: 'Farmácia' },
  { id: 8, accountId: 1, type: 1, amount: 6000, timestamp: '2026-08-23T15:00:00Z', counterparty: null },
]

const history = { items: movements, page: 1, pageSize: 20, total: 8 }

beforeEach(() => {
  vi.clearAllMocks()
  mockUseAuth.mockReturnValue({
    account,
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
  })
  mockNavigate.mockReturnValue(vi.fn())
  mockGetBalance.mockResolvedValue({ accountId: 1, balance: 125050 })
  mockGetMovements.mockResolvedValue(history)
  mockGetAvatar.mockResolvedValue(null)
  // jsdom não implementa object URLs — stub para o caso de avatar presente.
  URL.createObjectURL = vi.fn(() => 'blob:avatar-mock') as typeof URL.createObjectURL
  URL.revokeObjectURL = vi.fn() as typeof URL.revokeObjectURL
})

describe('ExtratoPage', () => {
  it('renderiza saldo, 8 movimentações com estilos de receita/despesa e o FAB', async () => {
    render(<ExtratoPage />)

    expect(await screen.findByText(/1\.250,50/)).toBeInTheDocument()
    expect(screen.getByText(/Olá/)).toBeInTheDocument()
    expect(screen.getByText(/Ana Teste/)).toBeInTheDocument()
    expect(screen.getByText('AT')).toBeInTheDocument()
    expect(screen.getByText('30/08/2026')).toBeInTheDocument()
    expect(screen.getAllByText('Boca do caixa')).toHaveLength(3)
    expect(screen.getAllByRole('listitem')).toHaveLength(8)
    expect(screen.getByRole('button', { name: 'Nova movimentação' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Ativar modo/ })).toBeInTheDocument()

    const incomeValue = screen.getByText(/\+ R\$\s*100,50/)
    expect(incomeValue).toHaveClass('text-income')
    expect(incomeValue.closest('li')).toHaveClass('bg-income-bg')

    const expenseValue = screen.getByText(/- R\$\s*35,90/)
    expect(expenseValue).toHaveClass('text-expense')
    expect(expenseValue.closest('li')).toHaveClass('bg-expense-bg')
  })

  it('abre o modal de movimentação ao clicar no FAB', async () => {
    const user = userEvent.setup()
    render(<ExtratoPage />)

    await user.click(await screen.findByRole('button', { name: 'Nova movimentação' }))
    expect(screen.getByRole('dialog', { name: 'Depósito' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Depositar' })).toBeInTheDocument()
  })

  it('mostra o estado de carregamento enquanto busca os dados', async () => {
    let resolveBalance: (value: BalanceDto) => void = () => {}
    mockGetBalance.mockReturnValue(
      new Promise<BalanceDto>((resolve) => {
        resolveBalance = resolve
      }),
    )

    render(<ExtratoPage />)
    expect(screen.getByText('Carregando…')).toBeInTheDocument()

    await act(async () => {
      resolveBalance({ accountId: 1, balance: 125050 })
    })
    expect(await screen.findByText(/1\.250,50/)).toBeInTheDocument()
  })

  it('mostra o erro e permite tentar novamente', async () => {
    const user = userEvent.setup()
    mockGetMovements.mockRejectedValueOnce(new Error('Falha de rede'))

    render(<ExtratoPage />)
    expect(await screen.findByText('Falha de rede')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText(/1\.250,50/)).toBeInTheDocument()
    expect(screen.getAllByRole('listitem')).toHaveLength(8)
  })

  it('desloga e volta para o login ao clicar em Sair', async () => {
    const user = userEvent.setup()
    const logout = vi.fn()
    const navigate = vi.fn()
    mockUseAuth.mockReturnValue({
      account,
      isAuthenticated: true,
      login: vi.fn(),
      logout,
    })
    mockNavigate.mockReturnValue(navigate)

    render(<ExtratoPage />)
    await user.click(screen.getByRole('button', { name: 'Sair' }))

    expect(logout).toHaveBeenCalledTimes(1)
    expect(navigate).toHaveBeenCalledWith('/login', { replace: true })
  })

  it('mostra a imagem do avatar quando existe e esconde as iniciais', async () => {
    mockGetAvatar.mockResolvedValue(new Blob(['fake'], { type: 'image/png' }))

    render(<ExtratoPage />)

    const img = await screen.findByAltText('Avatar de Ana Teste')
    expect(img).toHaveAttribute('src', 'blob:avatar-mock')
    expect(screen.queryByText('AT')).not.toBeInTheDocument()
  })

  it('abre o modal de avatar com as duas opções ao clicar no avatar', async () => {
    const user = userEvent.setup()
    render(<ExtratoPage />)

    await user.click(await screen.findByRole('button', { name: 'Opções do avatar' }))
    expect(screen.getByRole('button', { name: 'Ver imagem de perfil' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Trocar imagem de perfil' })).toBeInTheDocument()
  })
})
