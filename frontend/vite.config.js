import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': 'http://localhost:8090',
      '/hubs': { target: 'http://localhost:8090', ws: true }
    }
  }
})
