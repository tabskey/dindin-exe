import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/auth'

// Esqueleto da tela pós-login — a Fase 3 implementa saldo, histórico e FAB "+".
export function ExtratoPage() {
  const { account, logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <main className="min-h-svh bg-background px-4 py-6 text-foreground">
      <header className="flex items-center justify-between pr-14">
        <h1 className="text-lg font-bold">Olá, {account?.name}</h1>
        <button
          type="button"
          onClick={handleLogout}
          className="rounded-lg border border-border bg-surface px-3 py-1.5 text-sm text-muted transition-colors hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          Sair
        </button>
      </header>
      <p className="mt-10 text-center text-sm text-muted">Extrato em construção — próxima fase.</p>
    </main>
  )
}
