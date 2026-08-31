import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createAccount } from '../lib/api'
import { CreateAccountModal } from './CreateAccountModal'

vi.mock('../lib/api', () => ({
  createAccount: vi.fn(),
}))

const mockCreateAccount = vi.mocked(createAccount)
const onClose = vi.fn()
const onCreated = vi.fn()

function renderModal() {
  return render(<CreateAccountModal open onClose={onClose} onCreated={onCreated} />)
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText('Seu nome'), 'Ana Teste')
  await user.type(screen.getByPlaceholderText('000.000.000-00'), '11122233344')
  await user.type(screen.getByPlaceholderText('Mínimo 6 caracteres'), 'senha123')
}

beforeEach(() => {
  vi.clearAllMocks()
})

describe('CreateAccountModal', () => {
  it('valida nome, CPF e senha antes de chamar a API', async () => {
    const user = userEvent.setup()
    renderModal()

    await user.click(screen.getByRole('button', { name: 'Criar' }))
    expect(await screen.findByText('Informe seu nome.')).toBeInTheDocument()

    await user.type(screen.getByPlaceholderText('Seu nome'), 'Ana')
    await user.click(screen.getByRole('button', { name: 'Criar' }))
    expect(await screen.findByText('O CPF precisa ter 11 dígitos.')).toBeInTheDocument()

    await user.type(screen.getByPlaceholderText('000.000.000-00'), '11122233344')
    await user.type(screen.getByPlaceholderText('Mínimo 6 caracteres'), '12345')
    await user.click(screen.getByRole('button', { name: 'Criar' }))
    expect(await screen.findByText('A senha precisa ter pelo menos 6 caracteres.')).toBeInTheDocument()

    expect(mockCreateAccount).not.toHaveBeenCalled()
  })

  it('cria a conta com chave de idempotência e avisa o login (pré-preenche o CPF)', async () => {
    const user = userEvent.setup()
    mockCreateAccount.mockResolvedValue({
      id: 1,
      accountNumber: '00315-41',
      name: 'Ana Teste',
      cpf: '111.222.333-44',
      accountType: 0,
      createdAt: '2026-08-31T00:00:00Z',
    })
    renderModal()
    await fillValidForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar' }))

    await waitFor(() => expect(mockCreateAccount).toHaveBeenCalledTimes(1))
    const [input, idempotencyKey] = mockCreateAccount.mock.calls[0]
    expect(input).toEqual({ name: 'Ana Teste', cpf: '111.222.333-44', password: 'senha123' })
    expect(idempotencyKey).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/,
    )
    expect(onClose).toHaveBeenCalledTimes(1)
    expect(onCreated).toHaveBeenCalledWith('111.222.333-44')
  })

  it('mostra o erro do backend (409) sem fechar o modal', async () => {
    const user = userEvent.setup()
    mockCreateAccount.mockRejectedValue(new Error('CPF já cadastrado'))
    renderModal()
    await fillValidForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar' }))

    expect(await screen.findByText('CPF já cadastrado')).toBeInTheDocument()
    expect(onClose).not.toHaveBeenCalled()
  })
})
