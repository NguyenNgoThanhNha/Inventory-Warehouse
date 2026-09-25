import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-client';
import type { ApiLogsQuery } from '@/types';
import { apiLogsApi } from '../api/api-logs-api';

export function useApiLogs(query: ApiLogsQuery) {
  return useQuery({
    queryKey: queryKeys.apiLogs(query),
    queryFn: () => apiLogsApi.list(query),
    placeholderData: keepPreviousData,
  });
}

export function useApiLog(id: number | undefined) {
  return useQuery({
    queryKey: queryKeys.apiLog(id ?? 0),
    queryFn: () => apiLogsApi.get(id!),
    enabled: !!id,
  });
}
