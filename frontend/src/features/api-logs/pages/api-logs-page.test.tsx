import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/server';
import { API } from '@/test/handlers';
import { adminUser, apiLogDetail, apiLogItems } from '@/test/fixtures';
import { loginAs, renderWithProviders } from '@/test/render';
import { chooseSelectOption } from '@/test/ui';
import { parseApiLogFilters, prettyJson, toApiLogsQuery } from '../api-log-query';
import { ApiLogsPage } from './api-logs-page';

function captureRequests() {
  const calls: URLSearchParams[] = [];
  server.use(
    http.get(`${API}/api-logs`, ({ request }) => {
      const params = new URL(request.url).searchParams;
      calls.push(params);
      return HttpResponse.json({ items: apiLogItems, totalCount: apiLogItems.length, page: 1, pageSize: 20 });
    }),
  );
  return calls;
}

describe('api-log-query', () => {
  it('parses URL filters and converts local dates into an ISO range covering whole days', () => {
    const f = parseApiLogFilters(
      new URLSearchParams('method=post&statusCode=404&from=2026-09-01&to=2026-09-23&traceId=%2000-abc%20&bogus=1'),
    );
    expect(f).toMatchObject({ method: 'POST', statusCode: 404, from: '2026-09-01', to: '2026-09-23', traceId: '00-abc', page: 1 });
    const q = toApiLogsQuery(f);
    expect(new Date(q.from!).getTime()).toBe(new Date(2026, 8, 1, 0, 0, 0, 0).getTime());
    expect(new Date(q.to!).getTime()).toBe(new Date(2026, 8, 23, 23, 59, 59, 999).getTime());
    expect(q.to).toMatch(/Z$/);
    expect(parseApiLogFilters(new URLSearchParams('statusCode=abc&method=TRACE')).statusCode).toBeUndefined();
    expect(prettyJson('{"a":1}')).toBe('{\n  "a": 1\n}');
    expect(prettyJson('not json')).toBe('not json');
  });
});

describe('ApiLogsPage', () => {
  beforeEach(() => loginAs(adminUser));

  it('lists logs and sends filters (from the URL and the filter bar) to the API', async () => {
    const user = userEvent.setup();
    const calls = captureRequests();
    renderWithProviders(<ApiLogsPage />, { path: '/api-logs', route: '/api-logs?url=goods-issues&page=2' });

    const table = await screen.findByRole('table', { name: 'API logs' });
    expect(await within(table).findByText('/api/v1/goods-issues/999999')).toBeInTheDocument();
    expect(within(table).getByText('404')).toBeInTheDocument();
    expect(within(table).getByText('620 ms')).toBeInTheDocument();
    expect(calls[calls.length - 1].get('url')).toBe('goods-issues');
    expect(calls[calls.length - 1].get('page')).toBe('2');

    await chooseSelectOption(user, 'Method', 'GET');
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/api-logs?url=goods-issues&method=GET'));

    await user.type(screen.getByLabelText('Status code'), '404');
    await user.type(screen.getByLabelText('traceId'), '00-8e50');
    await user.click(screen.getByRole('button', { name: /Lọc/ }));
    await waitFor(() => {
      const last = calls[calls.length - 1];
      expect(last.get('method')).toBe('GET');
      expect(last.get('statusCode')).toBe('404');
      expect(last.get('traceId')).toBe('00-8e50');
      expect(last.get('page')).toBe('1');
    });
  });

  it('opens a detail sheet with pretty-printed request / response JSON and copy buttons', async () => {
    const user = userEvent.setup();
    let detailId: string | undefined;
    server.use(
      http.get(`${API}/api-logs/:id`, ({ params }) => {
        detailId = String(params.id);
        return HttpResponse.json(apiLogDetail(Number(params.id)));
      }),
    );
    renderWithProviders(<ApiLogsPage />, { path: '/api-logs', route: '/api-logs' });

    await user.click(await screen.findByText('/api/v1/auth/login'));
    const sheet = await screen.findByRole('dialog');
    expect(within(sheet).getByText('API log #86')).toBeInTheDocument();
    await waitFor(() => expect(detailId).toBe('86'));

    const request = await within(sheet).findByLabelText('Request');
    expect(request.tagName).toBe('PRE');
    expect(request.textContent).toBe('{\n  "email": "admin@inventory.local",\n  "password": "***"\n}');
    expect(within(sheet).getByLabelText('Response').textContent).toContain('"status": 404');
    expect(within(sheet).getByText(apiLogItems[1].traceId)).toBeInTheDocument();

    await user.click(within(sheet).getByRole('button', { name: 'Copy request' }));
    expect(await navigator.clipboard.readText()).toContain('"email": "admin@inventory.local"');
    expect(within(sheet).getByText('Đã copy')).toBeInTheDocument();
  });
});
