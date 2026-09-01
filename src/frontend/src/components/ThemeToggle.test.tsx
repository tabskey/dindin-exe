import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { ThemeToggle } from './ThemeToggle'

afterEach(() => {
  localStorage.clear()
  document.documentElement.classList.remove('dark')
})

describe('ThemeToggle', () => {
  it('alterna o tema ao clicar, atualizando o <html> e o localStorage', async () => {
    const user = userEvent.setup()
    render(<ThemeToggle />)
    expect(screen.getByRole('button', { name: 'Ativar modo escuro' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Ativar modo escuro' }))
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem('dindin-theme')).toBe('dark')
    expect(screen.getByRole('button', { name: 'Ativar modo claro' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Ativar modo claro' }))
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(localStorage.getItem('dindin-theme')).toBe('light')
  })

  it('respeita o tema salvo no localStorage', () => {
    localStorage.setItem('dindin-theme', 'dark')
    render(<ThemeToggle />)
    expect(screen.getByRole('button', { name: 'Ativar modo claro' })).toBeInTheDocument()
  })
})
