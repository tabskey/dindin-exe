import { createContext, useContext } from 'react'
import type { AccountDto } from '../lib/api'

export interface AuthContextValue {
  account: AccountDto | null
  isAuthenticated: boolean
  login: (cpf: string, password: string) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth deve ser usado dentro de <AuthProvider>.')
  }
  return context
}
