import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MovementModal } from './MovementModal'

vi.mock('../lib/api', () => ({
  createMovement: vi.fn(),
  getBalance: vi.fn(),
}))

import { createMovement, getBalance, type MovementDto } from '../lib/api'

const mockCreateMovement = vi.mocked(createMovement)
const mockGetBalance = vi.mocked(getBalance)

const accountId = 1

const movement: MovementDto = {
  id: 9,
  accountId,
  type: 0,
  amount: 5000,
  timestamp: '2026-08-31T00:00:00Z',
  counterparty: null,
}

function setup() {
  const onClose = vi.fn()
  const onSuccess = vi.fn()
  render(<MovementModal open accountId={accountId} onClose={onClose} onSuccess={onSuccess} />)
  return { onClose, onSuccess }
}

beforeEach(() => {
  vi.clearAllMocks()
  mockGetBalance.mockResolvedValue({ accountId, balance: 110000 })
})

describe('MovementModal', () => {
  it('deposita por CPF: envia valor + CPF, mostra confirmação com o novo saldo', async () => {
    const user = userEvent.setup()
    const { onSuccess } = setup()

    await user.type(screen.getByPlaceholderText('0,00'), '5050') // R$ 50,50 (5.050 centavos)
    await user.type(screen.getByPlaceholderText('000.000.000-00'), '11122233344')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))

    expect(await screen.findByText('Depósito realizado')).toBeInTheDocument()
    expect(screen.getByText(/R\$\s*50,50/)).toBeInTheDocument()
    expect(screen.getByText(/R\$\s*1\.100,00/)).toBeInTheDocument()

    expect(mockCreateMovement).toHaveBeenCalledTimes(1)
    const [callAccountId, input] = mockCreateMovement.mock.calls[0]
    expect(callAccountId).toBe(accountId)
    expect(input).toEqual({ type: 0, amount: 5050, counterpartyCpf: '111.222.333-44' })
    expect(onSuccess).toHaveBeenCalledTimes(1)
  })

  it('deposita por número da conta: envia XXXXX-XX', async () => {
    const user = userEvent.setup()
    setup()

    await user.type(screen.getByPlaceholderText('0,00'), '100') // R$ 1,00 (100 centavos)
    await user.click(screen.getByRole('button', { name: 'Número da conta' }))
    await user.type(screen.getByPlaceholderText('Número (00000)'), '00315')
    await user.type(screen.getByPlaceholderText('Dígito (00)'), '41')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))

    expect(await screen.findByText('Depósito realizado')).toBeInTheDocument()
    expect(mockCreateMovement).toHaveBeenCalledWith(
      accountId,
      { type: 0, amount: 100, counterpartyAccountNumber: '00315-41' },
      expect.any(String),
    )
  })

  it('deposita sem contraparte (auto-depósito): não envia campos de contraparte', async () => {
    const user = userEvent.setup()
    setup()

    await user.type(screen.getByPlaceholderText('0,00'), '5050')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))

    expect(await screen.findByText('Depósito realizado')).toBeInTheDocument()
    expect(mockCreateMovement).toHaveBeenCalledWith(accountId, { type: 0, amount: 5050 }, expect.any(String))
  })

  it('saca: envia só o valor e não mostra a seção "Pra quem?"', async () => {
    const user = userEvent.setup()
    setup()

    await user.click(screen.getByRole('button', { name: 'Saque' }))
    expect(screen.queryByText('Pra quem?')).not.toBeInTheDocument()

    await user.type(screen.getByPlaceholderText('0,00'), '15000') // R$ 150,00 (15.000 centavos)
    await user.click(screen.getByRole('button', { name: 'Sacar' }))

    expect(await screen.findByText('Saque realizado')).toBeInTheDocument()
    expect(mockCreateMovement).toHaveBeenCalledWith(accountId, { type: 1, amount: 15000 }, expect.any(String))
  })

  it('reutiliza a mesma Idempotency-Key no retry e gera uma nova após o sucesso', async () => {
    const user = userEvent.setup()
    setup()
    mockCreateMovement.mockRejectedValueOnce(new Error('Falha de rede'))
    mockCreateMovement.mockResolvedValueOnce(movement)

    await user.type(screen.getByPlaceholderText('0,00'), '5000') // R$ 50,00 (5.000 centavos)
    await user.click(screen.getByRole('button', { name: 'Depositar' }))
    expect(await screen.findByText('Falha de rede')).toBeInTheDocument()

    // Retry da mesma tentativa: mesma chave.
    await user.click(screen.getByRole('button', { name: 'Depositar' }))
    expect(await screen.findByText('Depósito realizado')).toBeInTheDocument()
    const firstKey = mockCreateMovement.mock.calls[0][2]
    const secondKey = mockCreateMovement.mock.calls[1][2]
    expect(firstKey).toBe(secondKey)

    // Depois do sucesso, a chave é regenerada: nova tentativa usa outra.
    await user.click(screen.getByRole('button', { name: 'Concluir' }))
    await user.type(screen.getByPlaceholderText('0,00'), '5050')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))
    expect(await screen.findByText('Depósito realizado')).toBeInTheDocument()
    const thirdKey = mockCreateMovement.mock.calls[2][2]
    expect(thirdKey).not.toBe(secondKey)
  })

  it('não envia quando o valor é zero', async () => {
    const user = userEvent.setup()
    setup()

    await user.type(screen.getByPlaceholderText('0,00'), '0')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))

    expect(await screen.findByText('Informe um valor maior que zero.')).toBeInTheDocument()
    expect(mockCreateMovement).not.toHaveBeenCalled()
  })

  it('valida CPF incompleto no depósito', async () => {
    const user = userEvent.setup()
    setup()

    await user.type(screen.getByPlaceholderText('0,00'), '5050')
    await user.type(screen.getByPlaceholderText('000.000.000-00'), '123')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))

    expect(await screen.findByText('O CPF precisa ter 11 dígitos.')).toBeInTheDocument()
    expect(mockCreateMovement).not.toHaveBeenCalled()
  })

  it('desabilita o botão e mostra "Enviando…" durante o envio', async () => {
    const user = userEvent.setup()
    setup()
    let resolveCreate: (value: MovementDto) => void = () => {}
    mockCreateMovement.mockImplementationOnce(() => new Promise<MovementDto>((resolve) => {
      resolveCreate = resolve
    }))

    await user.type(screen.getByPlaceholderText('0,00'), '5000')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))

    const submitButton = screen.getByRole('button', { name: 'Enviando…' })
    expect(submitButton).toBeDisabled()

    await act(async () => {
      resolveCreate(movement)
    })
    expect(await screen.findByText('Depósito realizado')).toBeInTheDocument()
  })

  it('fecha com Concluir após o sucesso e com Cancelar no formulário', async () => {
    const user = userEvent.setup()
    const { onClose } = setup()

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(onClose).toHaveBeenCalledTimes(1)

    await user.type(screen.getByPlaceholderText('0,00'), '5000')
    await user.click(screen.getByRole('button', { name: 'Depositar' }))
    await screen.findByText('Depósito realizado')
    await user.click(screen.getByRole('button', { name: 'Concluir' }))
    expect(onClose).toHaveBeenCalledTimes(2)
  })
})
