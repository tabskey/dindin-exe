import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from './LoginPage'

vi.mock('../context/auth', () => ({
  useAuth: vi.fn(),
}))
vi.mock('react-router-dom', () => ({
  useNavigate: vi.fn(),
}))
vi.mock('../lib/api', () => ({
  createAccount: vi.fn(),
}))

import { useAuth } from '../context/auth'
import { useNavigate } from 'react-router-dom'
import { createAccount } from '../lib/api'

const mockUseAuth = vi.mocked(useAuth)
const mockNavigate = vi.mocked(useNavigate)
const mockCreateAccount = vi.mocked(createAccount)

beforeEach(() => {
  vi.clearAllMocks()
  mockUseAuth.mockReturnValue({
    account: null,
    isAuthenticated: false,
    login: vi.fn().mockResolvedValue(undefined),
    logout: vi.fn(),
  })
  mockNavigate.mockReturnValue(vi.fn())
})

describe('LoginPage', () => {
  it('renderiza os campos de CPF e senha e o botão Entrar', () => {
    render(<LoginPage />)
    expect(screen.getByPlaceholderText('000.000.000-00')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Sua senha')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeInTheDocument()
  })

  it('submete o login com CPF mascarado e navega para o extrato', async () => {
    const user = userEvent.setup()
    const navigate = vi.fn()
    const login = vi.fn().mockResolvedValue(undefined)
    mockUseAuth.mockReturnValue({
      account: null,
      isAuthenticated: false,
      login,
      logout: vi.fn(),
    })
    mockNavigate.mockReturnValue(navigate)

    render(<LoginPage />)
    await user.type(screen.getByPlaceholderText('000.000.000-00'), '11122233344')
    await user.type(screen.getByPlaceholderText('Sua senha'), 'senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    await waitFor(() => {
      expect(login).toHaveBeenCalledWith('111.222.333-44', 'senha123')
    })
    expect(navigate).toHaveBeenCalledWith('/extrato', { replace: true })
  })

  it('desabilita o botão e mostra "Entrando…" enquanto submete', async () => {
    const user = userEvent.setup()
    let resolveLogin: () => void = () => {}
    const login = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveLogin = resolve
        }),
    )
    mockUseAuth.mockReturnValue({
      account: null,
      isAuthenticated: false,
      login,
      logout: vi.fn(),
    })

    render(<LoginPage />)
    await user.type(screen.getByPlaceholderText('000.000.000-00'), '11122233344')
    await user.type(screen.getByPlaceholderText('Sua senha'), 'senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(screen.getByRole('button', { name: 'Entrando…' })).toBeDisabled()
    // Segundo submit enquanto envia: ignorado — login chamado uma única vez.
    const form = document.querySelector<HTMLFormElement>('form')
    expect(form).not.toBeNull()
    fireEvent.submit(form!)
    expect(login).toHaveBeenCalledTimes(1)
    await act(async () => {
      resolveLogin()
    })
  })

  it('mostra a mensagem de erro quando o login falha', async () => {
    const user = userEvent.setup()
    mockUseAuth.mockReturnValue({
      account: null,
      isAuthenticated: false,
      login: vi.fn().mockRejectedValue(new Error('CPF ou senha inválidos')),
      logout: vi.fn(),
    })

    render(<LoginPage />)
    await user.type(screen.getByPlaceholderText('000.000.000-00'), '11122233344')
    await user.type(screen.getByPlaceholderText('Sua senha'), 'errada')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('CPF ou senha inválidos')).toBeInTheDocument()
  })

  it('abre o modal de criação de conta', async () => {
    const user = userEvent.setup()
    render(<LoginPage />)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))
    expect(screen.getByRole('dialog', { name: 'Criar conta' })).toBeInTheDocument()
  })

  it('fecha o modal de criação de conta pelo Cancelar', async () => {
    const user = userEvent.setup()
    render(<LoginPage />)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('pré-preenche o CPF ao criar a conta pelo modal', async () => {
    const user = userEvent.setup()
    mockCreateAccount.mockResolvedValue({
      id: 2,
      accountNumber: '00002-10',
      name: 'Novo User',
      cpf: '444.444.444-44',
      accountType: 0,
      createdAt: '2026-09-01T00:00:00Z',
    })
    render(<LoginPage />)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    const dialog = screen.getByRole('dialog', { name: 'Criar conta' })
    await user.type(within(dialog).getByPlaceholderText('Seu nome'), 'Novo User')
    await user.type(within(dialog).getByPlaceholderText('000.000.000-00'), '44444444444')
    await user.type(within(dialog).getByPlaceholderText('Mínimo 6 caracteres'), 'senha123')
    await user.click(within(dialog).getByRole('button', { name: 'Criar' }))

    await waitFor(() => expect(mockCreateAccount).toHaveBeenCalledTimes(1))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect((screen.getByPlaceholderText('000.000.000-00') as HTMLInputElement).value).toBe('444.444.444-44')
  })
})
