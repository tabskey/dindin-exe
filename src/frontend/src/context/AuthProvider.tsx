import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  clearToken,
  login as apiLogin,
  registerUnauthorizedHandler,
  setToken,
  type AccountDto,
} from '../lib/api'
import { AuthContext } from './auth'

const ACCOUNT_KEY = 'dindin-account'

function readStoredAccount(): AccountDto | null {
  const raw = localStorage.getItem(ACCOUNT_KEY)
  if (!raw) {
    return null
  }
  try {
    return JSON.parse(raw) as AccountDto
  } catch {
    localStorage.removeItem(ACCOUNT_KEY)
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [account, setAccount] = useState<AccountDto | null>(readStoredAccount)

  const login = useCallback(async (cpf: string, password: string) => {
    const response = await apiLogin(cpf, password)
    setToken(response.token)
    localStorage.setItem(ACCOUNT_KEY, JSON.stringify(response.account))
    setAccount(response.account)
  }, [])

  const logout = useCallback(() => {
    clearToken()
    localStorage.removeItem(ACCOUNT_KEY)
    setAccount(null)
  }, [])

  // 401 em rota autenticada (token expirado) → encerra a sessão automaticamente.
  useEffect(() => registerUnauthorizedHandler(logout), [logout])

  const value = useMemo(
    () => ({ account, isAuthenticated: account !== null, login, logout }),
    [account, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
