// Máscaras locais de formatação (sem libs externas).

// CPF: dígitos → XXX.XXX.XXX-XX (progressiva por tamanho; aceita entrada já mascarada).
export function maskCpf(value: string): string {
  const digits = value.replace(/\D/g, '').slice(0, 11)
  if (digits.length === 0) {
    return ''
  }
  if (digits.length <= 3) {
    return digits
  }
  if (digits.length <= 6) {
    return `${digits.slice(0, 3)}.${digits.slice(3)}`
  }
  if (digits.length <= 9) {
    return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6)}`
  }
  return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6, 9)}-${digits.slice(9)}`
}

// Número da conta: dígitos → XXXXX-XX (formato do backend, ex.: 00315-41).
export function maskAccountNumber(value: string): string {
  const digits = value.replace(/\D/g, '').slice(0, 7)
  if (digits.length <= 5) {
    return digits
  }
  return `${digits.slice(0, 5)}-${digits.slice(5)}`
}

// Moeda BRL: reais como digitados + vírgula para centavos → "1.234,56".
// Ex.: "50" → "50"; "50,5" → "50,5"; "1234567" → "1.234.567". Com complete=true
// os centavos são completados ("50" → "50,00"; "50,5" → "50,50"). Limite de 10
// dígitos nos reais (R$ 9.999.999.999,99 — o teto decimal(18,2) do backend é maior).
export function maskBRL(value: string, complete = false): string {
  const cleaned = value.replace(/[^\d,]/g, '')
  if (!cleaned) {
    return ''
  }
  const hasComma = cleaned.includes(',')
  const [intPart, decPart] = cleaned.split(',')
  const intDigits = intPart.replace(/\D/g, '').replace(/^0+(?=\d)/, '').slice(0, 10)
  const decDigits = (decPart ?? '').replace(/\D/g, '').slice(0, 2)
  const reais = (intDigits || '0').replace(/\B(?=(\d{3})+(?!\d))/g, '.')
  if (!hasComma) {
    return complete ? `${reais},00` : reais
  }
  return complete ? `${reais},${decDigits.padEnd(2, '0')}` : decDigits ? `${reais},${decDigits}` : `${reais},`
}

// Converte o texto mascarado de volta para número (pt-BR → Number).
export function parseBRL(value: string): number {
  if (!value) {
    return 0
  }
  const normalized = value.replace(/\./g, '').replace(',', '.')
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : 0
}
