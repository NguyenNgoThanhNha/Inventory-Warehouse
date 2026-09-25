import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-client';
import type { KardexQuery } from '@/types';
import { reportsApi } from '../api/reports-api';

/** The API caches the dashboard for 30 s; refetching more often than that only returns the same numbers. */
export const DASHBOARD_REFRESH_MS = 60_000;

export function useDashboard(warehouseId: number | undefined) {
  return useQuery({
    queryKey: queryKeys.dashboard(warehouseId),
    queryFn: () => reportsApi.dashboard(warehouseId),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
    refetchInterval: DASHBOARD_REFRESH_MS,
  });
}

export function useKardex(query: KardexQuery | undefined) {
  return useQuery({
    queryKey: queryKeys.kardex(query ?? {}),
    queryFn: () => reportsApi.kardex(query!),
    enabled: !!query,
    placeholderData: keepPreviousData,
  });
}
