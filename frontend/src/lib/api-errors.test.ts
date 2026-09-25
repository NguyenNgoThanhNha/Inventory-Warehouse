import { AxiosError, AxiosHeaders } from 'axios';
import { toast } from 'sonner';
import { vi } from 'vitest';
import { applyFieldErrors, getErrorMessage, getTraceId, showError } from './api-errors';

vi.mock('sonner', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));

function axiosError(status: number, data: unknown) {
  const config = { headers: new AxiosHeaders() };
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, null, {
    status,
    statusText: '',
    headers: {},
    config,
    data,
  });
}

describe('api-errors', () => {
  it('builds messages from ProblemDetails and falls back on status', () => {
    expect(getErrorMessage(axiosError(404, { title: 'Không tìm thấy', detail: "StockDocument '9' không tồn tại." }))).toBe(
      "Không tìm thấy: StockDocument '9' không tồn tại.",
    );
    expect(getErrorMessage(axiosError(400, { errors: { title: ['Title is required'] } }))).toBe('Title is required');
    expect(getErrorMessage(axiosError(403, null))).toBe('Bạn không có quyền thực hiện thao tác này');
  });

  it('maps camelCase and PascalCase error keys onto known fields only', () => {
    const setError = vi.fn();
    const err = axiosError(400, { errors: { Title: ['Trùng'], categoryId: ['Sai'], unknown: ['x'] } });
    expect(applyFieldErrors(err, ['title', 'categoryId'] as const, setError)).toBe(true);
    expect(setError).toHaveBeenCalledWith('title', { type: 'server', message: 'Trùng' });
    expect(setError).toHaveBeenCalledWith('categoryId', { type: 'server', message: 'Sai' });
    expect(setError).toHaveBeenCalledTimes(2);
  });

  it('error toasts show the traceId with a "Copy traceId" action', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const err = axiosError(500, { title: 'Server error', traceId: '00-abc-01' });
    expect(getTraceId(err)).toBe('00-abc-01');

    showError(err);
    expect(toast.error).toHaveBeenCalledWith(
      'Server error',
      expect.objectContaining({
        description: 'traceId: 00-abc-01',
        action: expect.objectContaining({ label: 'Copy traceId' }),
      }),
    );
    const options = vi.mocked(toast.error).mock.calls[0][1] as unknown as { action: { onClick: () => void } };
    options.action.onClick();
    expect(writeText).toHaveBeenCalledWith('00-abc-01');
  });
});
