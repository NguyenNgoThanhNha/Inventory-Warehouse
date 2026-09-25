import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/server';
import { API } from '@/test/handlers';
import { adminUser } from '@/test/fixtures';
import { loginAs, renderWithProviders } from '@/test/render';
import type { UserListItemDto } from '@/types';
import { UsersTab } from './users-tab';

const users: UserListItemDto[] = [
  { id: 'u-1', email: 'an.agent@inventory.local', fullName: 'An Agent', isActive: true, roles: [], createdDate: '2026-09-01T00:00:00Z' },
  { id: 'u-2', email: 'binh@inventory.local', fullName: 'Bình', isActive: false, roles: [], createdDate: '2026-09-01T00:00:00Z' },
];

function mockUsersApi() {
  const patches: { id: string; body: unknown }[] = [];
  const searches: (string | null)[] = [];
  server.use(
    http.get(`${API}/roles`, () => HttpResponse.json([])),
    http.get(`${API}/users`, ({ request }) => {
      searches.push(new URL(request.url).searchParams.get('search'));
      return HttpResponse.json({ items: users, totalCount: users.length, page: 1, pageSize: 20 });
    }),
    http.patch(`${API}/users/:id`, async ({ params, request }) => {
      const body = (await request.json()) as { isActive: boolean };
      patches.push({ id: String(params.id), body });
      const u = users.find((x) => x.id === params.id)!;
      return HttpResponse.json({ ...u, isActive: body.isActive });
    }),
  );
  return { patches, searches };
}

describe('UsersTab', () => {
  it('asks for confirmation before locking an account; unlocking needs none', async () => {
    loginAs(adminUser);
    const api = mockUsersApi();
    const user = userEvent.setup();
    renderWithProviders(<UsersTab />);

    await user.click(await screen.findByRole('switch', { name: 'Active an.agent@inventory.local' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Khóa tài khoản An Agent?')).toBeInTheDocument();
    expect(api.patches).toHaveLength(0);

    // cancel → nothing sent
    await user.click(within(dialog).getByRole('button', { name: 'Hủy' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(api.patches).toHaveLength(0);

    // confirm → PATCH isActive=false
    await user.click(screen.getByRole('switch', { name: 'Active an.agent@inventory.local' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Khóa' }));
    await waitFor(() => expect(api.patches).toEqual([{ id: 'u-1', body: { isActive: false } }]));

    // unlocking is applied directly
    await user.click(screen.getByRole('switch', { name: 'Active binh@inventory.local' }));
    await waitFor(() => expect(api.patches).toContainEqual({ id: 'u-2', body: { isActive: true } }));
  });

  it('debounces the user search', async () => {
    loginAs(adminUser);
    const api = mockUsersApi();
    const user = userEvent.setup();
    renderWithProviders(<UsersTab />);
    await screen.findByText('An Agent');

    await user.type(screen.getByRole('textbox', { name: 'Tìm user' }), 'binh');
    await waitFor(() => expect(api.searches).toContain('binh'));
    expect(api.searches.filter(Boolean)).toEqual(['binh']);
  });
});
