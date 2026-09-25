import { http, HttpResponse } from 'msw';
import { server } from '@/test/server';
import { API } from '@/test/handlers';
import { authResponse, staffUser } from '@/test/fixtures';
import { useAuthStore } from '@/stores/auth-store';
import { createApiClient } from './api-client';

describe('axios refresh interceptor', () => {
  beforeEach(() => {
    useAuthStore.getState().setSession(authResponse(staffUser, 'old'));
  });

  it('attaches the Bearer token', async () => {
    let auth: string | null = null;
    server.use(
      http.get(`${API}/auth/me`, ({ request }) => {
        auth = request.headers.get('Authorization');
        return HttpResponse.json(staffUser);
      }),
    );
    await createApiClient(API).get('/auth/me');
    expect(auth).toBe('Bearer access-old');
  });

  it('refreshes once for concurrent 401s, then retries each request with the new token', async () => {
    let refreshCalls = 0;
    let refreshBody: unknown = null;
    const seenTokens: string[] = [];
    server.use(
      http.post(`${API}/auth/refresh`, async ({ request }) => {
        refreshCalls++;
        refreshBody = await request.json();
        await new Promise((r) => setTimeout(r, 20));
        return HttpResponse.json(authResponse(staffUser, 'new'));
      }),
      http.get(`${API}/products/:id`, ({ request, params }) => {
        const token = request.headers.get('Authorization') ?? '';
        seenTokens.push(token);
        if (token !== 'Bearer access-new') return new HttpResponse(null, { status: 401 });
        return HttpResponse.json({ id: Number(params.id) });
      }),
    );

    const client = createApiClient(API);
    const results = await Promise.all([client.get('/products/1'), client.get('/products/2'), client.get('/products/3')]);

    expect(results.map((r) => r.data.id)).toEqual([1, 2, 3]);
    expect(refreshCalls).toBe(1);
    expect(refreshBody).toEqual({ refreshToken: 'refresh-old' });
    expect(seenTokens.filter((t) => t === 'Bearer access-new')).toHaveLength(3);
    const state = useAuthStore.getState();
    expect(state.accessToken).toBe('access-new');
    expect(state.refreshToken).toBe('refresh-new');
    // persisted to localStorage
    expect(localStorage.getItem('inventory-auth')).toContain('access-new');
  });

  it('logs out when the refresh call fails', async () => {
    server.use(
      http.post(`${API}/auth/refresh`, () => new HttpResponse(null, { status: 401 })),
      http.get(`${API}/products`, () => new HttpResponse(null, { status: 401 })),
    );
    const client = createApiClient(API);
    await expect(client.get('/products')).rejects.toBeTruthy();
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(useAuthStore.getState().user).toBeNull();
  });

  it('does not try to refresh when login itself returns 401', async () => {
    let refreshCalls = 0;
    server.use(
      http.post(`${API}/auth/refresh`, () => {
        refreshCalls++;
        return HttpResponse.json(authResponse());
      }),
    );
    const client = createApiClient(API);
    await expect(client.post('/auth/login', { email: 'a@b.c', password: 'wrong' })).rejects.toMatchObject({
      response: { status: 401 },
    });
    expect(refreshCalls).toBe(0);
  });
});
