import { api } from '@/lib/api-client';
import type { ActivityDto, RoleDetailDto, RoleDto, RoleRequest } from '@/types';

export const rolesApi = {
  list: () => api.get<RoleDto[]>('/roles').then((r) => r.data),
  get: (id: string) => api.get<RoleDetailDto>(`/roles/${id}`).then((r) => r.data),
  create: (body: RoleRequest) => api.post<RoleDetailDto>('/roles', body).then((r) => r.data),
  update: (id: string, body: RoleRequest) => api.put<RoleDetailDto>(`/roles/${id}`, body).then((r) => r.data),
  remove: (id: string) => api.delete<void>(`/roles/${id}`).then(() => undefined),
};

export const activitiesApi = {
  list: () => api.get<ActivityDto[]>('/activities').then((r) => r.data),
};
