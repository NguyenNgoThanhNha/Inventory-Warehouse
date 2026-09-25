import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from '@/stores/auth-store';
import type { AuthResponse } from '@/types';

export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '/api/v1';

type RetriableConfig = InternalAxiosRequestConfig & { _retry?: boolean };

/** Endpoints for which a 401 means "bad credentials", not "expired token". */
const AUTH_PATHS_WITHOUT_REFRESH = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/auth/logout',
  '/auth/forgot-password',
  '/auth/reset-password',
];

function isAuthPath(url: string | undefined): boolean {
  if (!url) return false;
  return AUTH_PATHS_WITHOUT_REFRESH.some((p) => url.includes(p));
}

/**
 * Creates the API client with:
 *  - request interceptor attaching `Authorization: Bearer <accessToken>`
 *  - response interceptor that on 401 calls POST /auth/refresh ONCE (single-flight;
 *    concurrent 401s wait on the same promise), retries the original request,
 *    and logs out when refresh fails.
 */
export function createApiClient(baseURL: string = API_BASE_URL): AxiosInstance {
  const instance = axios.create({ baseURL });
  // Separate instance without interceptors, so refresh can't recurse.
  const refreshClient = axios.create({ baseURL });

  let refreshPromise: Promise<string> | null = null;

  const refreshAccessToken = (): Promise<string> => {
    if (!refreshPromise) {
      const { refreshToken } = useAuthStore.getState();
      refreshPromise = (async () => {
        if (!refreshToken) throw new Error('No refresh token');
        const { data } = await refreshClient.post<AuthResponse>('/auth/refresh', { refreshToken });
        useAuthStore.getState().setSession(data);
        return data.accessToken;
      })().finally(() => {
        refreshPromise = null;
      });
    }
    return refreshPromise;
  };

  instance.interceptors.request.use((config) => {
    const token = useAuthStore.getState().accessToken;
    if (token && !config.headers.Authorization) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  });

  instance.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      const original = error.config as RetriableConfig | undefined;
      const status = error.response?.status;

      if (status !== 401 || !original || original._retry || isAuthPath(original.url)) {
        return Promise.reject(error);
      }

      if (!useAuthStore.getState().refreshToken) {
        useAuthStore.getState().logout();
        return Promise.reject(error);
      }

      original._retry = true;
      try {
        const newToken = await refreshAccessToken();
        original.headers.Authorization = `Bearer ${newToken}`;
        return instance(original);
      } catch (refreshError) {
        useAuthStore.getState().logout();
        return Promise.reject(refreshError instanceof AxiosError ? error : refreshError);
      }
    },
  );

  return instance;
}

export const api = createApiClient();

/** Drops undefined / null / empty-string values so they are not sent as query params. */
export function cleanParams<T extends object>(params: T): Partial<T> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  ) as Partial<T>;
}
