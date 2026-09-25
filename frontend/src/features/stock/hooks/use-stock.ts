import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-client';
import type { StockQuery, StockThresholdRequest } from '@/types';
import { stockApi } from '../api/stock-api';

/** Server page size for the stock table — the maximum the API allows. */
export const STOCK_PAGE_SIZE = 100;

/**
 * Stock rows loaded page by page as the user scrolls (server-side filter + paging);
 * the table virtualizes them so only visible rows are in the DOM.
 */
export function useInfiniteStock(query: Omit<StockQuery, 'pageSize'>) {
  return useInfiniteQuery({
    queryKey: queryKeys.stockList(query),
    queryFn: ({ pageParam }) => stockApi.list({ ...query, page: pageParam, pageSize: STOCK_PAGE_SIZE }),
    initialPageParam: 1,
    getNextPageParam: (last, pages) => {
      const loaded = pages.reduce((n, p) => n + p.items.length, 0);
      return loaded < last.totalCount && last.items.length > 0 ? pages.length + 1 : undefined;
    },
  });
}

/** Current quantities at a warehouse for the given products (document forms show "Tồn hiện" per line). */
export function useAvailableStock(warehouseId: number | undefined, productIds: number[]) {
  const ids = [...new Set(productIds)].sort((a, b) => a - b);
  return useQuery({
    queryKey: queryKeys.available(warehouseId ?? 0, ids),
    queryFn: () => stockApi.available(warehouseId!, ids),
    enabled: !!warehouseId && ids.length > 0,
    staleTime: 0,
    refetchOnWindowFocus: true,
    select: (rows) => new Map(rows.map((r) => [r.productId, r.quantity])),
  });
}

export function useSetThreshold() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: StockThresholdRequest) => stockApi.setThreshold(body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.stock }),
  });
}
