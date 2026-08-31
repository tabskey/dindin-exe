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

// Moeda BRL centavos-based: cada dígito digitado é um centavo (o backend trabalha
// com centavos inteiros). "1" → "0,01"; "12" → "0,12"; "123" → "1,23";
// "123456" → "1.234,56". Limite de 12 dígitos (R$ 9.999.999.999,99).
export function maskBRL(value: string): string {
  const digits = value.replace(/\D/g, '').slice(0, 12)
  if (!digits) {
    return ''
  }
  const padded = digits.padStart(3, '0')
  const reais = padded
    .slice(0, -2)
    .replace(/^0+(?=\d)/, '')
    .replace(/\B(?=(\d{3})+(?!\d))/g, '.')
  const cents = padded.slice(-2)
  return `${reais || '0'},${cents}`
}

// Converte o texto mascarado de volta para centavos inteiros ("1,23" → 123).
export function parseBRLToCents(value: string): number {
  const digits = value.replace(/\D/g, '')
  return digits ? Number(digits) : 0
}
