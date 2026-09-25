import { api } from '@/lib/api-client';
import type { AuthResponse, CurrentUserDto, LoginRequest, RegisterRequest, ResetPasswordRequest } from '@/types';

export const authApi = {
  login: (body: LoginRequest) => api.post<AuthResponse>('/auth/login', body).then((r) => r.data),
  register: (body: RegisterRequest) => api.post<AuthResponse>('/auth/register', body).then((r) => r.data),
  logout: (refreshToken: string) => api.post<void>('/auth/logout', { refreshToken }).then(() => undefined),
  forgotPassword: (email: string) => api.post<void>('/auth/forgot-password', { email }).then(() => undefined),
  resetPassword: (body: ResetPasswordRequest) => api.post<void>('/auth/reset-password', body).then(() => undefined),
  me: () => api.get<CurrentUserDto>('/auth/me').then((r) => r.data),
};
