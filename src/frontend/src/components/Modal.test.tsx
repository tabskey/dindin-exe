import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Modal } from './Modal'

function renderModal(overrides: { open?: boolean; onClose?: () => void; dialogClassName?: string } = {}) {
  const props = {
    open: true,
    onClose: vi.fn(),
    title: 'Título do modal',
    children: <input placeholder="Primeiro campo" />,
    ...overrides,
  }
  const utils = render(<Modal {...props} />)
  return { ...props, ...utils }
}

describe('Modal', () => {
  it('não renderiza nada quando fechado', () => {
    renderModal({ open: false })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('renderiza o título e o conteúdo', () => {
    renderModal()
    expect(screen.getByRole('dialog', { name: 'Título do modal' })).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Primeiro campo')).toBeInTheDocument()
  })

  it('fecha com a tecla Esc', async () => {
    const user = userEvent.setup()
    const { onClose } = renderModal()
    await user.keyboard('{Escape}')
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('fecha com clique fora (overlay)', async () => {
    const user = userEvent.setup()
    const { onClose } = renderModal()
    const overlay = screen.getByRole('dialog').firstElementChild
    expect(overlay).not.toBeNull()
    await user.click(overlay as HTMLElement)
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('foca o primeiro campo ao abrir e trava o scroll do body', () => {
    renderModal()
    expect(screen.getByPlaceholderText('Primeiro campo')).toHaveFocus()
    expect(document.body.style.overflow).toBe('hidden')
  })

  it('restaura o scroll do body ao desmontar', () => {
    const { unmount } = renderModal()
    expect(document.body.style.overflow).toBe('hidden')
    unmount()
    expect(document.body.style.overflow).toBe('')
  })

  it('aplica a classe personalizada ao diálogo quando informada', () => {
    renderModal({ dialogClassName: 'custom-dialog-class' })
    const dialog = screen.getByRole('dialog')
    expect(dialog.children[1]).toHaveClass('custom-dialog-class')
  })
})
