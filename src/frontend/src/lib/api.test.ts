import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  ApiError,
  clearToken,
  createAccount,
  createMovement,
  getAvatar,
  getBalance,
  getMovements,
  getToken,
  login,
  registerUnauthorizedHandler,
  setToken,
  updateAvatar,
} from './api'

const fetchMock = vi.fn()

// Response mínimo com o que o client usa: ok, status, json e blob.
function fakeResponse(status: number, body?: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => {
      if (body === undefined) {
        throw new SyntaxError('Unexpected end of JSON input')
      }
      return body
    },
    blob: async () => new Blob(['avatar-bytes'], { type: 'image/png' }),
  } as unknown as Response
}

beforeEach(() => {
  localStorage.clear()
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('token helpers', () => {
  it('getToken devolve null sem token e o valor salvo após setToken; clearToken remove', () => {
    expect(getToken()).toBeNull()
    setToken('abc')
    expect(getToken()).toBe('abc')
    clearToken()
    expect(getToken()).toBeNull()
  })
})

describe('request (núcleo do client)', () => {
  it('envia Authorization (com token) + Content-Type JSON e devolve o JSON', async () => {
    setToken('token-123')
    fetchMock.mockResolvedValue(fakeResponse(200, { accountId: 1, balance: 125050 }))

    const balance = await getBalance(1)

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/accounts/1/balance')
    const headers = init?.headers as Headers
    expect(headers.get('Authorization')).toBe('Bearer token-123')
    expect(headers.get('Content-Type')).toBe('application/json')
    expect(balance).toEqual({ accountId: 1, balance: 125050 })
  })

  it('lança ApiError com a mensagem do corpo { error }', async () => {
    fetchMock.mockResolvedValue(fakeResponse(400, { error: 'Valor inválido' }))

    await expect(getBalance(1)).rejects.toEqual(new ApiError(400, 'Valor inválido'))
  })

  it.each([
    [401, 'CPF ou senha inválidos'],
    [409, 'CPF já cadastrado'],
    [404, 'Não encontrado'],
    [500, 'Algo deu errado. Tente novamente.'],
  ])('mapeia erro %i sem corpo útil para "%s"', async (status, message) => {
    fetchMock.mockResolvedValue(fakeResponse(status, {}))

    await expect(getBalance(1)).rejects.toEqual(new ApiError(status, message))
  })

  it('corpo inválido cai no fallback por status', async () => {
    fetchMock.mockResolvedValue(fakeResponse(502, undefined))

    await expect(getBalance(1)).rejects.toEqual(new ApiError(502, 'Algo deu errado. Tente novamente.'))
  })

  it('401 com token dispara o handler de sessão expirada', async () => {
    const handler = vi.fn()
    const cleanup = registerUnauthorizedHandler(handler)
    setToken('token-123')
    fetchMock.mockResolvedValue(fakeResponse(401, { error: 'Sessão expirada' }))

    await expect(getBalance(1)).rejects.toThrow(ApiError)
    expect(handler).toHaveBeenCalledTimes(1)

    // O cleanup remove o handler: o próximo 401 não chama mais.
    cleanup()
    fetchMock.mockResolvedValue(fakeResponse(401, { error: 'Sessão expirada' }))
    await expect(getBalance(1)).rejects.toThrow(ApiError)
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('resposta 204 devolve undefined', async () => {
    fetchMock.mockResolvedValue(fakeResponse(204))

    await expect(getMovements(1)).resolves.toBeUndefined()
  })
})

describe('login', () => {
  it('faz POST em /auth/login com CPF e senha e devolve token + conta', async () => {
    fetchMock.mockResolvedValue(
      fakeResponse(200, { token: 'jwt', account: { id: 1, name: 'Ana' } }),
    )

    const result = await login('111.222.333-44', 'senha123')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/auth/login')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({ cpf: '111.222.333-44', password: 'senha123' })
    expect(result).toEqual({ token: 'jwt', account: { id: 1, name: 'Ana' } })
  })
})

describe('createAccount', () => {
  const input = { name: 'Novo User', cpf: '444.444.444-44', password: 'senha123', accountType: 1 }

  it('faz POST em /accounts com o accountType escolhido', async () => {
    fetchMock.mockResolvedValue(fakeResponse(201, { id: 2, ...input }))

    await createAccount(input)

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/accounts')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual(input)
  })

  it('envia Idempotency-Key quando fornecida', async () => {
    fetchMock.mockResolvedValue(fakeResponse(201, { id: 2, ...input }))

    await createAccount(input, 'k-1')

    const [, init] = fetchMock.mock.calls[0]
    const headers = init?.headers as Headers
    expect(headers.get('Idempotency-Key')).toBe('k-1')
  })
})

describe('getMovements', () => {
  it('usa page/pageSize na query e aplica os padrões 1/20', async () => {
    fetchMock.mockResolvedValue(fakeResponse(200, { items: [], page: 1, pageSize: 20, total: 0 }))

    await getMovements(1)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/accounts/1/movements?page=1&pageSize=20')

    await getMovements(1, 2, 10)
    expect(fetchMock.mock.calls[1][0]).toBe('/api/accounts/1/movements?page=2&pageSize=10')
  })
})

describe('createMovement', () => {
  it('faz POST com o payload e a Idempotency-Key', async () => {
    fetchMock.mockResolvedValue(
      fakeResponse(201, { id: 9, accountId: 3, type: 0, amount: 5050, timestamp: '', counterparty: null }),
    )

    await createMovement(3, { type: 0, amount: 5050, counterpartyCpf: '111.222.333-44' }, 'key-9')

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/accounts/3/movements')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({ type: 0, amount: 5050, counterpartyCpf: '111.222.333-44' })
    const headers = init?.headers as Headers
    expect(headers.get('Idempotency-Key')).toBe('key-9')
  })
})

describe('getAvatar', () => {
  it('devolve null para 404 (sem avatar)', async () => {
    fetchMock.mockResolvedValue(fakeResponse(404))

    await expect(getAvatar(1)).resolves.toBeNull()
  })

  it('devolve o blob da imagem quando existe', async () => {
    fetchMock.mockResolvedValue(fakeResponse(200))

    const blob = await getAvatar(1)
    expect(blob).toBeInstanceOf(Blob)
    expect(await blob?.text()).toBe('avatar-bytes')
  })

  it('lança ApiError em erro diferente de 404', async () => {
    fetchMock.mockResolvedValue(fakeResponse(500, { error: 'Erro interno' }))

    await expect(getAvatar(1)).rejects.toEqual(new ApiError(500, 'Erro interno'))
  })

  it('envia Authorization no GET do avatar e dispara o handler em 401 com token', async () => {
    const handler = vi.fn()
    const cleanup = registerUnauthorizedHandler(handler)
    setToken('token-123')
    fetchMock.mockResolvedValue(fakeResponse(200))

    await getAvatar(1)
    const headers = fetchMock.mock.calls[0][1]?.headers as Headers
    expect(headers.get('Authorization')).toBe('Bearer token-123')

    fetchMock.mockResolvedValue(fakeResponse(401, { error: 'Sessão expirada' }))
    await expect(getAvatar(1)).rejects.toEqual(new ApiError(401, 'Sessão expirada'))
    expect(handler).toHaveBeenCalledTimes(1)
    cleanup()
  })
})

describe('updateAvatar', () => {
  it('faz POST multipart sem Content-Type manual e resolve', async () => {
    setToken('token-123')
    fetchMock.mockResolvedValue(fakeResponse(200))
    const file = new File(['png-bytes'], 'avatar.png', { type: 'image/png' })

    await updateAvatar(1, file)

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/accounts/1/avatar')
    expect(init?.method).toBe('POST')
    expect(init?.body).toBeInstanceOf(FormData)
    const headers = init?.headers as Headers
    expect(headers.get('Authorization')).toBe('Bearer token-123')
    expect(headers.get('Content-Type')).toBeNull()
  })

  it('lança ApiError quando o upload falha', async () => {
    fetchMock.mockResolvedValue(fakeResponse(413, { error: 'Imagem muito grande' }))
    const file = new File(['png-bytes'], 'avatar.png', { type: 'image/png' })

    await expect(updateAvatar(1, file)).rejects.toEqual(new ApiError(413, 'Imagem muito grande'))
  })

  it('dispara o handler de sessão expirada em 401 com token', async () => {
    const handler = vi.fn()
    const cleanup = registerUnauthorizedHandler(handler)
    setToken('token-123')
    fetchMock.mockResolvedValue(fakeResponse(401, { error: 'Sessão expirada' }))
    const file = new File(['png-bytes'], 'avatar.png', { type: 'image/png' })

    await expect(updateAvatar(1, file)).rejects.toEqual(new ApiError(401, 'Sessão expirada'))
    expect(handler).toHaveBeenCalledTimes(1)
    cleanup()
  })
})
