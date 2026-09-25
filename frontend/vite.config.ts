/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

/** Backend for the dev / preview proxy (override with API_PROXY_TARGET=http://localhost:8090). */
const API_PROXY_TARGET = process.env.API_PROXY_TARGET ?? 'http://localhost:5090';

const proxy = {
  // trailing slash: must not swallow the SPA route /api-logs
  '/api/': { target: API_PROXY_TARGET, changeOrigin: true },
};

/**
 * Only the framework core gets a stable, long-cacheable vendor chunk. Everything else is left to
 * Rollup's natural splitting so heavy libraries used by lazy routes (react-day-picker +
 * date-fns) stay in those lazy chunks. (An object-form manualChunks used to pull recharts' shared
 * deps into a "charts" chunk that the entry imported, so 114 kB gz of charts loaded on every page.)
 */
const CORE_VENDOR = /[\\/]node_modules[\\/](react|react-dom|scheduler|react-router|react-router-dom|@remix-run[\\/]router)[\\/]/;
/** Libraries the entry needs anyway (login / stock table); split out only for long-term caching across deploys. */
const APP_VENDOR =
  /[\\/]node_modules[\\/](@tanstack[\\/](query-core|react-query|table-core|react-table)|axios|zod|react-hook-form|@hookform[\\/]resolvers|zustand|sonner)[\\/]/;

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: { port: 5174, proxy },
  preview: { port: 4174, proxy },
  build: {
    rollupOptions: {
      output: {
        manualChunks: (id) => (CORE_VENDOR.test(id) ? 'react' : APP_VENDOR.test(id) ? 'vendor' : undefined),
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    testTimeout: 30000,
    hookTimeout: 30000,
  },
});
