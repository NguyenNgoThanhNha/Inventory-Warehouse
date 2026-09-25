import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-client';
import { useAuthStore } from '@/stores/auth-store';
import { authApi } from '../api/auth-api';

export function useLogin() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationFn: authApi.login,
    meta: { suppressGlobalError: true },
    onSuccess: (data) => setSession(data),
  });
}

export function useRegister() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationFn: authApi.register,
    meta: { suppressGlobalError: true },
    onSuccess: (data) => setSession(data),
  });
}

export function useForgotPassword() {
  return useMutation({ mutationFn: authApi.forgotPassword });
}

export function useResetPassword() {
  return useMutation({ mutationFn: authApi.resetPassword, meta: { suppressGlobalError: true } });
}

/** Revokes the refresh token (best effort), clears the session and the query cache. */
export function useLogout() {
  const queryClient = useQueryClient();
  return async () => {
    const { refreshToken, logout } = useAuthStore.getState();
    if (refreshToken) {
      try {
        await authApi.logout(refreshToken);
      } catch {
        // ignore — we log out locally anyway
      }
    }
    logout();
    queryClient.clear();
  };
}

/** Refreshes the current user's roles / permissions once per app load (an admin may have changed them). */
export function useSyncCurrentUser() {
  const setUser = useAuthStore((s) => s.setUser);
  const me = useQuery({
    queryKey: queryKeys.me,
    queryFn: authApi.me,
    staleTime: 5 * 60_000,
    meta: { suppressGlobalError: true },
  });
  useEffect(() => {
    if (me.data) setUser(me.data);
  }, [me.data, setUser]);
}
