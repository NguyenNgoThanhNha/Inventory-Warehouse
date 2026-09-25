import { useEffect, useState } from 'react';
import { FilterX, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { DateRangePicker } from '@/components/common/date-range-picker';
import { dayjs, toApiDate } from '@/lib/date';
import type { ParamPatch } from '@/lib/hooks/use-url-params';
import { API_LOG_METHODS, type ApiLogFilters as Filters } from '../api-log-query';

const ALL = '__all__';

/** traceId / url / statusCode are applied on submit; method and date range apply immediately. */
export function ApiLogFilters({ filters, onChange }: { filters: Filters; onChange: (patch: ParamPatch) => void }) {
  const [traceId, setTraceId] = useState(filters.traceId ?? '');
  const [url, setUrl] = useState(filters.url ?? '');
  const [statusCode, setStatusCode] = useState(filters.statusCode ? String(filters.statusCode) : '');

  useEffect(() => setTraceId(filters.traceId ?? ''), [filters.traceId]);
  useEffect(() => setUrl(filters.url ?? ''), [filters.url]);
  useEffect(() => setStatusCode(filters.statusCode ? String(filters.statusCode) : ''), [filters.statusCode]);

  const hasFilters = !!(filters.traceId || filters.url || filters.method || filters.statusCode || filters.from || filters.to);
  const range = filters.from && filters.to ? { from: dayjs(filters.from).toDate(), to: dayjs(filters.to).toDate() } : undefined;

  return (
    <form
      className="flex flex-wrap items-center gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        onChange({ traceId: traceId.trim(), url: url.trim(), statusCode: statusCode.trim() });
      }}
    >
      <Input
        aria-label="traceId"
        placeholder="traceId"
        className="w-full font-mono sm:w-72"
        value={traceId}
        onChange={(e) => setTraceId(e.target.value)}
      />
      <Input
        aria-label="URL"
        placeholder="URL chứa..."
        className="w-full sm:w-52"
        value={url}
        onChange={(e) => setUrl(e.target.value)}
      />
      <Select value={filters.method ?? ALL} onValueChange={(v) => onChange({ method: v === ALL ? undefined : v })}>
        <SelectTrigger aria-label="Method" className="w-32">
          <SelectValue placeholder="Method" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>
            <span className="text-muted-foreground">Method: tất cả</span>
          </SelectItem>
          {API_LOG_METHODS.map((m) => (
            <SelectItem key={m} value={m}>
              {m}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Input
        aria-label="Status code"
        placeholder="Status code"
        inputMode="numeric"
        type="number"
        min={100}
        max={599}
        className="w-32"
        value={statusCode}
        onChange={(e) => setStatusCode(e.target.value)}
      />
      <DateRangePicker
        aria-label="Khoảng thời gian"
        allowClear
        placeholder="Mọi thời điểm"
        value={range}
        onChange={(r) => onChange({ from: r ? toApiDate(r.from) : undefined, to: r ? toApiDate(r.to) : undefined })}
      />
      <Button type="submit">
        <Search /> Lọc
      </Button>
      {hasFilters && (
        <Button
          type="button"
          variant="ghost"
          onClick={() =>
            onChange({ traceId: undefined, url: undefined, method: undefined, statusCode: undefined, from: undefined, to: undefined })
          }
        >
          <FilterX /> Xóa lọc
        </Button>
      )}
    </form>
  );
}
