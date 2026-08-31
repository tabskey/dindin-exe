import { describe, expect, it } from 'vitest'
import { maskAccountNumber, maskBRL, maskCpf, parseBRL } from './masks'

describe('maskCpf', () => {
  it('retorna vazio quando não há dígitos', () => {
    expect(maskCpf('')).toBe('')
    expect(maskCpf('abc.')).toBe('')
  })

  it('formata progressivamente por quantidade de dígitos', () => {
    expect(maskCpf('1')).toBe('1')
    expect(maskCpf('111')).toBe('111')
    expect(maskCpf('111222')).toBe('111.222')
    expect(maskCpf('111222333')).toBe('111.222.333')
    expect(maskCpf('11122233344')).toBe('111.222.333-44')
  })

  it('aceita entrada já mascarada, ignora não-dígitos e limita a 11 dígitos', () => {
    expect(maskCpf('111.222.333-44')).toBe('111.222.333-44')
    expect(maskCpf('111222333445566')).toBe('111.222.333-44')
    expect(maskCpf('1a2b3c4d5e6f7g8h9i0j1k')).toBe('123.456.789-01')
  })
})

describe('maskAccountNumber', () => {
  it('formata dígitos no padrão XXXXX-XX', () => {
    expect(maskAccountNumber('00315')).toBe('00315')
    expect(maskAccountNumber('0031541')).toBe('00315-41')
  })

  it('ignora não-dígitos e limita a 7 dígitos', () => {
    expect(maskAccountNumber('00-315-41')).toBe('00315-41')
    expect(maskAccountNumber('003154177')).toBe('00315-41')
  })
})

describe('maskBRL', () => {
  it('mantém os reais como digitados e agrupa milhares', () => {
    expect(maskBRL('')).toBe('')
    expect(maskBRL('0')).toBe('0')
    expect(maskBRL('5')).toBe('5')
    expect(maskBRL('50')).toBe('50')
    expect(maskBRL('5050')).toBe('5.050')
    expect(maskBRL('1234567')).toBe('1.234.567')
  })

  it('aceita vírgula para os centavos e remove zeros à esquerda', () => {
    expect(maskBRL('50,')).toBe('50,')
    expect(maskBRL('50,5')).toBe('50,5')
    expect(maskBRL('50,50')).toBe('50,50')
    expect(maskBRL('05')).toBe('5')
    expect(maskBRL('0,50')).toBe('0,50')
  })

  it('ignora não-dígitos/vírgula e limita a 10 dígitos nos reais', () => {
    expect(maskBRL('R$ 1.234,56')).toBe('1.234,56')
    expect(maskBRL('99999999999999999999')).toBe('9.999.999.999')
  })
})

describe('parseBRL', () => {
  it('converte o texto mascarado de volta em número', () => {
    expect(parseBRL('')).toBe(0)
    expect(parseBRL('0')).toBe(0)
    expect(parseBRL('50')).toBe(50)
    expect(parseBRL('50,50')).toBe(50.5)
    expect(parseBRL('12.345,67')).toBe(12345.67)
  })
})
