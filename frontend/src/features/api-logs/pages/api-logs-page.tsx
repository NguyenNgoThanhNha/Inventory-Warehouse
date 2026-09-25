import { useMemo, useState } from 'react';
import type { ColumnDef } from '@tanstack/react-table';
import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { DataTable } from '@/components/common/data-table';
import { PageHeader } from '@/components/common/page-header';
import { formatDateTimeSeconds } from '@/lib/date';
import { useUrlParams } from '@/lib/hooks/use-url-params';
import { cn } from '@/lib/utils';
import type { ApiLogListItemDto } from '@/types';
import { DEFAULT_PAGE_SIZE, parseApiLogFilters, toApiLogsQuery } from '../api-log-query';
import { ApiLogDetailSheet } from '../components/api-log-detail-sheet';
import { ApiLogFilters } from '../components/api-log-filters';
import { MethodBadge, StatusCodeBadge } from '../components/http-badges';
import { useApiLogs } from '../hooks/use-api-logs';

const columns: ColumnDef<ApiLogListItemDto>[] = [
  {
    id: 'createdDate',
    header: 'Thời gian',
    cell: ({ row }) => <span className="whitespace-nowrap tabular-nums">{formatDateTimeSeconds(row.original.createdDate)}</span>,
  },
  { id: 'method', header: 'Method', cell: ({ row }) => <MethodBadge method={row.original.method} /> },
  {
    id: 'url',
    header: 'URL',
    cell: ({ row }) => (
      <code className="line-clamp-1 max-w-[20rem] font-mono text-xs break-all" title={row.original.url}>
        {row.original.url}
      </code>
    ),
  },
  { id: 'statusCode', header: 'Status', cell: ({ row }) => <StatusCodeBadge code={row.original.statusCode} /> },
  {
    id: 'duration',
    header: 'Thời lượng',
    cell: ({ row }) => `${row.original.durationMs} ms`,
    meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
  },
  {
    id: 'user',
    header: 'User',
    cell: ({ row }) => row.original.userName ?? <span className="text-muted-foreground">—</span>,
  },
  {
    id: 'traceId',
    header: 'traceId',
    cell: ({ row }) => (
      <code className="block max-w-[11rem] truncate font-mono text-xs text-muted-foreground" title={row.original.traceId}>
        {row.original.traceId}
      </code>
    ),
  },
];

export function ApiLogsPage() {
  const [searchParams, updateParams] = useUrlParams();
  const filters = useMemo(() => parseApiLogFilters(searchParams), [searchParams]);
  const query = useMemo(() => toApiLogsQuery(filters), [filters]);
  const logs = useApiLogs(query);
  const [selectedId, setSelectedId] = useState<number | undefined>();

  return (
    <div className="space-y-4">
      <PageHeader
        title="API Logs"
        description="Log request/response của API để debug; traceId trùng với traceId trong thông báo lỗi."
        actions={
          <Button variant="outline" size="icon" aria-label="Tải lại" onClick={() => void logs.refetch()}>
            <RefreshCw className={cn(logs.isFetching && 'animate-spin')} />
          </Button>
        }
      />
      <Card>
        <CardContent className="space-y-4">
          <ApiLogFilters filters={filters} onChange={updateParams} />
          <DataTable
            aria-label="API logs"
            columns={columns}
            data={logs.data?.items ?? []}
            getRowId={(l) => String(l.id)}
            loading={logs.isFetching}
            onRowClick={(l) => setSelectedId(l.id)}
            emptyText="Không có log nào"
            error={logs.isError ? 'Không tải được API logs' : undefined}
            onRetry={() => void logs.refetch()}
            pagination={{
              page: filters.page,
              pageSize: filters.pageSize,
              totalCount: logs.data?.totalCount ?? 0,
              onPageChange: (page) => updateParams({ page: page > 1 ? page : undefined }, false),
              onPageSizeChange: (size) => updateParams({ pageSize: size !== DEFAULT_PAGE_SIZE ? size : undefined }),
            }}
          />
        </CardContent>
      </Card>
      <ApiLogDetailSheet id={selectedId} onClose={() => setSelectedId(undefined)} />
    </div>
  );
}
