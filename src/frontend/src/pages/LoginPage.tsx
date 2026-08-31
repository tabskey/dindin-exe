import { useState, type FormEvent } from 'react'

export function LoginPage() {
  const [cpf, setCpf] = useState('')
  const [password, setPassword] = useState('')

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    // Próxima etapa: chamar POST /auth/login (JWT) e redirecionar para o extrato.
  }

  return (
    <main className="flex min-h-svh items-center justify-center px-4">
      <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-8 shadow-sm">
        <header className="mb-8 text-center">
          <h1 className="text-2xl font-bold text-foreground">DinDin.exe</h1>
          <p className="mt-1 text-sm text-muted">Acesse sua conta</p>
        </header>

        <form onSubmit={handleSubmit} className="space-y-4">
          <label className="block">
            <span className="text-sm font-medium text-foreground">CPF</span>
            <input
              type="text"
              inputMode="numeric"
              autoComplete="username"
              placeholder="000.000.000-00"
              value={cpf}
              onChange={(event) => setCpf(event.target.value)}
              className="mt-1 w-full rounded-lg border border-border bg-background px-3 py-2 text-foreground placeholder:text-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/40"
            />
          </label>

          <label className="block">
            <span className="text-sm font-medium text-foreground">Senha</span>
            <input
              type="password"
              autoComplete="current-password"
              placeholder="Sua senha"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="mt-1 w-full rounded-lg border border-border bg-background px-3 py-2 text-foreground placeholder:text-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/40"
            />
          </label>

          <button
            type="submit"
            className="w-full rounded-lg bg-accent px-3 py-2 font-medium text-accent-foreground transition-opacity hover:opacity-90 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
          >
            Entrar
          </button>
        </form>

        <p className="mt-6 text-center text-xs text-muted">
          Contas de teste: 111.111.111-11 · senha123
        </p>
      </div>
    </main>
  )
}
