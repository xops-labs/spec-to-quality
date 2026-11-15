/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The production build is written into the backend's static-file location (wwwroot) so the API serves the SPA.
export default defineConfig({
  plugins: [react()],
  build: { outDir: '../backend/src/IncidentApp.Api/wwwroot', emptyOutDir: true },
  server: { port: 5173, proxy: { '/api': 'http://localhost:8080' } },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    coverage: { provider: 'v8', reporter: ['text', 'lcov'], include: ['src/**'], exclude: ['src/test/**', 'src/main.tsx'] },
  },
});
