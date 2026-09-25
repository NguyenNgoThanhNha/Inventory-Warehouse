import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { AppProviders } from '@/app/providers';
import { PermissionRoute } from '@/app/route-guards';
import { createQueryClient } from '@/lib/query-client';
import { PERMISSIONS } from '@/lib/permissions';
import { adminUser, managerUser, staffUser } from '@/test/fixtures';
import { API } from '@/test/handlers';
import { loginAs } from '@/test/render';
import { server } from '@/test/server';
import type { CurrentUserDto } from '@/types';
import { AppLayout } from './app-layout';

function renderShell(user: CurrentUserDto, route = '/stock') {
  loginAs(user);
  server.use(http.get(`${API}/auth/me`, () => HttpResponse.json(user)));
  return render(
    <AppProviders queryClient={createQueryClient({ retry: false })}>
      <MemoryRouter initialEntries={[route]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/stock" element={<div>STOCK PAGE</div>} />
            <Route path="/goods-issues" element={<div>ISSUES PAGE</div>} />
            <Route element={<PermissionRoute anyOf={PERMISSIONS.settings} />}>
              <Route path="/settings" element={<div>SETTINGS PAGE</div>} />
            </Route>
          </Route>
        </Routes>
      </MemoryRouter>
    </AppProviders>,
  );
}

const linkTexts = () => screen.getAllByRole('link').map((a) => a.textContent?.trim());

describe('AppLayout / permission gating', () => {
  it('staff: sees warehouse pages but not Phân quyền / API Logs; /settings shows 403', async () => {
    renderShell(staffUser, '/settings');
    expect(await screen.findByText('403 — Không có quyền')).toBeInTheDocument();
    expect(screen.queryByText('SETTINGS PAGE')).not.toBeInTheDocument();
    const links = linkTexts();
    expect(links).toEqual(expect.arrayContaining(['Tồn kho', 'Nhập kho', 'Xuất kho', 'Chuyển kho', 'Kiểm kê', 'Danh mục']));
    for (const hidden of ['Phân quyền', 'API Logs']) expect(links).not.toContain(hidden);
    // unread badge is polled from the API
    expect(await screen.findByTestId('unread-count')).toHaveTextContent('3');
  });

  it('admin sees everything including Phân quyền and API Logs', async () => {
    const { unmount } = renderShell(managerUser);
    await screen.findByText('STOCK PAGE');
    expect(linkTexts()).not.toContain('Phân quyền');
    unmount();

    renderShell(adminUser, '/settings');
    expect(await screen.findByText('SETTINGS PAGE')).toBeInTheDocument();
    expect(linkTexts()).toEqual(expect.arrayContaining(['Phân quyền', 'API Logs']));
  });

  it('global search jumps to the stock table filtered by the term', async () => {
    const user = userEvent.setup();
    renderShell(managerUser, '/goods-issues');
    await screen.findByText('ISSUES PAGE');
    await user.type(screen.getByRole('textbox', { name: 'Tìm hàng trong kho' }), 'P002{Enter}');
    expect(await screen.findByText('STOCK PAGE')).toBeInTheDocument();
  });

  it('on mobile the sidebar is an off-canvas sheet opened by the topbar trigger', async () => {
    const width = window.innerWidth;
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: 375 });
    try {
      const user = userEvent.setup();
      renderShell(managerUser);
      await screen.findByText('STOCK PAGE');
      // collapsed: no navigation links rendered until the sheet is opened
      expect(screen.queryByRole('link', { name: 'Xuất kho' })).not.toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: 'Thu gọn / mở menu' }));
      const sheet = await screen.findByRole('dialog');
      await user.click(within(sheet).getByRole('link', { name: 'Xuất kho' }));
      // navigating closes the sheet
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(await screen.findByText('ISSUES PAGE')).toBeInTheDocument();
    } finally {
      Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
    }
  });
});
