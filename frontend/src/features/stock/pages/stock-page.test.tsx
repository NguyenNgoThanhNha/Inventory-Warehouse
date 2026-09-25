import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { managerUser, staffUser } from '@/test/fixtures';
import { API } from '@/test/handlers';
import { loginAs, renderWithProviders } from '@/test/render';
import { server } from '@/test/server';
import type { StockRowDto } from '@/types';
import { StockPage } from './stock-page';

// jsdom has no layout: give the scroll container a height so the virtualizer renders rows.
const spies: { mockRestore: () => void }[] = [];
beforeEach(() => {
  spies.push(
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(600),
    vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(1200),
  );
});
// restore only these spies: vi.restoreAllMocks() would also reset the global matchMedia mock from setup.ts
afterEach(() => spies.splice(0).forEach((s) => s.mockRestore()));

const makeRows = (count: number, offset = 0): StockRowDto[] =>
  Array.from({ length: count }, (_, i) => ({
    productId: offset + i + 1,
    sku: `SKU${String(offset + i + 1).padStart(5, '0')}`,
    name: `Sản phẩm ${offset + i + 1}`,
    unit: 'cái',
    groupName: 'Nhóm',
    total: 10,
    isLow: false,
    warehouses: [{ warehouseId: 1, quantity: 10, minThreshold: 0, isLow: false }],
  }));

describe('StockPage', () => {
  it('renders one column per warehouse and marks low-stock rows', async () => {
    loginAs(staffUser);
    renderWithProviders(<StockPage />, { path: '/stock', route: '/stock' });

    const table = await screen.findByRole('table', { name: 'Tồn kho' });
    expect(await within(table).findByRole('columnheader', { name: 'KHO-A' })).toBeInTheDocument();
    expect(within(table).getByRole('columnheader', { name: 'KHO-B' })).toBeInTheDocument();
    const low = (await within(table).findByText('P002')).closest<HTMLElement>('[role="row"]')!;
    expect(low).toHaveAttribute('data-low', 'true');
    expect(within(low).getByText('LOW')).toBeInTheDocument();
    // staff has no WAREHOUSE:U → quantities are not editable
    expect(within(table).queryByRole('button', { name: /Đặt ngưỡng/ })).not.toBeInTheDocument();
  });

  it('"Chỉ hàng sắp hết" filters on the server and is kept in the URL', async () => {
    loginAs(staffUser);
    const user = userEvent.setup();
    renderWithProviders(<StockPage />, { path: '/stock', route: '/stock' });
    await screen.findByText('P001');

    await user.click(screen.getByLabelText('Chỉ hàng sắp hết'));

    expect(await screen.findByTestId('location')).toHaveTextContent('/stock?belowThreshold=true');
    await screen.findByText('Đã tải 1 / 1 sản phẩm');
    expect(screen.queryByText('P001')).not.toBeInTheDocument();
  });

  it('virtualizes: renders only a window of a large result and loads the next page on scroll', async () => {
    loginAs(staffUser);
    const total = 12_000;
    const pagesRequested: number[] = [];
    server.use(
      http.get(`${API}/stock`, ({ request }) => {
        const page = Number(new URL(request.url).searchParams.get('page'));
        pagesRequested.push(page);
        return HttpResponse.json({ items: makeRows(100, (page - 1) * 100), totalCount: total, page, pageSize: 100 });
      }),
    );
    renderWithProviders(<StockPage />, { path: '/stock', route: '/stock' });

    const table = await screen.findByRole('table', { name: 'Tồn kho' });
    await within(table).findByText('SKU00001');
    const rendered = within(table).getAllByRole('row').length - 1; // minus header
    expect(rendered).toBeGreaterThan(10);
    expect(rendered).toBeLessThan(60); // 100 loaded, only the visible window (+ overscan) is in the DOM
    expect(screen.getByText('Đã tải 100 / 12.000 sản phẩm')).toBeInTheDocument();

    // scroll near the end of the loaded rows → next page
    const scroller = table as HTMLDivElement;
    scroller.scrollTop = 90 * 40;
    scroller.dispatchEvent(new Event('scroll'));
    expect(await screen.findByText('Đã tải 200 / 12.000 sản phẩm')).toBeInTheDocument();
    expect(pagesRequested).toEqual([1, 2]);
  });

  it('manager edits a threshold from a quantity cell', async () => {
    loginAs(managerUser);
    let saved: unknown;
    server.use(
      http.put(`${API}/stock/threshold`, async ({ request }) => {
        saved = await request.json();
        return HttpResponse.json({});
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<StockPage />, { path: '/stock', route: '/stock' });

    await user.click(await screen.findByRole('button', { name: /P002 tại KHO-A: 8, dưới ngưỡng/ }));
    const input = await screen.findByLabelText('Ngưỡng (0 = không cảnh báo)');
    await user.clear(input);
    await user.type(input, '25');
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    await screen.findByText(/Đã đặt ngưỡng 25 cho P002/);
    expect(saved).toEqual({ productId: 2, warehouseId: 1, minThreshold: 25 });
  });
});
