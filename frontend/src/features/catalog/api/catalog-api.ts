import { api, cleanParams } from '@/lib/api-client';
import type {
  PagedResult,
  ProductDto,
  ProductGroupDto,
  ProductRequest,
  ProductsQuery,
  SupplierDto,
  SupplierRequest,
  WarehouseDto,
  WarehouseRequest,
} from '@/types';

export const productsApi = {
  list: (query: ProductsQuery) =>
    api.get<PagedResult<ProductDto>>('/products', { params: cleanParams(query) }).then((r) => r.data),
  create: (body: ProductRequest) => api.post<ProductDto>('/products', body).then((r) => r.data),
  update: (id: number, body: ProductRequest) => api.put<ProductDto>(`/products/${id}`, body).then((r) => r.data),
  remove: (id: number) => api.delete(`/products/${id}`).then(() => undefined),
};

export const productGroupsApi = {
  list: () => api.get<ProductGroupDto[]>('/product-groups').then((r) => r.data),
  save: (id: number | null, name: string) =>
    (id ? api.put<ProductGroupDto>(`/product-groups/${id}`, { name }) : api.post<ProductGroupDto>('/product-groups', { name })).then(
      (r) => r.data,
    ),
};

export const warehousesApi = {
  list: (includeInactive = false) =>
    api.get<WarehouseDto[]>('/warehouses', { params: includeInactive ? { includeInactive } : {} }).then((r) => r.data),
  save: (id: number | null, body: WarehouseRequest) =>
    (id ? api.put<WarehouseDto>(`/warehouses/${id}`, body) : api.post<WarehouseDto>('/warehouses', body)).then((r) => r.data),
};

export const suppliersApi = {
  list: (query: { search?: string; page?: number; pageSize?: number }) =>
    api.get<PagedResult<SupplierDto>>('/suppliers', { params: cleanParams(query) }).then((r) => r.data),
  save: (id: number | null, body: SupplierRequest) =>
    (id ? api.put<SupplierDto>(`/suppliers/${id}`, body) : api.post<SupplierDto>('/suppliers', body)).then((r) => r.data),
};
