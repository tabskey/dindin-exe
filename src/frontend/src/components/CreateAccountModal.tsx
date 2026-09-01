import { useState } from 'react'
import { createAccount } from '../lib/api'
import { maskCpf } from '../lib/masks'
import { Modal } from './Modal'

interface CreateAccountModalProps {
  readonly open: boolean
  readonly onClose: () => void
  readonly onCreated: (cpf: string) => void
}

export function CreateAccountModal({ open, onClose, onCreated }: CreateAccountModalProps) {
  const [name, setName] = useState('')
  const [cpf, setCpf] = useState('')
  const [password, setPassword] = useState('')
  const [accountType, setAccountType] = useState(0)
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  function reset() {
    setName('')
    setCpf('')
    setPassword('')
    setAccountType(0)
    setError('')
  }

  async function handleSubmit(event: React.SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting) {
      return
    }
    setError('')

    if (!name.trim()) {
      setError('Informe seu nome.')
      return
    }
    if (cpf.replace(/\D/g, '').length !== 11) {
      setError('O CPF precisa ter 11 dígitos.')
      return
    }
    if (password.length < 6) {
      setError('A senha precisa ter pelo menos 6 caracteres.')
      return
    }

    setSubmitting(true)
    const normalizedCpf = maskCpf(cpf)
    try {
      // Chave por tentativa: um replay da mesma tentativa não duplica a conta.
      await createAccount({ name: name.trim(), cpf: normalizedCpf, password, accountType }, crypto.randomUUID())
      reset()
      onClose()
      // Pré-preenche o CPF no login; a senha fica vazia para o usuário digitar.
      onCreated(normalizedCpf)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível criar a conta. Tente novamente.')
    } finally {
      setSubmitting(false)
    }
  }

  const inputClasses =
    'mt-1 w-full rounded-lg border border-border bg-background px-3 py-2 text-foreground placeholder:text-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/40'

  return (
    <Modal open={open} onClose={onClose} title="Criar conta">
      <form onSubmit={handleSubmit} className="space-y-4">
        <label className="block">
          <span className="text-sm font-medium text-foreground">Nome</span>
          <input
            type="text"
            autoComplete="name"
            placeholder="Seu nome"
            value={name}
            onChange={(event) => setName(event.target.value)}
            className={inputClasses}
          />
        </label>

        <label className="block">
          <span className="text-sm font-medium text-foreground">CPF</span>
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
        </label>

        <label className="block">
          <span className="text-sm font-medium text-foreground">Senha</span>
          <input
            type="password"
            autoComplete="new-password"
            placeholder="Mínimo 6 caracteres"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className={inputClasses}
          />
        </label>

        <fieldset className="block">
          <legend className="text-sm font-medium text-foreground">Tipo de conta</legend>
          <div className="mt-1 flex gap-3">
            <label className="flex flex-1 cursor-pointer items-center gap-2 rounded-lg border border-border bg-background px-3 py-2">
              <input
                type="radio"
                name="accountType"
                value={0}
                checked={accountType === 0}
                onChange={() => setAccountType(0)}
                className="size-4 accent-accent"
              />
              <span className="text-sm text-foreground">Conta Corrente</span>
            </label>
            <label className="flex flex-1 cursor-pointer items-center gap-2 rounded-lg border border-border bg-background px-3 py-2">
              <input
                type="radio"
                name="accountType"
                value={1}
                checked={accountType === 1}
                onChange={() => setAccountType(1)}
                className="size-4 accent-accent"
              />
              <span className="text-sm text-foreground">Conta Poupança</span>
            </label>
          </div>
        </fieldset>

        {error && <p className="text-sm text-expense">{error}</p>}

        <div className="flex gap-3 pt-1">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded-lg border border-border px-3 py-2 font-medium text-muted transition-colors hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            Cancelar
          </button>
          <button
            type="submit"
            disabled={submitting}
            className="flex-1 rounded-lg bg-accent px-3 py-2 font-medium text-accent-foreground transition-opacity hover:opacity-90 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting ? 'Criando…' : 'Criar'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
