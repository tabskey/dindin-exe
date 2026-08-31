import { useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { createMovement, getBalance, type MovementType } from '../lib/api'
import { maskBRL, maskCpf, parseBRL } from '../lib/masks'
import { Modal } from './Modal'

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

interface MovementModalProps {
  open: boolean
  accountId: number
  onClose: () => void
  // Chamado após sucesso, para o extrato atualizar saldo e histórico.
  onSuccess: () => void
}

// "Pra quem?" no depósito: identificar a contraparte por CPF ou número da conta;
// vazio → auto-depósito (boca do caixa).
type CounterpartyMode = 'cpf' | 'account'

interface SuccessInfo {
  amount: number
  balance: number
}

export function MovementModal({ open, accountId, onClose, onSuccess }: MovementModalProps) {
  const [type, setType] = useState<MovementType>(0)
  const [amount, setAmount] = useState('')
  const [counterpartyMode, setCounterpartyMode] = useState<CounterpartyMode>('cpf')
  const [cpf, setCpf] = useState('')
  const [accountNumber, setAccountNumber] = useState('')
  const [accountDigit, setAccountDigit] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<SuccessInfo | null>(null)
  // Idempotência por tentativa: a mesma chave é reutilizada no retry da mesma
  // tentativa (replay não duplica); é regenerada após o sucesso.
  const idempotencyKeyRef = useRef<string | null>(null)
  // True depois que o usuário digita a vírgula; antes disso o ",00" é auto-gerado.
  const commaTypedRef = useRef(false)

  function resetForm() {
    setType(0)
    setAmount('')
    setCounterpartyMode('cpf')
    setCpf('')
    setAccountNumber('')
    setAccountDigit('')
    setError('')
    setSubmitting(false)
    setSuccess(null)
    idempotencyKeyRef.current = null
    commaTypedRef.current = false
  }

  function handleClose() {
    resetForm()
    onClose()
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting) {
      return
    }
    setError('')

    const amountValue = parseBRL(amount)
    if (amountValue <= 0) {
      setError('Informe um valor maior que zero.')
      return
    }

    // Contraparte só existe no depósito; ambos vazios → auto-depósito.
    let counterpartyCpf: string | undefined
    let counterpartyAccountNumber: string | undefined
    if (type === 0) {
      if (counterpartyMode === 'cpf') {
        const digits = cpf.replace(/\D/g, '')
        if (digits.length > 0 && digits.length !== 11) {
          setError('O CPF precisa ter 11 dígitos.')
          return
        }
        counterpartyCpf = digits.length === 11 ? maskCpf(cpf) : undefined
      } else {
        const number = accountNumber.replace(/\D/g, '')
        const digit = accountDigit.replace(/\D/g, '')
        if ((number.length > 0 || digit.length > 0) && (number.length !== 5 || digit.length !== 2)) {
          setError('Informe número e dígito da conta (XXXXX-XX).')
          return
        }
        counterpartyAccountNumber = number.length === 5 && digit.length === 2 ? `${number}-${digit}` : undefined
      }
    }

    setSubmitting(true)
    try {
      const key = idempotencyKeyRef.current ?? crypto.randomUUID()
      idempotencyKeyRef.current = key
      await createMovement(
        accountId,
        {
          type,
          amount: amountValue,
          ...(counterpartyCpf !== undefined && { counterpartyCpf }),
          ...(counterpartyAccountNumber !== undefined && { counterpartyAccountNumber }),
        },
        key,
      )
      idempotencyKeyRef.current = null
      // O 201 não traz o saldo novo — busca para mostrar na confirmação.
      const balanceData = await getBalance(accountId)
      setSuccess({ amount: amountValue, balance: balanceData.balance })
      onSuccess()
    } catch (err) {
      // Erro mantém a chave atual: retry da mesma tentativa não duplica.
      setError(err instanceof Error ? err.message : 'Não foi possível realizar a movimentação.')
    } finally {
      setSubmitting(false)
    }
  }

  const inputClasses =
    'mt-1 w-full rounded-lg border border-border bg-background px-3 py-2 text-foreground placeholder:text-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/40'
  const toggleActive = 'flex-1 rounded-lg bg-accent px-3 py-2 text-sm font-medium text-accent-foreground'
  const toggleInactive =
    'flex-1 rounded-lg border border-border px-3 py-2 text-sm font-medium text-muted transition-colors hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-accent'

  return (
    <Modal open={open} onClose={handleClose} title={type === 0 ? 'Depósito' : 'Saque'}>
      {success ? (
        <div className="text-center">
          <p className="text-sm text-muted">{type === 0 ? 'Depósito realizado' : 'Saque realizado'}</p>
          <p className="mt-1 text-3xl font-bold tabular-nums">{brl.format(success.amount)}</p>
          <div className="mt-6 rounded-xl border border-border bg-balance-bg p-4">
            <p className="text-sm text-muted">Saldo atual</p>
            <p className="mt-1 text-2xl font-bold tabular-nums">{brl.format(success.balance)}</p>
          </div>
          <button
            type="button"
            onClick={handleClose}
            className="mt-6 w-full rounded-lg bg-accent px-3 py-2 font-medium text-accent-foreground transition-opacity hover:opacity-90 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
          >
            Concluir
          </button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <div role="group" aria-label="Tipo de movimentação" className="flex gap-2">
            <button
              type="button"
              aria-pressed={type === 0}
              onClick={() => setType(0)}
              className={type === 0 ? toggleActive : toggleInactive}
            >
              Depósito
            </button>
            <button
              type="button"
              aria-pressed={type === 1}
              onClick={() => setType(1)}
              className={type === 1 ? toggleActive : toggleInactive}
            >
              Saque
            </button>
          </div>

          <label className="block">
            <span className="text-sm font-medium text-foreground">Valor</span>
            <div className="relative">
              <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-sm text-muted">R$</span>
              <input
                type="text"
                inputMode="decimal"
                autoComplete="off"
                placeholder="0,00"
                value={amount}
                onChange={(event) => setAmount(maskBRL(event.target.value))}
                className={`${inputClasses} pl-10`}
              />
            </div>
          </label>

          {type === 0 && (
            <fieldset>
              <legend className="text-sm font-medium text-foreground">Pra quem?</legend>
              <div className="mt-1 flex gap-2">
                <button
                  type="button"
                  aria-pressed={counterpartyMode === 'cpf'}
                  onClick={() => setCounterpartyMode('cpf')}
                  className={counterpartyMode === 'cpf' ? toggleActive : toggleInactive}
                >
                  CPF
                </button>
                <button
                  type="button"
                  aria-pressed={counterpartyMode === 'account'}
                  onClick={() => setCounterpartyMode('account')}
                  className={counterpartyMode === 'account' ? toggleActive : toggleInactive}
                >
                  Número da conta
                </button>
              </div>
              {counterpartyMode === 'cpf' ? (
                <input
                  type="text"
                  inputMode="numeric"
                  autoComplete="off"
                  placeholder="000.000.000-00"
                  value={cpf}
                  onChange={(event) => setCpf(event.target.value.replace(/\D/g, '').slice(0, 11))}
                  onBlur={() => setCpf(maskCpf(cpf))}
                  className={inputClasses}
                />
              ) : (
                <div className="mt-1 flex gap-2">
                  <input
                    type="text"
                    inputMode="numeric"
                    autoComplete="off"
                    placeholder="Número (00000)"
                    value={accountNumber}
                    onChange={(event) => setAccountNumber(event.target.value.replace(/\D/g, '').slice(0, 5))}
                    className={inputClasses}
                  />
                  <input
                    type="text"
                    inputMode="numeric"
                    autoComplete="off"
                    placeholder="Dígito (00)"
                    value={accountDigit}
                    onChange={(event) => setAccountDigit(event.target.value.replace(/\D/g, '').slice(0, 2))}
                    className={inputClasses}
                  />
                </div>
              )}
              <p className="mt-2 text-xs text-muted">Deixe em branco para depositar para você mesmo (boca do caixa).</p>
            </fieldset>
          )}

          {error && <p className="text-sm text-expense">{error}</p>}

          <div className="flex gap-3 pt-1">
            <button
              type="button"
              onClick={handleClose}
              className="flex-1 rounded-lg border border-border px-3 py-2 font-medium text-muted transition-colors hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex-1 rounded-lg bg-accent px-3 py-2 font-medium text-accent-foreground transition-opacity hover:opacity-90 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface disabled:cursor-not-allowed disabled:opacity-60"
            >
              {submitting ? 'Enviando…' : type === 0 ? 'Depositar' : 'Sacar'}
            </button>
          </div>
        </form>
      )}
    </Modal>
  )
}
