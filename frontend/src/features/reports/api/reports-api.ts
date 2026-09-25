import { api, cleanParams } from '@/lib/api-client';
import type { DashboardDto, KardexDto, KardexQuery } from '@/types';

export const reportsApi = {
  dashboard: (warehouseId?: number) =>
    api.get<DashboardDto>('/reports/dashboard', { params: cleanParams({ warehouseId }) }).then((r) => r.data),
  kardex: (query: KardexQuery) => api.get<KardexDto>('/reports/kardex', { params: cleanParams(query) }).then((r) => r.data),
};
