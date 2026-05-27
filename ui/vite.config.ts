import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': path.resolve(__dirname, 'src') },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5017', changeOrigin: true, secure: false },
      '/hubs': { target: 'http://localhost:5017', changeOrigin: true, secure: false, ws: true },
      '/health': { target: 'http://localhost:5017', changeOrigin: true, secure: false },
      '/live': { target: 'http://localhost:5017', changeOrigin: true, secure: false },
      '/swagger': { target: 'http://localhost:5017', changeOrigin: true, secure: false },
    },
  },
  build: {
    outDir: path.resolve(__dirname, '../src/SignalNine.Web/wwwroot'),
    emptyOutDir: true,
    sourcemap: true,
  },
});
