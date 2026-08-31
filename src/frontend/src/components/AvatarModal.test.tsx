import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AvatarModal } from './AvatarModal'

vi.mock('../lib/api', () => ({
  updateAvatar: vi.fn(),
}))

import { updateAvatar } from '../lib/api'

const mockUpdateAvatar = vi.mocked(updateAvatar)

const defaultProps = {
  open: true,
  accountId: 1,
  name: 'Ana Teste',
  initials: 'AT',
  avatarUrl: null,
  onClose: vi.fn(),
  onAvatarUpdated: vi.fn(),
}

function fileInput(): HTMLInputElement {
  const input = document.querySelector<HTMLInputElement>('input[type="file"]')
  if (!input) {
    throw new Error('input[type="file"] não encontrado no DOM')
  }
  return input
}

beforeEach(() => {
  vi.clearAllMocks()
})

describe('AvatarModal', () => {
  it('não renderiza quando fechado', () => {
    render(<AvatarModal {...defaultProps} open={false} />)
    expect(screen.queryByText('Ver imagem de perfil')).not.toBeInTheDocument()
  })

  it('mostra as duas opções quando aberto', () => {
    render(<AvatarModal {...defaultProps} />)
    expect(screen.getByRole('button', { name: 'Ver imagem de perfil' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Trocar imagem de perfil' })).toBeInTheDocument()
  })

  it('ver a imagem sem avatar mostra as iniciais grandes e permite voltar', async () => {
    const user = userEvent.setup()
    render(<AvatarModal {...defaultProps} />)

    await user.click(screen.getByRole('button', { name: 'Ver imagem de perfil' }))
    expect(screen.getByText('AT')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Fechar' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Voltar' }))
    expect(screen.getByRole('button', { name: 'Trocar imagem de perfil' })).toBeInTheDocument()
  })

  it('ver a imagem com avatar mostra a imagem grande', async () => {
    const user = userEvent.setup()
    render(<AvatarModal {...defaultProps} avatarUrl="blob:avatar-mock" />)

    await user.click(screen.getByRole('button', { name: 'Ver imagem de perfil' }))
    const img = screen.getByAltText('Avatar de Ana Teste')
    expect(img).toHaveAttribute('src', 'blob:avatar-mock')
    // Tamanho original limitado a 800px de altura; no celular cabe na tela.
    expect(img).toHaveClass('max-h-[min(800px,75svh)]')
  })

  it('envia o arquivo escolhido, avisa o pai e fecha ao concluir', async () => {
    const onClose = vi.fn()
    const onAvatarUpdated = vi.fn()
    mockUpdateAvatar.mockResolvedValue(undefined)
    render(<AvatarModal {...defaultProps} onClose={onClose} onAvatarUpdated={onAvatarUpdated} />)

    const file = new File(['fake'], 'avatar.png', { type: 'image/png' })
    fireEvent.change(fileInput(), { target: { files: [file] } })

    await waitFor(() => {
      expect(mockUpdateAvatar).toHaveBeenCalledWith(1, file)
      expect(onAvatarUpdated).toHaveBeenCalledTimes(1)
      expect(onClose).toHaveBeenCalledTimes(1)
    })
  })

  it('mostra o erro do upload sem fechar o modal', async () => {
    mockUpdateAvatar.mockRejectedValue(new Error('Arquivo muito grande'))
    render(<AvatarModal {...defaultProps} />)

    const file = new File(['fake'], 'avatar.png', { type: 'image/png' })
    fireEvent.change(fileInput(), { target: { files: [file] } })

    expect(await screen.findByText('Arquivo muito grande')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Trocar imagem de perfil' })).toBeInTheDocument()
  })
})
