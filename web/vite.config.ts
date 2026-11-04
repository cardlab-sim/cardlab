import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// Dev: `npm run dev` proxies /api to the host started with `dotnet run --project src/CardLab.Api` (port 5080).
// Build: web/dist, copied into the host's wwwroot by `dotnet publish` (DEC-3).
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: { modules: { classNameStrategy: 'non-scoped' } },
  },
});
