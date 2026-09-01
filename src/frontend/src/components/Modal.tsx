import { useEffect, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

interface ModalProps {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  // Classes do diálogo — usado para alargar o conteúdo (ex.: avatar em tamanho original).
  dialogClassName?: string
}

// Modal acessível: overlay, fecha com Esc e clique fora, aria-modal, foco no
// primeiro campo e trava de scroll. Sem lib de UI (ADR 0004).
export function Modal({ open, onClose, title, children, dialogClassName }: ModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)

  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    if (!open) {
      return
    }

    const previousOverflow = document.body.style.overflow
    const previouslyFocused = document.activeElement as HTMLElement | null
    document.body.style.overflow = 'hidden'
    const focusable = dialogRef.current?.querySelector<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
    )
    focusable?.focus()

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onCloseRef.current()
        return
      }

      // Focus trap: Tab cicla dentro do diálogo em vez de escapar para a página.
      if (event.key === 'Tab') {
        const dialog = dialogRef.current
        if (!dialog) {
          return
        }
        const focusables = Array.from(
          dialog.querySelectorAll<HTMLElement>(
            'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
          ),
        ).filter((element) => !element.hasAttribute('disabled'))
        if (focusables.length === 0) {
          event.preventDefault()
          return
        }
        const first = focusables[0]
        const last = focusables[focusables.length - 1]
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault()
          last.focus()
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault()
          first.focus()
        }
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', handleKeyDown)
      // Restaura o foco no elemento que abriu o modal.
      previouslyFocused?.focus()
    }
  }, [open])

  if (!open) {
    return null
  }

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-label={title}>
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" aria-hidden="true" onClick={onClose} />
      <div ref={dialogRef} className={dialogClassName ?? 'relative w-full max-w-sm rounded-2xl border border-border bg-surface p-14 shadow-lg'}>
        <h2 className="mb-4 text-center font-['Bree_Serif',serif] text-3xl text-foreground sm:text-4xl">{title}</h2>
        {children}
      </div>
    </div>,
    document.body,
  )
}
