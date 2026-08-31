import { describe, expect, it } from 'vitest'
import { maskAccountNumber, maskCpf } from './masks'

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
