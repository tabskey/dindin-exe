import { ArrowLeft, Image, Upload, X } from 'lucide-react'
import { useRef, useState, type ChangeEvent } from 'react'
import { updateAvatar } from '../lib/api'
import { Modal } from './Modal'

interface AvatarModalProps {
  open: boolean
  accountId: number
  name: string
  initials: string
  avatarUrl: string | null
  onClose: () => void
  // Chamado após um upload bem-sucedido para o pai recarregar o avatar.
  onAvatarUpdated: () => void
}

export function AvatarModal({
  open,
  accountId,
  name,
  initials,
  avatarUrl,
  onClose,
  onAvatarUpdated,
}: AvatarModalProps) {
  const [viewing, setViewing] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const fileInputRef = useRef<HTMLInputElement>(null)

  async function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    // Zera o valor cedo para permitir escolher o mesmo arquivo de novo.
    event.target.value = ''
    if (!file) {
      return
    }

    setError('')
    setUploading(true)
    try {
      await updateAvatar(accountId, file)
      onAvatarUpdated()
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível trocar o avatar.')
    } finally {
      setUploading(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={viewing ? 'Seu avatar' : 'Avatar'}
      // Visualização: diálogo mais largo para a imagem em tamanho original.
      dialogClassName={
        viewing
          ? 'relative w-fit max-w-[calc(100vw-2rem)] rounded-2xl border border-border bg-surface p-6 shadow-lg'
          : undefined
      }
    >
      {/* Navegação no topo do diálogo: X fecha; setinha volta (só na visualização). */}
      <div className="absolute right-3 top-3">
        <button
          type="button"
          aria-label="Fechar"
          onClick={onClose}
          className="rounded-full p-2 text-muted transition-colors hover:bg-foreground/10 hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <X className="size-5" />
        </button>
      </div>
      {viewing && (
        <div className="absolute left-3 top-3">
          <button
            type="button"
            aria-label="Voltar"
            onClick={() => setViewing(false)}
            className="rounded-full p-2 text-muted transition-colors hover:bg-foreground/10 hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <ArrowLeft className="size-5" />
          </button>
        </div>
      )}

      {viewing ? (
        <div className="flex flex-col items-center pt-2">
          {avatarUrl ? (
            // Tamanho original limitado a 800px de altura; no celular cabe na tela (75svh).
            <img
              src={avatarUrl}
              alt={`Avatar de ${name}`}
              className="max-h-[min(800px,75svh)] max-w-full rounded-xl object-contain"
            />
          ) : (
            <div className="flex size-40 items-center justify-center rounded-full bg-accent text-4xl font-bold text-accent-foreground">
              {initials}
            </div>
          )}
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          <button
            type="button"
            onClick={() => setViewing(true)}
            className="flex items-center justify-center gap-2 rounded-lg border border-border px-3 py-2 font-medium text-foreground transition-colors hover:border-accent focus:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <Image className="size-4" />
            Ver imagem de perfil
          </button>
          <button
            type="button"
            disabled={uploading}
            onClick={() => fileInputRef.current?.click()}
            className="flex items-center justify-center gap-2 rounded-lg border border-border px-3 py-2 font-medium text-foreground transition-colors hover:border-accent focus:outline-none focus-visible:ring-2 focus-visible:ring-accent disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Upload className="size-4" />
            {uploading ? 'Enviando…' : 'Trocar imagem de perfil'}
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*"
            className="hidden"
            onChange={(event) => void handleFileChange(event)}
          />
          {error && <p className="text-sm text-expense">{error}</p>}
        </div>
      )}
    </Modal>
  )
}
