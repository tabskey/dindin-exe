// Client mínimo da API do DinDin.exe — base relativa /api (proxy do Vite em dev,
// nginx em produção). Erros no corpo { "error": "<mensagem>" } mapeados por status.

export interface AccountDto {
  id: number
  accountNumber: string
  name: string
  cpf: string
  accountType: number
  createdAt: string
}

export interface LoginResponse {
  token: string
  account: AccountDto
}

export interface BalanceDto {
  accountId: number
  balance: number
}

export type MovementType = 0 | 1 // 0 = crédito (entrada), 1 = débito (saída)

export interface MovementDto {
  id: number
  accountId: number
  type: MovementType
  amount: number
  timestamp: string
  counterparty: string | null
}

export interface MovementHistoryDto {
  items: MovementDto[]
  page: number
  pageSize: number
  total: number
}

export interface CreateMovementRequest {
  type: MovementType
  amount: number
  counterpartyCpf?: string
  counterpartyAccountNumber?: string
}

const API_BASE = '/api'
const TOKEN_KEY = 'dindin-token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

let unauthorizedHandler: (() => void) | null = null

// Registra o callback de sessão expirada (AuthProvider usa para logout automático
// em 401 de rota autenticada). Retorna o cleanup para o useEffect.
export function registerUnauthorizedHandler(handler: () => void): () => void {
  unauthorizedHandler = handler
  return () => {
    unauthorizedHandler = null
  }
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { error?: string }
    if (typeof body.error === 'string' && body.error) {
      return body.error
    }
  } catch {
    // corpo vazio ou inválido — cai no fallback por status
  }

  switch (response.status) {
    case 401:
      return 'CPF ou senha inválidos'
    case 409:
      return 'CPF já cadastrado'
    case 404:
      return 'Não encontrado'
    default:
      return 'Algo deu errado. Tente novamente.'
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getToken()
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers })

  if (!response.ok) {
    if (response.status === 401 && token) {
      unauthorizedHandler?.()
    }
    throw new ApiError(response.status, await errorMessage(response))
  }

  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

export function login(cpf: string, password: string): Promise<LoginResponse> {
  return request('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ cpf, password }),
  })
}

export function createAccount(
  input: { name: string; cpf: string; password: string },
  idempotencyKey?: string,
): Promise<AccountDto> {
  return request('/accounts', {
    method: 'POST',
    body: JSON.stringify({ ...input, accountType: 0 }),
    headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
  })
}

export function getBalance(accountId: number): Promise<BalanceDto> {
  return request(`/accounts/${accountId}/balance`)
}

export function getMovements(accountId: number, page = 1, pageSize = 20): Promise<MovementHistoryDto> {
  return request(`/accounts/${accountId}/movements?page=${page}&pageSize=${pageSize}`)
}

export function createMovement(
  accountId: number,
  input: CreateMovementRequest,
  idempotencyKey: string,
): Promise<MovementDto> {
  return request(`/accounts/${accountId}/movements`, {
    method: 'POST',
    body: JSON.stringify(input),
    headers: { 'Idempotency-Key': idempotencyKey },
  })
}
