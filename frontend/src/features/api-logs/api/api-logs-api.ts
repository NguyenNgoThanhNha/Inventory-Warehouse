import { api, cleanParams } from '@/lib/api-client';
import type { ApiLogDetailDto, ApiLogListItemDto, ApiLogsQuery, PagedResult } from '@/types';

export const apiLogsApi = {
  list: (query: ApiLogsQuery) =>
    api.get<PagedResult<ApiLogListItemDto>>('/api-logs', { params: cleanParams(query) }).then((r) => r.data),
  get: (id: number) => api.get<ApiLogDetailDto>(`/api-logs/${id}`).then((r) => r.data),
};
