import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      // O front sempre chama /api/... — em dev, o Vite repassa para a API via porta
      // do Docker (nginx em :80, que já remove o prefixo /api). Se a API rodar
      // fora do Docker (dotnet run), aponte o target para http://localhost:5041
      // e adicione rewrite: (path) => path.replace(/^\/api/, '').
      '/api': {
        target: 'http://localhost',
        changeOrigin: true,
      },
    },
  },
})
