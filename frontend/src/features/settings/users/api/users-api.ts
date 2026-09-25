import { api, cleanParams } from '@/lib/api-client';
import type {
  ActivityPermissionInput,
  PagedResult,
  UpdateUserRequest,
  UserListItemDto,
  UserPermissionDetailDto,
  UsersQuery,
} from '@/types';

export const usersApi = {
  list: (query: UsersQuery) =>
    api.get<PagedResult<UserListItemDto>>('/users', { params: cleanParams(query) }).then((r) => r.data),
  update: (id: string, body: UpdateUserRequest) => api.patch<UserListItemDto>(`/users/${id}`, body).then((r) => r.data),
  setRoles: (id: string, roleIds: string[]) =>
    api.put<UserListItemDto>(`/users/${id}/roles`, { roleIds }).then((r) => r.data),
  getPermissions: (id: string) => api.get<UserPermissionDetailDto>(`/users/${id}/permissions`).then((r) => r.data),
  setPermissions: (id: string, activities: ActivityPermissionInput[]) =>
    api.put<UserPermissionDetailDto>(`/users/${id}/permissions`, { activities }).then((r) => r.data),
};
