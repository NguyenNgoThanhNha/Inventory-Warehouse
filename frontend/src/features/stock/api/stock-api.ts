import { api, cleanParams } from '@/lib/api-client';
import type { AvailableStockDto, PagedResult, StockQuery, StockRowDto, StockThresholdRequest } from '@/types';

export const stockApi = {
  list: (query: StockQuery & { page: number }) =>
    api.get<PagedResult<StockRowDto>>('/stock', { params: cleanParams(query) }).then((r) => r.data),
  available: (warehouseId: number, productIds: number[]) =>
    api
      .get<AvailableStockDto[]>('/stock/available', {
        params: { warehouseId, productIds },
        // ASP.NET binds arrays as repeated keys: productIds=1&productIds=2
        paramsSerializer: { indexes: null },
      })
      .then((r) => r.data),
  setThreshold: (body: StockThresholdRequest) => api.put('/stock/threshold', body).then((r) => r.data),
};
