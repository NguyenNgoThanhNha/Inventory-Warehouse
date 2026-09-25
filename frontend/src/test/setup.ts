import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { server } from './server';
import { useAuthStore } from '@/stores/auth-store';

configure({ asyncUtilTimeout: 5000 });

// ---- jsdom polyfills needed by Radix UI / cmdk / recharts ----
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver = ResizeObserverMock as unknown as typeof ResizeObserver;

// Radix Select / Popover use pointer capture and scrollIntoView, which jsdom does not implement
const proto = window.HTMLElement.prototype as HTMLElement & Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.setPointerCapture ??= () => {};
proto.releasePointerCapture ??= () => {};
proto.scrollIntoView ??= () => {};

if (!window.URL.createObjectURL) {
  window.URL.createObjectURL = vi.fn(() => 'blob:mock');
  window.URL.revokeObjectURL = vi.fn();
}

// ---- MSW ----
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
  useAuthStore.getState().logout();
  localStorage.clear();
});
afterAll(() => server.close());
