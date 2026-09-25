import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { AppProviders } from '@/app/providers';
import { createQueryClient } from '@/lib/query-client';
import { useAuthStore } from '@/stores/auth-store';
import type { CurrentUserDto } from '@/types';
import { authResponse } from './fixtures';

export function loginAs(user: CurrentUserDto) {
  useAuthStore.getState().setSession(authResponse(user));
}

/** Renders the current location so tests can assert navigation / search params. */
export function LocationDisplay() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname + location.search}</div>;
}

interface Options {
  /** route pattern the UI is mounted at */
  path?: string;
  /** initial URL */
  route?: string;
  /** additional routes (e.g. navigation targets) */
  extraRoutes?: { path: string; element: ReactElement }[];
}

/**
 * Renders UI inside the app providers and a MemoryRouter.
 * (A non-data router is used on purpose: data routers build `Request` objects whose AbortSignal
 * is incompatible between jsdom and Node's fetch, which silently breaks navigation in tests.)
 */
export function renderWithProviders(ui: ReactElement, { path = '/', route = '/', extraRoutes = [] }: Options = {}) {
  const queryClient = createQueryClient({ retry: false });
  const result = render(
    <AppProviders queryClient={queryClient}>
      <MemoryRouter initialEntries={[route]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <Routes>
          <Route path={path} element={ui} />
          {extraRoutes.map((r) => (
            <Route key={r.path} path={r.path} element={r.element} />
          ))}
        </Routes>
        <LocationDisplay />
      </MemoryRouter>
    </AppProviders>,
  );
  return { ...result, queryClient };
}
