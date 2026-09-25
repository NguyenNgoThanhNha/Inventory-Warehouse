import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { showError, getStatus } from '@/lib/api-errors';
import { queryKeys } from '@/lib/query-client';
import { useCurrentUser } from '@/stores/auth-store';
import type { ActivityPermissionInput, UsersQuery } from '@/types';
import { usersApi } from '../api/users-api';

export function useUsers(query: UsersQuery) {
  return useQuery({
    queryKey: queryKeys.userList(query),
    queryFn: () => usersApi.list(query),
    placeholderData: keepPreviousData,
  });
}

export function useUserPermissions(userId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.userPermissions(userId ?? ''),
    queryFn: () => usersApi.getPermissions(userId!),
    enabled: !!userId,
    staleTime: 0,
  });
}

/** After changing the current user's own roles / permissions, refresh /auth/me so the UI updates. */
function useRefreshMeIfSelf() {
  const queryClient = useQueryClient();
  const me = useCurrentUser();
  return (userId: string) => {
    if (userId === me?.id) void queryClient.invalidateQueries({ queryKey: queryKeys.me });
  };
}

export function useToggleUserActive() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => usersApi.update(id, { isActive }),
    onSuccess: (u) => {
      toast.success(`${u.isActive ? 'Đã mở khóa' : 'Đã khóa'} ${u.fullName}`);
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
    },
  });
}

export function useSetUserRoles() {
  const queryClient = useQueryClient();
  const refreshMe = useRefreshMeIfSelf();
  return useMutation({
    mutationFn: ({ id, roleIds }: { id: string; roleIds: string[] }) => usersApi.setRoles(id, roleIds),
    meta: { suppressGlobalError: true },
    onSuccess: (u) => {
      toast.success(`Đã cập nhật role cho ${u.fullName}`);
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
      void queryClient.invalidateQueries({ queryKey: queryKeys.roles });
      refreshMe(u.id);
    },
    onError: (err) => showError(err, getStatus(err) === 409 ? 'Không thể tự gỡ role Admin của chính mình' : undefined),
  });
}

export function useSetUserPermissions() {
  const queryClient = useQueryClient();
  const refreshMe = useRefreshMeIfSelf();
  return useMutation({
    mutationFn: ({ id, activities }: { id: string; activities: ActivityPermissionInput[] }) =>
      usersApi.setPermissions(id, activities),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.userPermissions(data.userId), data);
      toast.success('Đã lưu quyền riêng');
      refreshMe(data.userId);
    },
  });
}
