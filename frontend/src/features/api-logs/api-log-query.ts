import { dayjs } from '@/lib/date';
import { toPositiveInt } from '@/lib/hooks/use-url-params';
import type { ApiLogsQuery } from '@/types';

export const API_LOG_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const;
export const DEFAULT_PAGE_SIZE = 20;

/** URL filter state; `from` / `to` are local dates (yyyy-MM-dd). */
export interface ApiLogFilters {
  traceId?: string;
  url?: string;
  method?: string;
  statusCode?: number;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

export function parseApiLogFilters(params: URLSearchParams): ApiLogFilters {
  const method = params.get('method')?.toUpperCase();
  const from = params.get('from');
  const to = params.get('to');
  const statusCode = Number(params.get('statusCode'));
  return {
    traceId: params.get('traceId')?.trim() || undefined,
    url: params.get('url')?.trim() || undefined,
    method: method && (API_LOG_METHODS as readonly string[]).includes(method) ? method : undefined,
    statusCode: Number.isInteger(statusCode) && statusCode >= 100 && statusCode <= 599 ? statusCode : undefined,
    from: from && DATE_RE.test(from) ? from : undefined,
    to: to && DATE_RE.test(to) ? to : undefined,
    page: toPositiveInt(params.get('page')) ?? 1,
    pageSize: Math.min(toPositiveInt(params.get('pageSize')) ?? DEFAULT_PAGE_SIZE, 100),
  };
}

/**
 * Filters → API query. The API compares `from` / `to` against the log's DateTime (UTC), so local
 * dates are sent as the start / end of that local day in ISO UTC (a bare `to=yyyy-MM-dd` would mean midnight).
 */
export function toApiLogsQuery(f: ApiLogFilters): ApiLogsQuery {
  return {
    traceId: f.traceId,
    url: f.url,
    method: f.method,
    statusCode: f.statusCode,
    from: f.from ? dayjs(f.from).startOf('day').toISOString() : undefined,
    to: f.to ? dayjs(f.to).endOf('day').toISOString() : undefined,
    page: f.page,
    pageSize: f.pageSize,
  };
}

/** Pretty-prints JSON; returns the raw text when it is not valid JSON. */
export function prettyJson(text: string | null | undefined): string {
  if (!text) return '';
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}
