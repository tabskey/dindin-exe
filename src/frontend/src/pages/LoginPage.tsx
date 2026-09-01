import { useState } from 'react'
import { IdCard, Lock } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { CreateAccountModal } from '../components/CreateAccountModal'
import { ThemeToggle } from '../components/ThemeToggle'
import { useAuth } from '../context/auth'
import { maskCpf } from '../lib/masks'
import coinArt from '../assets/coin-art.svg'
import '../assets/login-coin.css'
import '../assets/login-pixel-bg.css'

export function LoginPage() {
  const navigate = useNavigate()
  const { login } = useAuth()

  const [cpf, setCpf] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)

  async function handleSubmit(event: React.SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting) {
      return
    }
    setError('')
    setSubmitting(true)
    try {
      await login(maskCpf(cpf), password)
      navigate('/extrato', { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível entrar. Tente novamente.')
    } finally {
      setSubmitting(false)
    }
  }

  const inputClasses =
    'mt-1 w-full rounded-lg border border-border bg-background px-3 py-2 text-foreground placeholder:text-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/40'

  return (
    <main className="pixel-bg flex min-h-svh items-center justify-center px-4">
      <ThemeToggle />
      <div className="relative flex w-full max-w-sm flex-col items-center">
        <div className="login-coin-wrap" aria-label="Moeda girando do DinDin.EXE">
          <div className="login-coin-stage">
            <div className="login-coin-spin">
              <img src={coinArt} alt="" className="login-coin-image" />
              <div className="login-coin-shine" aria-hidden="true" />
            </div>
          </div>
        </div>

        <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-8 shadow-xl shadow-black/25">
          <header className="mb-8 text-center">
            <h1 className="brand-title" aria-label="DinDin.EXE">
              <span>DinDin</span>
              <span className="brand-exe">.EXE</span>
            </h1>
            <p className="mt-1 text-sm text-muted">Acesse sua conta</p>
          </header>

        <form onSubmit={handleSubmit} className="space-y-4">
          <label className="block">
            <span className="text-sm font-medium text-foreground">CPF</span>
            <div className="relative">
              <IdCard className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                type="text"
                inputMode="numeric"
                autoComplete="username"
                placeholder="000.000.000-00"
                value={cpf}
                onChange={(event) => setCpf(event.target.value.replace(/\D/g, '').slice(0, 11))}
                onBlur={() => setCpf(maskCpf(cpf))}
                className={`${inputClasses} pl-10`}
              />
            </div>
            <span className="mt-1 block text-xs text-muted">
              Pode digitar só os números — o campo formata ao sair.
            </span>
          </label>

          <label className="block">
            <span className="text-sm font-medium text-foreground">Senha</span>
            <div className="relative">
              <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                type="password"
                autoComplete="current-password"
                placeholder="Sua senha"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                className={`${inputClasses} pl-10`}
              />
            </div>
          </label>

          {error && <p className="text-sm text-expense">{error}</p>}

          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-lg bg-accent px-3 py-2 font-medium text-accent-foreground transition-opacity hover:opacity-90 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting ? 'Entrando…' : 'Entrar'}
          </button>
        </form>

        <div className="mt-6 border-t border-border pt-4 text-center">
          <p className="text-xs text-muted">Não tem conta?</p>
          <button
            type="button"
            onClick={() => setCreateOpen(true)}
            className="mt-1 rounded font-medium text-accent hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            Criar conta
          </button>
        </div>

        <p className="mt-4 text-center text-xs text-muted">
          Contas de teste: 111.111.111-11 · senha123
        </p>
      </div>
    </div>

    <CreateAccountModal
      open={createOpen}
      onClose={() => setCreateOpen(false)}
      onCreated={(newCpf) => setCpf(newCpf)}
    />
    </main>
  )
}
