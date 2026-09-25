import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-client';
import { useCan } from '@/stores/auth-store';
import type { ProductRequest, ProductsQuery, SupplierRequest, WarehouseRequest } from '@/types';
import { productGroupsApi, productsApi, suppliersApi, warehousesApi } from '../api/catalog-api';

/** Catalog data changes rarely — keep it fresh for 5 minutes. */
const CATALOG_STALE_MS = 5 * 60_000;

export function useProducts(query: ProductsQuery, enabled = true) {
  return useQuery({
    queryKey: queryKeys.productList(query),
    queryFn: () => productsApi.list(query),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useProductGroups() {
  const canRead = useCan('PRODUCT', 'R');
  return useQuery({
    queryKey: queryKeys.productGroups,
    queryFn: productGroupsApi.list,
    staleTime: CATALOG_STALE_MS,
    enabled: canRead,
  });
}

export function useWarehouses(includeInactive = false) {
  const canRead = useCan('WAREHOUSE', 'R');
  return useQuery({
    queryKey: [...queryKeys.warehouses, { includeInactive }],
    queryFn: () => warehousesApi.list(includeInactive),
    staleTime: CATALOG_STALE_MS,
    enabled: canRead,
  });
}

export function useSuppliers(query: { search?: string; page?: number; pageSize?: number }, enabled = true) {
  return useQuery({
    queryKey: queryKeys.supplierList(query),
    queryFn: () => suppliersApi.list(query),
    placeholderData: keepPreviousData,
    staleTime: CATALOG_STALE_MS,
    enabled,
  });
}

export function useSaveProduct(id: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ProductRequest) => (id ? productsApi.update(id, body) : productsApi.create(body)),
    meta: { suppressGlobalError: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.products });
      void queryClient.invalidateQueries({ queryKey: queryKeys.stock });
    },
  });
}

export function useDeleteProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: productsApi.remove,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.products }),
  });
}

export function useSaveProductGroup(id: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => productGroupsApi.save(id, name),
    meta: { suppressGlobalError: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.productGroups });
      void queryClient.invalidateQueries({ queryKey: queryKeys.products });
    },
  });
}

export function useSaveWarehouse(id: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: WarehouseRequest) => warehousesApi.save(id, body),
    meta: { suppressGlobalError: true },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.warehouses }),
  });
}

export function useSaveSupplier(id: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SupplierRequest) => suppliersApi.save(id, body),
    meta: { suppressGlobalError: true },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.suppliers }),
  });
}
