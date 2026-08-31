import { useCallback, useEffect, useRef, useState } from 'react'
import { Plus } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/auth'
import { AvatarModal } from '../components/AvatarModal'
import { MovementModal } from '../components/MovementModal'
import { getAvatar, getBalance, getMovements, type MovementDto } from '../lib/api'

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

// Iniciais para o fallback do avatar: "Ana Teste" → "AT"; nome único → 2 primeiras letras.
function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/)
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase()
  }

  const first = parts[0]?.at(0) ?? ''
  const last = parts.at(-1)?.at(0) ?? ''
  return (first + last).toUpperCase()
}

// Fetch puro, sem setState — chamável de effect/handler sem acionar a regra
// react-hooks/set-state-in-effect; o componente aplica o resultado em callbacks
// (setState em callback assíncrono é permitido pela regra).
async function fetchExtrato(accountId: number) {
  const [balanceData, history, avatarBlob] = await Promise.all([
    getBalance(accountId),
    getMovements(accountId),
    // Avatar ausente (404 → null) ou falho não derruba o extrato.
    getAvatar(accountId).catch(() => null),
  ])
  return {
    balance: balanceData.balance,
    items: history.items,
    avatar: avatarBlob ? URL.createObjectURL(avatarBlob) : null,
  }
}

export function ExtratoPage() {
  const { account, logout } = useAuth()
  const navigate = useNavigate()

  const [balance, setBalance] = useState<number | null>(null)
  const [movements, setMovements] = useState<MovementDto[]>([])
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const avatarUrlRef = useRef<string | null>(null)
  const [avatarMenuOpen, setAvatarMenuOpen] = useState(false)
  const [movementOpen, setMovementOpen] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const loadExtrato = useCallback(() => {
    if (!account) {
      return
    }
    fetchExtrato(account.id)
      .then((data) => {
        avatarUrlRef.current = data.avatar
        setBalance(data.balance)
        setMovements(data.items)
        setAvatarUrl(data.avatar)
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Não foi possível carregar o extrato.')
      })
      .finally(() => {
        setLoading(false)
      })
  }, [account])

  // Recarrega só o avatar (após upload bem-sucedido); falha mantém o atual.
  const reloadAvatar = useCallback(() => {
    if (!account) {
      return
    }
    getAvatar(account.id)
      .then((blob) => {
        const next = blob ? URL.createObjectURL(blob) : null
        if (avatarUrlRef.current) {
          URL.revokeObjectURL(avatarUrlRef.current)
        }
        avatarUrlRef.current = next
        setAvatarUrl(next)
      })
      .catch(() => {
        // Avatar indisponível: mantém o que já está na tela.
      })
  }, [account])

  // Revoga o object URL do avatar ao desmontar (evita vazamento de memória).
  useEffect(() => {
    return () => {
      if (avatarUrlRef.current) {
        URL.revokeObjectURL(avatarUrlRef.current)
      }
    }
  }, [])

  useEffect(() => {
    loadExtrato()
  }, [loadExtrato])

  function handleRetry() {
    setError('')
    setLoading(true)
    loadExtrato()
  }

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  const initials = account ? getInitials(account.name) : ''

  return (
    <main className="min-h-svh bg-background px-4 py-6 text-foreground">
      <div className="mx-auto w-full max-w-md">
        <header className="flex items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <button
              type="button"
              aria-label="Opções do avatar"
              onClick={() => setAvatarMenuOpen(true)}
              className="flex size-10 shrink-0 cursor-pointer items-center justify-center overflow-hidden rounded-full bg-accent text-sm font-bold text-accent-foreground transition-transform hover:scale-105 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
            >
              {avatarUrl ? (
                <img src={avatarUrl} alt={`Avatar de ${account?.name ?? ''}`} className="size-full object-cover" />
              ) : (
                initials
              )}
            </button>
            <h1 className="truncate">
              <span className="font-bree text-xl text-accent">Olá,</span>
              <span className="text-lg font-bold"> {account?.name}</span>
            </h1>
          </div>
          <button
            type="button"
            onClick={handleLogout}
            className="rounded-lg border border-border bg-surface px-3 py-1.5 text-sm text-muted transition-colors hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            Sair
          </button>
        </header>

        {loading && <p className="mt-10 text-center text-sm text-muted">Carregando…</p>}

        {!loading && error && (
          <div className="mt-10 w-full rounded-xl border border-border bg-surface p-6 text-center">
            <p className="text-sm text-foreground">{error}</p>
            <button
              type="button"
              onClick={handleRetry}
              className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-medium text-accent-foreground transition-opacity hover:opacity-90 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
            >
              Tentar novamente
            </button>
          </div>
        )}

        {!loading && !error && (
          <section className="mt-6 w-full">
            <div className="rounded-2xl border border-border bg-balance-bg p-6">
              <p className="text-sm text-muted">Saldo</p>
              <p className="mt-1 text-3xl font-bold tabular-nums">{brl.format(balance ?? 0)}</p>
            </div>

            <h2 className="mb-2 mt-8 text-sm font-semibold text-muted">Movimentações</h2>
            <ul className="space-y-2">
              {movements.length === 0 ? (
                <li className="rounded-xl border border-border bg-surface p-4 text-center text-sm text-muted">
                  Nenhuma movimentação ainda.
                </li>
              ) : (
                movements.map((movement) => (
                  <li
                    key={movement.id}
                    className={`flex items-center justify-between gap-3 rounded-xl border border-border p-4 ${
                      movement.type === 0 ? 'bg-income-bg' : 'bg-expense-bg'
                    }`}
                  >
                    <div>
                      <p className="text-sm font-medium">{dateFormatter.format(new Date(movement.timestamp))}</p>
                      <p className="text-xs text-muted">{movement.counterparty ?? 'Boca do caixa'}</p>
                    </div>
                    <span
                      className={`text-base font-semibold tabular-nums ${
                        movement.type === 0 ? 'text-income' : 'text-expense'
                      }`}
                    >
                      {movement.type === 0 ? '+' : '-'} {brl.format(movement.amount)}
                    </span>
                  </li>
                ))
              )}
            </ul>
          </section>
        )}
      </div>

      <button
        type="button"
        aria-label="Nova movimentação"
        onClick={() => setMovementOpen(true)}
        className="fixed bottom-6 right-6 z-40 flex size-14 items-center justify-center rounded-full bg-accent text-accent-foreground shadow-lg transition-transform hover:scale-105 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
      >
        <Plus className="size-7" />
      </button>

      {account && (
        <AvatarModal
          open={avatarMenuOpen}
          accountId={account.id}
          name={account.name}
          initials={initials}
          avatarUrl={avatarUrl}
          onClose={() => setAvatarMenuOpen(false)}
          onAvatarUpdated={reloadAvatar}
        />
      )}

      {account && (
        <MovementModal
          open={movementOpen}
          accountId={account.id}
          onClose={() => setMovementOpen(false)}
          // Refresh silencioso: saldo e histórico atualizados após a movimentação.
          onSuccess={loadExtrato}
        />
      )}
    </main>
  )
}
