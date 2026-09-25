import { http, HttpResponse } from 'msw';
import type { ApiLogListItemDto, CreateDocumentRequest, PagedResult, ProductDto, StockRowDto } from '@/types';
import {
  activities,
  apiLogDetail,
  apiLogItems,
  authResponse,
  availableAtA,
  productGroups,
  products,
  staffUser,
  stockDocument,
  stockRows,
  warehouses,
} from './fixtures';

export const API = '/api/v1';

const paged = <T,>(items: T[], url: URL): PagedResult<T> => ({
  items,
  totalCount: items.length,
  page: Number(url.searchParams.get('page') ?? 1),
  pageSize: Number(url.searchParams.get('pageSize') ?? 20),
});

/** Default happy-path handlers; individual tests override with server.use(...). */
export const handlers = [
  http.post(`${API}/auth/login`, async ({ request }) => {
    const body = (await request.json()) as { email: string; password: string };
    if (body.password !== 'Staff@123') {
      return HttpResponse.json(
        { title: 'Unauthorized', status: 401, detail: 'Invalid credentials' },
        { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    return HttpResponse.json(authResponse({ ...staffUser, email: body.email }));
  }),
  http.post(`${API}/auth/refresh`, () => HttpResponse.json(authResponse(staffUser, '2'))),
  http.post(`${API}/auth/logout`, () => new HttpResponse(null, { status: 204 })),
  http.get(`${API}/auth/me`, () => HttpResponse.json(staffUser)),
  http.get(`${API}/activities`, () => HttpResponse.json(activities)),

  http.get(`${API}/warehouses`, () => HttpResponse.json(warehouses)),
  http.get(`${API}/product-groups`, () => HttpResponse.json(productGroups)),
  http.get(`${API}/products`, ({ request }) => {
    const url = new URL(request.url);
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const items: ProductDto[] = products.filter((p) => !search || p.sku.toLowerCase().startsWith(search) || p.name.toLowerCase().includes(search));
    return HttpResponse.json(paged(items, url));
  }),
  http.get(`${API}/suppliers`, ({ request }) =>
    HttpResponse.json(paged([{ id: 1, name: 'Kim khí Thành Phát', phone: null, email: null, address: null }], new URL(request.url))),
  ),

  http.get(`${API}/stock/available`, ({ request }) => {
    const url = new URL(request.url);
    const ids = url.searchParams.getAll('productIds').map(Number);
    return HttpResponse.json(ids.map((productId) => ({ productId, quantity: availableAtA[productId] ?? 0 })));
  }),
  http.get(`${API}/stock`, ({ request }) => {
    const url = new URL(request.url);
    const low = url.searchParams.get('belowThreshold') === 'true';
    const items: StockRowDto[] = low ? stockRows.filter((r) => r.isLow) : stockRows;
    return HttpResponse.json(paged(items, url));
  }),

  http.post(`${API}/goods-issues`, async ({ request }) => {
    const body = (await request.json()) as CreateDocumentRequest;
    return HttpResponse.json(stockDocument({ id: 900, status: body.post ? 'Posted' : 'Draft' }), { status: 201 });
  }),
  http.get(`${API}/goods-issues/:id`, ({ params }) => HttpResponse.json(stockDocument({ id: Number(params.id) }))),

  http.get(`${API}/notifications/unread-count`, () => HttpResponse.json({ count: 3 })),
  http.get(`${API}/notifications`, () => HttpResponse.json([])),

  http.get(`${API}/api-logs`, ({ request }) => {
    const url = new URL(request.url);
    const statusCode = url.searchParams.get('statusCode');
    const method = url.searchParams.get('method');
    const items: ApiLogListItemDto[] = apiLogItems.filter(
      (l) => (!statusCode || l.statusCode === Number(statusCode)) && (!method || l.method === method.toUpperCase()),
    );
    return HttpResponse.json(paged(items, url));
  }),
  http.get(`${API}/api-logs/:id`, ({ params }) => HttpResponse.json(apiLogDetail(Number(params.id)))),
];
