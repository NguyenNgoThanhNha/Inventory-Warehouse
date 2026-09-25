import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { ExportButton } from '@/components/common/export-button';
import { adminUser } from '@/test/fixtures';
import { API } from '@/test/handlers';
import { loginAs, renderWithProviders } from '@/test/render';
import { server } from '@/test/server';
import type { ImportProductsResultDto } from '@/types';
import { ImportProductsDialog } from './import-products-dialog';

const xlsx = () => new File(['fake'], 'san-pham.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

const result = (dryRun: boolean): ImportProductsResultDto => ({
  dryRun,
  totalRows: 1500,
  validRows: 1498,
  created: 1400,
  updated: 98,
  groupsCreated: ['Nhóm mới'],
  errors: [
    { row: 101, column: 'Tên sản phẩm', message: 'Thiếu tên sản phẩm.' },
    { row: 201, column: 'Giá vốn', message: 'Giá vốn phải là số ≥ 0.' },
  ],
});

describe('ImportProductsDialog', () => {
  it('checks the file first (dry run), lists bad rows, then imports only on confirm', async () => {
    loginAs(adminUser);
    const calls: string[] = [];
    server.use(
      http.post(`${API}/imports/products`, async ({ request }) => {
        const dryRun = new URL(request.url).searchParams.get('dryRun') === 'true';
        calls.push(dryRun ? 'dry' : 'import');
        return HttpResponse.json(result(dryRun));
      }),
    );
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    renderWithProviders(<ImportProductsDialog open onOpenChange={onOpenChange} />);

    await user.upload(screen.getByLabelText('Chọn file Excel'), xlsx());

    expect(await screen.findByText(/1\.400/)).toBeInTheDocument();
    const errors = screen.getByRole('region', { name: 'Lỗi từng dòng' });
    expect(within(errors).getByText('2 dòng lỗi sẽ bị bỏ qua')).toBeInTheDocument();
    expect(within(errors).getByText('101')).toBeInTheDocument();
    expect(calls).toEqual(['dry']);

    await user.click(screen.getByRole('button', { name: /Import 1\.498 dòng hợp lệ/ }));
    expect(await screen.findByText(/Đã import: 1400 thêm mới, 98 cập nhật, bỏ qua 2 dòng lỗi/)).toBeInTheDocument();
    expect(calls).toEqual(['dry', 'import']);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});

describe('ExportButton', () => {
  it('shows the ProblemDetails message even though the error body arrives as a Blob', async () => {
    loginAs(adminUser);
    server.use(
      http.get(`${API}/exports/stock`, () =>
        HttpResponse.json({ title: 'Dữ liệu không hợp lệ', status: 400, detail: 'Có 120.000 sản phẩm, tối đa 100.000 dòng.' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<ExportButton url="/exports/stock" fallbackName="ton-kho.xlsx" />);

    await user.click(screen.getByRole('button', { name: 'Xuất Excel' }));

    expect(await screen.findByText(/tối đa 100\.000 dòng/)).toBeInTheDocument();
  });
});
