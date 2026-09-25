import { useMemo } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { DataTable } from '@/components/common/data-table';
import { PageHeader } from '@/components/common/page-header';
import { useWarehouses } from '@/features/catalog';
import { formatDateTimeShort } from '@/lib/date';
import { formatQty } from '@/lib/format';
import { useDebouncedCallback } from '@/lib/hooks/use-debounced-callback';
import { oneOf, toPositiveInt, useUrlParams } from '@/lib/hooks/use-url-params';
import { useCan } from '@/stores/auth-store';
import { DOCUMENT_STATUSES, type DocumentType, type StockDocumentListItemDto } from '@/types';
import { DocumentStatusBadge } from '../components/document-badges';
import { DOCUMENT_CONFIG, REASON_LABEL, STATUS_LABEL } from '../config';
import { useDocuments } from '../hooks/use-documents';

const ALL = 'all';

export function DocumentListPage({ type }: { type: DocumentType }) {
  const cfg = DOCUMENT_CONFIG[type];
  const navigate = useNavigate();
  const [params, update] = useUrlParams();
  const query = {
    status: oneOf(DOCUMENT_STATUSES, params.get('status')),
    warehouseId: toPositiveInt(params.get('warehouseId')),
    search: params.get('search') ?? undefined,
    page: toPositiveInt(params.get('page')) ?? 1,
    pageSize: toPositiveInt(params.get('pageSize')) ?? 20,
  };
  const { data, isFetching, isError, refetch } = useDocuments(cfg, query);
  const { data: warehouses = [] } = useWarehouses();
  const canCreate = useCan(cfg.activity, 'C');
  const debouncedSearch = useDebouncedCallback((v: string) => update({ search: v.trim() }));

  const columns = useMemo<ColumnDef<StockDocumentListItemDto>[]>(
    () => [
      {
        id: 'code',
        header: 'Mã phiếu',
        cell: ({ row }) => (
          <Link to={`/${cfg.path}/${row.original.id}`} className="font-mono text-xs font-medium text-primary hover:underline" onClick={(e) => e.stopPropagation()}>
            {row.original.code}
          </Link>
        ),
      },
      { id: 'status', header: 'Trạng thái', cell: ({ row }) => <DocumentStatusBadge status={row.original.status} /> },
      {
        id: 'warehouse',
        header: type === 'Transfer' ? 'Kho xuất → nhận' : 'Kho',
        cell: ({ row }) =>
          row.original.toWarehouseName ? `${row.original.warehouseName} → ${row.original.toWarehouseName}` : row.original.warehouseName,
      },
      ...(type === 'GoodsReceipt'
        ? [{ id: 'supplier', header: 'Nhà cung cấp', cell: ({ row }) => row.original.supplierName ?? '—' } as ColumnDef<StockDocumentListItemDto>]
        : []),
      ...(type === 'GoodsIssue'
        ? [{ id: 'reason', header: 'Lý do', cell: ({ row }) => (row.original.reason ? REASON_LABEL[row.original.reason] : '—') } as ColumnDef<StockDocumentListItemDto>]
        : []),
      {
        id: 'lines',
        header: 'Số dòng',
        meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
        cell: ({ row }) => row.original.lineCount,
      },
      {
        id: 'qty',
        header: 'Tổng SL',
        meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
        cell: ({ row }) => formatQty(row.original.totalQuantity),
      },
      { id: 'created', header: 'Lập lúc', cell: ({ row }) => formatDateTimeShort(row.original.createdDate) },
      { id: 'creator', header: 'Người lập', cell: ({ row }) => row.original.createdName ?? 'Hệ thống' },
      { id: 'posted', header: 'Ghi sổ lúc', cell: ({ row }) => formatDateTimeShort(row.original.postedAt) },
    ],
    [cfg.path, type],
  );

  return (
    <div className="space-y-4">
      <PageHeader
        title={cfg.title}
        actions={
          canCreate && (
            <Button asChild>
              <Link to={`/${cfg.path}/new`}>
                <Plus /> Lập {cfg.noun}
              </Link>
            </Button>
          )
        }
      />
      <Card>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative w-full max-w-xs">
              <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                aria-label="Tìm mã phiếu"
                placeholder="Mã phiếu..."
                className="pl-8"
                defaultValue={query.search}
                onChange={(e) => debouncedSearch.run(e.target.value)}
              />
            </div>
            <Select value={query.status ?? ALL} onValueChange={(v) => update({ status: v === ALL ? undefined : v })}>
              <SelectTrigger aria-label="Trạng thái" className="w-40">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Mọi trạng thái</SelectItem>
                {DOCUMENT_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {STATUS_LABEL[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={query.warehouseId ? String(query.warehouseId) : ALL} onValueChange={(v) => update({ warehouseId: v === ALL ? undefined : v })}>
              <SelectTrigger aria-label="Kho" className="w-52">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả kho</SelectItem>
                {warehouses.map((w) => (
                  <SelectItem key={w.id} value={String(w.id)}>
                    {w.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <DataTable
            aria-label={cfg.title}
            columns={columns}
            data={data?.items ?? []}
            getRowId={(d) => String(d.id)}
            loading={isFetching}
            error={isError ? 'Không tải được danh sách phiếu' : undefined}
            onRetry={() => void refetch()}
            onRowClick={(d) => navigate(`/${cfg.path}/${d.id}`)}
            emptyText={`Chưa có ${cfg.noun} nào`}
            pagination={{
              page: query.page,
              pageSize: query.pageSize,
              totalCount: data?.totalCount ?? 0,
              onPageChange: (page) => update({ page }, false),
              onPageSizeChange: (pageSize) => update({ pageSize }),
            }}
          />
        </CardContent>
      </Card>
    </div>
  );
}
