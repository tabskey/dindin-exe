import { useState, type ReactNode } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Modal } from './Modal'

function renderModal(overrides: { open?: boolean; onClose?: () => void; dialogClassName?: string; children?: ReactNode } = {}) {
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

  it('trap o foco com Tab: do último elemento volta ao primeiro (e Shift+Tab inverte)', () => {
    const { container } = renderModal({
      children: (
        <>
          <input placeholder="Campo 1" />
          <input placeholder="Campo 2" />
        </>
      ),
    })
    const campo1 = screen.getByPlaceholderText('Campo 1')
    const campo2 = screen.getByPlaceholderText('Campo 2')

    // Tab no último → primeiro (sem escapar do diálogo).
    fireEvent.keyDown(campo2, { key: 'Tab' })
    expect(campo1).toHaveFocus()

    // Shift+Tab no primeiro → último.
    fireEvent.keyDown(campo1, { key: 'Tab', shiftKey: true })
    expect(campo2).toHaveFocus()

    expect(container).toBeDefined()
  })

  it('restaura o foco no gatilho ao fechar', async () => {
    const user = userEvent.setup()

    function ModalWithTrigger() {
      const [open, setOpen] = useState(false)
      return (
        <div>
          <button type="button" onClick={() => setOpen(true)}>
            Abrir
          </button>
          <Modal open={open} onClose={() => setOpen(false)} title="Título">
            <input placeholder="Campo" />
          </Modal>
        </div>
      )
    }

    render(<ModalWithTrigger />)
    const trigger = screen.getByRole('button', { name: 'Abrir' })
    await user.click(trigger)
    expect(screen.getByPlaceholderText('Campo')).toHaveFocus()

    await user.keyboard('{Escape}')
    expect(trigger).toHaveFocus()
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
