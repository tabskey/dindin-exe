import { describe, expect, it } from 'vitest'
import { maskAccountNumber, maskBRL, maskCpf, parseBRLToCents } from './masks'

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
  it('trata cada dígito como centavo e sempre exibe duas casas', () => {
    expect(maskBRL('')).toBe('')
    expect(maskBRL('1')).toBe('0,01')
    expect(maskBRL('12')).toBe('0,12')
    expect(maskBRL('123')).toBe('1,23')
    expect(maskBRL('5050')).toBe('50,50')
    expect(maskBRL('123456')).toBe('1.234,56')
  })

  it('agrupa milhares e limita a 12 dígitos', () => {
    expect(maskBRL('123456789012')).toBe('1.234.567.890,12')
    expect(maskBRL('99999999999999999999')).toBe('9.999.999.999,99')
  })

  it('ignora não-dígitos e zeros à esquerda', () => {
    expect(maskBRL('R$ 1.234,56')).toBe('1.234,56')
    expect(maskBRL('0005')).toBe('0,05')
  })
})

describe('parseBRLToCents', () => {
  it('converte o texto mascarado para centavos inteiros', () => {
    expect(parseBRLToCents('')).toBe(0)
    expect(parseBRLToCents('0,00')).toBe(0)
    expect(parseBRLToCents('0,01')).toBe(1)
    expect(parseBRLToCents('0,50')).toBe(50)
    expect(parseBRLToCents('50,50')).toBe(5050)
    expect(parseBRLToCents('12.345,67')).toBe(1234567)
  })
})
