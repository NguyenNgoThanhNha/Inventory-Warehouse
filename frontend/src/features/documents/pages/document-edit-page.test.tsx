import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { managerUser, staffUser, stockDocument } from '@/test/fixtures';
import { API } from '@/test/handlers';
import { loginAs, renderWithProviders } from '@/test/render';
import { server } from '@/test/server';
import type { UpdateDocumentRequest } from '@/types';
import { DocumentDetailPage } from './document-detail-page';
import { DocumentEditPage } from './document-edit-page';

const renderEdit = () =>
  renderWithProviders(<DocumentEditPage type="GoodsIssue" />, {
    path: '/goods-issues/:id/edit',
    route: '/goods-issues/455/edit',
    extraRoutes: [{ path: '/goods-issues/:id', element: <div>DETAIL PAGE</div> }],
  });

describe('Sửa phiếu nháp', () => {
  it('prefills the form from the draft and PUTs the changes with its rowVersion', async () => {
    loginAs(staffUser);
    let sent: UpdateDocumentRequest | undefined;
    server.use(
      http.put(`${API}/goods-issues/455`, async ({ request }) => {
        sent = (await request.json()) as UpdateDocumentRequest;
        return HttpResponse.json(stockDocument({ note: 'đã sửa' }));
      }),
    );
    const user = userEvent.setup();
    renderEdit();

    expect(await screen.findByText('Sửa phiếu xuất PX-2026-00455')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Sản phẩm dòng 1' })).toHaveTextContent('P001 — Ốc vít M6');
    const qty = screen.getByLabelText('Số lượng dòng 1');
    expect(qty).toHaveValue(30);
    expect(screen.queryByRole('button', { name: 'Ghi sổ' })).not.toBeInTheDocument();

    await user.clear(qty);
    await user.type(qty, '12');
    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));

    expect(await screen.findByText('DETAIL PAGE')).toBeInTheDocument();
    expect(sent).toMatchObject({
      rowVersion: 'AAAAAAAAB9E=',
      warehouseId: 1,
      reason: 'Sale',
      lines: [{ productId: 1, quantity: 12 }],
    });
  });

  it('refuses to edit a posted document', async () => {
    loginAs(managerUser);
    server.use(http.get(`${API}/goods-issues/:id`, () => HttpResponse.json(stockDocument({ status: 'Posted' }))));
    renderEdit();
    expect(await screen.findByText(/đã ghi sổ — không sửa được/)).toBeInTheDocument();
  });

  it('shows "Sửa" on the detail page only to the owner or an approver', async () => {
    const other = { ...staffUser, id: '00000000-0000-0000-0000-00000000a009' };
    loginAs(other);
    const { unmount } = renderWithProviders(<DocumentDetailPage type="GoodsIssue" />, { path: '/goods-issues/:id', route: '/goods-issues/455' });
    await screen.findByText('PX-2026-00455');
    expect(screen.queryByRole('link', { name: 'Sửa' })).not.toBeInTheDocument();
    unmount();

    loginAs(staffUser); // chủ phiếu
    renderWithProviders(<DocumentDetailPage type="GoodsIssue" />, { path: '/goods-issues/:id', route: '/goods-issues/455' });
    expect(await screen.findByRole('link', { name: 'Sửa' })).toHaveAttribute('href', '/goods-issues/455/edit');
  });
});
