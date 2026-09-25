import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { managerUser } from '@/test/fixtures';
import { API } from '@/test/handlers';
import { loginAs, renderWithProviders } from '@/test/render';
import { server } from '@/test/server';
import type { DashboardDto, KardexDto } from '@/types';
import { DashboardPage } from './dashboard-page';
import { KardexPage } from './kardex-page';

const kardex: KardexDto = {
  productId: 1,
  sku: 'P001',
  productName: 'Ốc vít M6',
  unit: 'cái',
  warehouseId: 1,
  from: '2026-06-01',
  to: '2026-06-18',
  opening: 100,
  totalIn: 50,
  totalOut: 60,
  closing: 90,
  totalCount: 3,
  page: 1,
  pageSize: 50,
  rows: [
    { id: 1, occurredAt: '2026-06-05T02:00:00Z', documentId: 21, documentCode: 'PN-2026-00021', documentType: 'GoodsReceipt', movementType: 'In', warehouseId: 1, warehouseCode: 'KHO-A', inQty: 50, outQty: 0, balance: 150 },
    { id: 2, occurredAt: '2026-06-09T02:00:00Z', documentId: 440, documentCode: 'PX-2026-00440', documentType: 'GoodsIssue', movementType: 'Out', warehouseId: 1, warehouseCode: 'KHO-A', inQty: 0, outQty: 30, balance: 120 },
    { id: 3, occurredAt: '2026-06-18T02:00:00Z', documentId: 455, documentCode: 'PX-2026-00455', documentType: 'GoodsIssue', movementType: 'Out', warehouseId: 1, warehouseCode: 'KHO-A', inQty: 0, outQty: 30, balance: 90 },
  ],
};

const dashboard: DashboardDto = {
  generatedAt: '2026-09-25T08:00:00Z',
  stockValue: 2_400_000_000,
  productsInStock: 11980,
  lowStockCount: 37,
  draftDocuments: 2,
  postedToday: 12,
  slowMovingDays: 30,
  valueByWarehouse: [{ warehouseId: 1, code: 'KHO-A', name: 'Kho A — TP.HCM', value: 1_400_000_000, quantity: 5000 }],
  valueByGroup: [{ groupId: 1, name: 'Ốc vít & bu lông', value: 900_000_000 }],
  topLowStock: [{ productId: 2, sku: 'P002', name: 'Bản lề inox', warehouseId: 1, warehouseCode: 'KHO-A', quantity: 10, minThreshold: 30 }],
  inOutByDay: [{ date: '2026-09-25', inValue: 1000, outValue: 500 }],
  slowMoving: [{ productId: 5, sku: 'P005', name: 'Đai ốc M8', quantity: 400, value: 120_000, lastOutAt: null }],
};

describe('KardexPage', () => {
  it('asks for a product first', async () => {
    loginAs(managerUser);
    renderWithProviders(<KardexPage />, { path: '/kardex', route: '/kardex' });
    expect(await screen.findByText('Chọn sản phẩm để xem thẻ kho')).toBeInTheDocument();
  });

  it('shows the opening balance, each document with its running balance, and links to the documents', async () => {
    loginAs(managerUser);
    let requested: URLSearchParams | undefined;
    server.use(
      http.get(`${API}/reports/kardex`, ({ request }) => {
        requested = new URL(request.url).searchParams;
        return HttpResponse.json(kardex);
      }),
    );
    renderWithProviders(<KardexPage />, { path: '/kardex', route: '/kardex?productId=1&warehouseId=1&from=2026-06-01&to=2026-06-18' });

    const table = await screen.findByRole('table', { name: 'Thẻ kho' });
    const rows = within(table).getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Tồn đầu kỳ');
    expect(rows[1]).toHaveTextContent('100');
    expect(rows[2]).toHaveTextContent('+50');
    expect(rows[2]).toHaveTextContent('150');
    expect(rows[4]).toHaveTextContent('−30');
    expect(rows[4]).toHaveTextContent('90');
    expect(within(table).getByRole('link', { name: 'PX-2026-00455' })).toHaveAttribute('href', '/goods-issues/455');
    expect(screen.getByRole('combobox', { name: 'Sản phẩm' })).toHaveTextContent('P001 — Ốc vít M6');
    expect(Object.fromEntries(requested!)).toMatchObject({ productId: '1', warehouseId: '1', from: '2026-06-01', to: '2026-06-18' });
  });
});

describe('DashboardPage', () => {
  it('renders KPIs, low-stock and slow-moving lists from one API call', async () => {
    loginAs(managerUser);
    server.use(http.get(`${API}/reports/dashboard`, () => HttpResponse.json(dashboard)));
    renderWithProviders(<DashboardPage />, { path: '/dashboard', route: '/dashboard' });

    expect(await screen.findByText('2,4 tỷ')).toBeInTheDocument();
    expect(screen.getByText('37').closest('a')).toHaveAttribute('href', '/stock?belowThreshold=true');
    expect(screen.getByRole('link', { name: /Bản lề inox/ })).toHaveAttribute('href', '/kardex?productId=2&warehouseId=1');
    expect(screen.getByText(/chưa từng xuất/)).toBeInTheDocument();
    expect(screen.getByText(/Số liệu lúc/)).toBeInTheDocument();
  });
});
