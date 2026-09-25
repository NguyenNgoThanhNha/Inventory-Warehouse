import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { can, canAny, type PermissionRequirement } from '@/lib/permissions';
import type { ActivityAction, AuthResponse, CurrentUserDto } from '@/types';

export const AUTH_STORAGE_KEY = 'inventory-auth';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  user: CurrentUserDto | null;
  setSession: (auth: AuthResponse) => void;
  setUser: (user: CurrentUserDto) => void;
  logout: () => void;
  /** Permission check for the current user. */
  can: (code: string, action: ActivityAction) => boolean;
}

const empty = {
  accessToken: null,
  refreshToken: null,
  accessTokenExpiresAt: null,
  user: null,
};

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      ...empty,
      setSession: (auth) =>
        set({
          accessToken: auth.accessToken,
          refreshToken: auth.refreshToken,
          accessTokenExpiresAt: auth.accessTokenExpiresAt,
          user: auth.user,
        }),
      setUser: (user) => set({ user }),
      logout: () => set({ ...empty }),
      can: (code, action) => can(get().user, code, action),
    }),
    {
      name: AUTH_STORAGE_KEY,
      storage: createJSONStorage(() => localStorage),
      partialize: (s) => ({
        accessToken: s.accessToken,
        refreshToken: s.refreshToken,
        accessTokenExpiresAt: s.accessTokenExpiresAt,
        user: s.user,
      }),
    },
  ),
);

export const useIsAuthenticated = () => useAuthStore((s) => !!s.accessToken && !!s.user);

export const useCurrentUser = () => useAuthStore((s) => s.user);

/** Reactive permission check (re-renders when the user / permissions change). */
export const useCan = (code: string, action: ActivityAction) => useAuthStore((s) => can(s.user, code, action));

/** Reactive "any of" permission check. */
export const useCanAny = (requirements: readonly PermissionRequirement[]) =>
  useAuthStore((s) => canAny(s.user, requirements));
