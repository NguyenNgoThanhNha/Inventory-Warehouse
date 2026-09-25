import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from '@/lib/query-client';
import type { RoleRequest } from '@/types';
import { activitiesApi, rolesApi } from '../api/roles-api';

export function useRoles(enabled = true) {
  return useQuery({ queryKey: queryKeys.roles, queryFn: rolesApi.list, enabled, staleTime: 60_000 });
}

export function useRole(id: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.role(id ?? ''),
    queryFn: () => rolesApi.get(id!),
    enabled: enabled && !!id,
    staleTime: 0,
  });
}

export function useActivities(enabled = true) {
  return useQuery({ queryKey: queryKeys.activities, queryFn: activitiesApi.list, enabled, staleTime: 5 * 60_000 });
}

/** Create (id undefined) or update a role. Errors are handled by the dialog. */
export function useSaveRole(id: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RoleRequest) => (id ? rolesApi.update(id, body) : rolesApi.create(body)),
    meta: { suppressGlobalError: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.roles });
      // permissions of users having this role may have changed (including me)
      void queryClient.invalidateQueries({ queryKey: queryKeys.me });
    },
  });
}

export function useDeleteRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => rolesApi.remove(id),
    onSuccess: () => {
      toast.success('Đã xóa role');
      void queryClient.invalidateQueries({ queryKey: queryKeys.roles });
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
    },
  });
}
