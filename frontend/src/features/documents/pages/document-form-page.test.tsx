import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { managerUser, staffUser, stockDocument } from '@/test/fixtures';
import { API } from '@/test/handlers';
import { loginAs, renderWithProviders } from '@/test/render';
import { server } from '@/test/server';
import { chooseSelectOption } from '@/test/ui';
import type { CreateDocumentRequest } from '@/types';
import { DocumentFormPage } from './document-form-page';

function renderIssueForm() {
  return renderWithProviders(<DocumentFormPage type="GoodsIssue" />, {
    path: '/goods-issues/new',
    route: '/goods-issues/new',
    extraRoutes: [{ path: '/goods-issues/:id', element: <div>DETAIL PAGE</div> }],
  });
}

async function pickProduct(user: UserEvent, line: number, search: string, optionText: RegExp) {
  await user.click(screen.getByRole('combobox', { name: `Sản phẩm dòng ${line}` }));
  await user.type(await screen.findByPlaceholderText('Gõ SKU hoặc tên...'), search);
  await user.click(await screen.findByRole('option', { name: optionText }));
}

async function fillHeader(user: UserEvent) {
  await chooseSelectOption(user, await screen.findByRole('combobox', { name: 'Kho' }), /KHO-A/);
  await chooseSelectOption(user, 'Lý do xuất', 'Bán hàng');
}

describe('DocumentFormPage (phiếu xuất)', () => {
  it('staff can only save a draft — the "Ghi sổ" button needs GOODS_ISSUE:U', async () => {
    loginAs(staffUser);
    renderIssueForm();
    expect(await screen.findByRole('button', { name: 'Lưu nháp' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ghi sổ' })).not.toBeInTheDocument();
  });

  it('shows current stock per line and blocks posting when a line exceeds it', async () => {
    loginAs(managerUser);
    const user = userEvent.setup();
    renderIssueForm();
    await fillHeader(user);
    await pickProduct(user, 1, 'P002', /P002/);

    const row = screen.getByRole('combobox', { name: 'Sản phẩm dòng 1' }).closest('tr')!;
    expect(await within(row).findByText('8')).toBeInTheDocument(); // "Tồn hiện"

    await user.type(screen.getByLabelText('Số lượng dòng 1'), '10');
    expect(await screen.findByText('Vượt tồn — chỉ còn 8')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ghi sổ' })).toBeDisabled();

    await user.clear(screen.getByLabelText('Số lượng dòng 1'));
    await user.type(screen.getByLabelText('Số lượng dòng 1'), '8');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Ghi sổ' })).toBeEnabled());
  });

  it('maps a 409 "không đủ hàng" from the server onto the line and retries with the same Idempotency-Key', async () => {
    loginAs(managerUser);
    const keys: (string | null)[] = [];
    let attempt = 0;
    server.use(
      http.post(`${API}/goods-issues`, async ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        const body = (await request.json()) as CreateDocumentRequest;
        if (++attempt === 1)
          return HttpResponse.json(
            {
              title: 'Không đủ hàng',
              status: 409,
              detail: 'Không đủ hàng: P001 tồn 5, cần 30.',
              shortages: [{ productId: 1, sku: 'P001', warehouseId: 1, available: 5, requested: 30 }],
            },
            { status: 409 },
          );
        expect(body).toMatchObject({ warehouseId: 1, reason: 'Sale', post: true, lines: [{ productId: 1, quantity: 5 }] });
        return HttpResponse.json(stockDocument({ id: 901, status: 'Posted' }), { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderIssueForm();
    await fillHeader(user);
    await pickProduct(user, 1, 'P001', /P001/);
    await user.type(screen.getByLabelText('Số lượng dòng 1'), '30');

    await user.click(screen.getByRole('button', { name: 'Ghi sổ' }));
    expect(await screen.findByText('Không đủ hàng — chỉ còn 5')).toBeInTheDocument();
    expect(screen.getByText(/Tồn kho vừa thay đổi/)).toBeInTheDocument();

    await user.clear(screen.getByLabelText('Số lượng dòng 1'));
    await user.type(screen.getByLabelText('Số lượng dòng 1'), '5');
    await user.click(screen.getByRole('button', { name: 'Ghi sổ' }));

    expect(await screen.findByText('DETAIL PAGE')).toBeInTheDocument();
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
  });

  it('validates required fields before calling the API', async () => {
    loginAs(managerUser);
    const user = userEvent.setup();
    renderIssueForm();
    await user.click(await screen.findByRole('button', { name: 'Lưu nháp' }));
    expect(await screen.findByText('Chọn kho')).toBeInTheDocument();
    expect(screen.getByText('Chọn lý do xuất')).toBeInTheDocument();
    expect(screen.getByText('Chọn sản phẩm')).toBeInTheDocument();
  });
});
