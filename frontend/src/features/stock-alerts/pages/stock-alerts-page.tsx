import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { Loader2, ScanSearch } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DataTable } from '@/components/common/data-table';
import { PageHeader } from '@/components/common/page-header';
import { useWarehouses } from '@/features/catalog';
import { api, cleanParams } from '@/lib/api-client';
import { formatDateTimeShort } from '@/lib/date';
import { formatQty } from '@/lib/format';
import { toPositiveInt, useUrlParams } from '@/lib/hooks/use-url-params';
import { queryKeys } from '@/lib/query-client';
import { cn } from '@/lib/utils';
import { useCan } from '@/stores/auth-store';
import type { PagedResult, ScanLowStockResultDto, StockAlertDto } from '@/types';

const ALL = 'all';

interface AlertsQuery {
  isResolved: boolean;
  warehouseId?: number;
  page: number;
  pageSize: number;
}

const alertsApi = {
  list: (q: AlertsQuery) => api.get<PagedResult<StockAlertDto>>('/stock-alerts', { params: cleanParams(q) }).then((r) => r.data),
  scan: () => api.post<ScanLowStockResultDto>('/stock-alerts/scan').then((r) => r.data),
};

/**
 * Cảnh báo tồn thấp do job nền mở / đóng (spec §3.4). Tab "Đang mở": hàng còn dưới ngưỡng;
 * "Đã xử lý": tồn đã hồi (nhập thêm) hoặc ngưỡng đã bỏ.
 */
export function StockAlertsPage() {
  const [params, update] = useUrlParams();
  const query: AlertsQuery = {
    isResolved: params.get('tab') === 'resolved',
    warehouseId: toPositiveInt(params.get('warehouseId')),
    page: toPositiveInt(params.get('page')) ?? 1,
    pageSize: toPositiveInt(params.get('pageSize')) ?? 20,
  };
  const queryClient = useQueryClient();
  const { data, isFetching } = useQuery({
    queryKey: queryKeys.stockAlertList(query),
    queryFn: () => alertsApi.list(query),
    placeholderData: keepPreviousData,
  });
  const { data: warehouses = [] } = useWarehouses();
  const canScan = useCan('WAREHOUSE', 'U');
  const scan = useMutation({
    mutationFn: alertsApi.scan,
    onSuccess: (r) => {
      toast.success(`Đã quét: ${r.opened} cảnh báo mới, ${r.resolved} đã hết, ${r.stillOpen} đang mở`);
      void queryClient.invalidateQueries({ queryKey: queryKeys.stockAlerts });
      void queryClient.invalidateQueries({ queryKey: queryKeys.notifications });
    },
  });

  const columns = useMemo<ColumnDef<StockAlertDto>[]>(
    () => [
      {
        id: 'product',
        header: 'Sản phẩm',
        cell: ({ row }) => (
          <Link to={`/kardex?productId=${row.original.productId}&warehouseId=${row.original.warehouseId}`} className="hover:underline">
            <span className="font-mono text-xs text-muted-foreground">{row.original.sku}</span>{' '}
            <span className="font-medium">{row.original.productName}</span>
          </Link>
        ),
      },
      { id: 'warehouse', header: 'Kho', cell: ({ row }) => row.original.warehouseCode },
      {
        id: 'atAlert',
        header: 'Tồn lúc báo',
        meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
        cell: ({ row }) => formatQty(row.original.quantityAtAlert),
      },
      {
        id: 'current',
        header: 'Tồn hiện tại',
        meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
        cell: ({ row }) => {
          const { currentQuantity: q, minThreshold } = row.original;
          return <span className={cn(q !== null && q < minThreshold && 'font-semibold text-red-600 dark:text-red-400')}>{formatQty(q)}</span>;
        },
      },
      {
        id: 'threshold',
        header: 'Ngưỡng',
        meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
        cell: ({ row }) => formatQty(row.original.minThreshold),
      },
      { id: 'createdAt', header: 'Phát hiện', cell: ({ row }) => formatDateTimeShort(row.original.createdAt) },
      ...(query.isResolved
        ? [{ id: 'resolvedAt', header: 'Hết cảnh báo', cell: ({ row }) => formatDateTimeShort(row.original.resolvedAt) } as ColumnDef<StockAlertDto>]
        : []),
    ],
    [query.isResolved],
  );

  return (
    <div className="space-y-4">
      <PageHeader
        title="Cảnh báo tồn thấp"
        description="Job nền quét định kỳ: tồn rơi xuống dưới ngưỡng thì mở cảnh báo và báo cho quản lý kho; nhập đủ hàng thì cảnh báo tự đóng."
        actions={
          canScan && (
            <Button variant="outline" disabled={scan.isPending} onClick={() => scan.mutate()}>
              {scan.isPending ? <Loader2 className="animate-spin" /> : <ScanSearch />}
              Quét ngay
            </Button>
          )
        }
      />
      <Card>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Tabs value={query.isResolved ? 'resolved' : 'open'} onValueChange={(v) => update({ tab: v === 'open' ? undefined : v })}>
              <TabsList>
                <TabsTrigger value="open">Đang mở</TabsTrigger>
                <TabsTrigger value="resolved">Đã xử lý</TabsTrigger>
              </TabsList>
            </Tabs>
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
            aria-label="Cảnh báo tồn"
            columns={columns}
            data={data?.items ?? []}
            getRowId={(a) => String(a.id)}
            loading={isFetching}
            emptyText={query.isResolved ? 'Chưa có cảnh báo nào được xử lý' : 'Không có cảnh báo nào đang mở'}
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
