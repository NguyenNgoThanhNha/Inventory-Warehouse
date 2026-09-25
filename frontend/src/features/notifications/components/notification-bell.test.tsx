import { act, renderHook, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { focusManager, QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '@/lib/query-client';
import { server } from '@/test/server';
import { API } from '@/test/handlers';
import { managerUser } from '@/test/fixtures';
import { loginAs, renderWithProviders } from '@/test/render';
import type { NotificationDto } from '@/types';
import { UNREAD_POLL_INTERVAL, useUnreadCount } from '../hooks/use-notifications';
import { NotificationBell } from './notification-bell';

const notifications: NotificationDto[] = [
  { id: 1, message: 'P002 dưới ngưỡng tồn tại Kho A', link: '/stock?belowThreshold=true', isRead: false, createdAt: '2026-09-20T10:00:00Z' },
  { id: 2, message: 'PX-2026-00455 chờ duyệt', link: '/goods-issues/455', isRead: false, createdAt: '2026-09-20T09:00:00Z' },
];

/** Server state: the count reflects the notifications' isRead flags; mark-read waits for `release()`. */
function mockNotificationsApi({ failMarkRead = false } = {}) {
  const state = notifications.map((n) => ({ ...n }));
  let release!: () => void;
  const gate = new Promise<void>((r) => (release = r));
  server.use(
    http.get(`${API}/notifications/unread-count`, () => HttpResponse.json({ count: state.filter((n) => !n.isRead).length })),
    http.get(`${API}/notifications`, () => HttpResponse.json(state)),
    http.post(`${API}/notifications/:id/read`, async ({ params }) => {
      await gate;
      if (failMarkRead) return HttpResponse.json({ title: 'Boom', status: 500 }, { status: 500 });
      const n = state.find((x) => x.id === Number(params.id));
      if (n) n.isRead = true;
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return { release: () => release() };
}

async function openBell(user: ReturnType<typeof userEvent.setup>) {
  renderWithProviders(<NotificationBell />);
  const bell = await screen.findByRole('button', { name: 'Thông báo (2 chưa đọc)' });
  await user.click(bell);
  return screen.findByRole('dialog');
}

describe('NotificationBell', () => {
  it('marks a notification read optimistically (before the server answers)', async () => {
    loginAs(managerUser);
    const api = mockNotificationsApi();
    const user = userEvent.setup();
    const panel = await openBell(user);

    const markButtons = await within(panel).findAllByRole('button', { name: 'Đã đọc' });
    expect(markButtons).toHaveLength(2);
    await user.click(markButtons[0]);

    // the request is still pending, yet the badge and the item are already updated
    expect(await screen.findByTestId('unread-count')).toHaveTextContent('1');
    expect(within(panel).getAllByRole('button', { name: 'Đã đọc' })).toHaveLength(1);

    api.release();
    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('1'));
  });

  it('rolls the optimistic update back when the request fails', async () => {
    loginAs(managerUser);
    const api = mockNotificationsApi({ failMarkRead: true });
    const user = userEvent.setup();
    const panel = await openBell(user);

    await user.click((await within(panel).findAllByRole('button', { name: 'Đã đọc' }))[0]);
    expect(await screen.findByTestId('unread-count')).toHaveTextContent('1');

    api.release();
    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('2'));
    expect(within(panel).getAllByRole('button', { name: 'Đã đọc' })).toHaveLength(2);
  });
});

/** waitFor() polls with setInterval, which this test fakes; poll with real timeouts instead. */
async function until(check: () => boolean, timeout = 3000) {
  const start = Date.now();
  while (!check()) {
    if (Date.now() - start > timeout) throw new Error('condition not met in time');
    await act(() => new Promise((r) => setTimeout(r, 10)));
  }
}

describe('useUnreadCount polling', () => {
  afterEach(() => {
    vi.useRealTimers();
    act(() => focusManager.setFocused(undefined));
  });

  it('polls every 30s while visible, pauses while the tab is hidden and refreshes on return', async () => {
    loginAs(managerUser);
    let calls = 0;
    server.use(
      http.get(`${API}/notifications/unread-count`, async () => {
        calls++;
        await delay(0);
        return HttpResponse.json({ count: calls });
      }),
    );
    // only intervals are faked: MSW / waitFor keep using real timeouts
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    const client = createQueryClient({ retry: false });
    const { result } = renderHook(() => useUnreadCount(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });
    await until(() => result.current.data === 1);

    act(() => vi.advanceTimersByTime(UNREAD_POLL_INTERVAL));
    await until(() => calls === 2);

    // tab hidden: no requests however long it stays hidden
    act(() => focusManager.setFocused(false));
    act(() => vi.advanceTimersByTime(UNREAD_POLL_INTERVAL * 5));
    await act(() => new Promise((r) => setTimeout(r, 100)));
    expect(calls).toBe(2);

    // back to the tab: immediate refresh, then polling resumes
    act(() => focusManager.setFocused(true));
    await until(() => calls === 3);
    act(() => vi.advanceTimersByTime(UNREAD_POLL_INTERVAL));
    await until(() => calls === 4);
  });
});
