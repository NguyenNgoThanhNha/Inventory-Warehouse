import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { useAuthStore } from '@/stores/auth-store';
import { LoginPage } from './login-page';

function renderLogin() {
  return renderWithProviders(<LoginPage />, {
    path: '/login',
    route: '/login',
    extraRoutes: [{ path: '/', element: <div>HOME PAGE</div> }],
  });
}

describe('LoginPage', () => {
  it('shows validation errors when submitting an empty / invalid form', async () => {
    const user = userEvent.setup();
    renderLogin();

    await user.click(screen.getByRole('button', { name: /đăng nhập/i }));
    expect(await screen.findByText('Vui lòng nhập email')).toBeInTheDocument();
    expect(screen.getByText('Vui lòng nhập mật khẩu')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');

    await user.type(screen.getByLabelText('Email'), 'not-an-email');
    await user.click(screen.getByRole('button', { name: /đăng nhập/i }));
    expect(await screen.findByText('Email không hợp lệ')).toBeInTheDocument();
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it('shows an error for wrong credentials', async () => {
    const user = userEvent.setup();
    renderLogin();
    await user.type(screen.getByLabelText('Email'), 'staff@inventory.local');
    await user.type(screen.getByLabelText('Mật khẩu'), 'Wrong@123');
    await user.click(screen.getByRole('button', { name: /đăng nhập/i }));
    expect(await screen.findByText('Email hoặc mật khẩu không đúng')).toBeInTheDocument();
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it('stores tokens and navigates home after a successful login', async () => {
    const user = userEvent.setup();
    renderLogin();
    await user.type(screen.getByLabelText('Email'), 'staff@inventory.local');
    await user.type(screen.getByLabelText('Mật khẩu'), 'Staff@123');
    await user.click(screen.getByRole('button', { name: /đăng nhập/i }));

    expect(await screen.findByText('HOME PAGE')).toBeInTheDocument();
    await waitFor(() => expect(useAuthStore.getState().accessToken).toBe('access-1'));
    const state = useAuthStore.getState();
    expect(state.refreshToken).toBe('refresh-1');
    expect(state.user?.roles).toEqual(['Staff']);
    expect(state.can('GOODS_ISSUE', 'C')).toBe(true);
    expect(state.can('GOODS_ISSUE', 'U')).toBe(false);
    expect(JSON.parse(localStorage.getItem('inventory-auth') ?? '{}').state.accessToken).toBe('access-1');
  });
});
