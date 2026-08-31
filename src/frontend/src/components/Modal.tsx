import { useEffect, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

interface ModalProps {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
}

// Modal acessível: overlay, fecha com Esc e clique fora, aria-modal, foco no
// primeiro campo e trava de scroll. Sem lib de UI (ADR 0004).
export function Modal({ open, onClose, title, children }: ModalProps) {
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
    document.body.style.overflow = 'hidden'
    const focusable = dialogRef.current?.querySelector<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
    )
    focusable?.focus()

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onCloseRef.current()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [open])

  if (!open) {
    return null
  }

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-label={title}>
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" aria-hidden="true" onClick={onClose} />
      <div ref={dialogRef} className="relative w-full max-w-sm rounded-2xl border border-border bg-surface p-6 shadow-lg">
        <h2 className="mb-4 text-lg font-bold text-foreground">{title}</h2>
        {children}
      </div>
    </div>,
    document.body,
  )
}
