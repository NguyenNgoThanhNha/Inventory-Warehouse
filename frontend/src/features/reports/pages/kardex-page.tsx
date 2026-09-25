import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { BookOpen } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { DataTablePagination } from '@/components/common/data-table';
import { DateRangePicker } from '@/components/common/date-range-picker';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { ProductPicker, useWarehouses } from '@/features/catalog';
import { DOCUMENT_CONFIG } from '@/features/documents';
import { dayjs, formatDateTime, toApiDate } from '@/lib/date';
import { formatQty } from '@/lib/format';
import { toPositiveInt, useUrlParams } from '@/lib/hooks/use-url-params';
import { cn } from '@/lib/utils';
import type { KardexQuery, MovementType } from '@/types';
import { useKardex } from '../hooks/use-reports';

const ALL = 'all';
const DATE = /^\d{4}-\d{2}-\d{2}$/;

export const MOVEMENT_LABEL: Record<MovementType, string> = {
  In: 'Nhập',
  Out: 'Xuất',
  TransferIn: 'Chuyển đến',
  TransferOut: 'Chuyển đi',
  Adjust: 'Kiểm kê',
};

/** Sổ nhập – xuất – tồn (spec §4.3): tồn đầu kỳ, từng chứng từ, tồn cuối lũy kế (running total tính ở SQL). */
export function KardexPage() {
  const [params, update] = useUrlParams();
  const productId = toPositiveInt(params.get('productId'));
  const warehouseId = toPositiveInt(params.get('warehouseId'));
  const from = DATE.test(params.get('from') ?? '') ? params.get('from')! : undefined;
  const to = DATE.test(params.get('to') ?? '') ? params.get('to')! : undefined;
  const page = toPositiveInt(params.get('page')) ?? 1;
  const pageSize = toPositiveInt(params.get('pageSize')) ?? 50;

  const query = useMemo<KardexQuery | undefined>(
    () => (productId ? { productId, warehouseId, from, to, page, pageSize } : undefined),
    [productId, warehouseId, from, to, page, pageSize],
  );
  const { data, isFetching, isError } = useKardex(query);
  const { data: warehouses = [] } = useWarehouses();

  const picked = data && data.productId === productId
    ? { id: data.productId, sku: data.sku, name: data.productName, unit: data.unit, cost: 0 }
    : null;
  const range = data ? { from: dayjs(data.from).toDate(), to: dayjs(data.to).toDate() } : undefined;
  const showOpeningRow = page === 1;

  return (
    <div className="space-y-4">
      <PageHeader title="Thẻ kho" description="Sổ nhập – xuất – tồn của một sản phẩm theo khoảng thời gian" />

      <div className="flex flex-wrap items-center gap-2">
        <div className="w-full max-w-sm">
          <ProductPicker
            aria-label="Sản phẩm"
            value={picked}
            onChange={(p) => update({ productId: p.id })}
          />
        </div>
        <Select value={warehouseId ? String(warehouseId) : ALL} onValueChange={(v) => update({ warehouseId: v === ALL ? undefined : v })}>
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
        <DateRangePicker
          aria-label="Khoảng ngày"
          value={range}
          onChange={(r) => update({ from: r ? toApiDate(r.from) : undefined, to: r ? toApiDate(r.to) : undefined })}
        />
      </div>

      {!productId ? (
        <Card>
          <CardContent>
            <EmptyState icon={<BookOpen />} title="Chọn sản phẩm để xem thẻ kho" description="Mặc định hiển thị 30 ngày gần nhất." />
          </CardContent>
        </Card>
      ) : isError && !data ? (
        <Card>
          <CardContent>
            <EmptyState title="Không tải được thẻ kho" />
          </CardContent>
        </Card>
      ) : !data ? (
        <Skeleton className="h-64 w-full" />
      ) : (
        <Card>
          <CardContent className="space-y-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <div className="font-medium">
                <span className="font-mono">{data.sku}</span> — {data.productName}{' '}
                <span className="font-normal text-muted-foreground">
                  ({data.unit}) · {warehouseId ? warehouses.find((w) => w.id === warehouseId)?.name : 'Tất cả kho'} ·{' '}
                  {dayjs(data.from).format('DD/MM/YYYY')} → {dayjs(data.to).format('DD/MM/YYYY')}
                </span>
              </div>
              <div className="flex gap-4 text-sm tabular-nums">
                <span>Đầu kỳ <b>{formatQty(data.opening)}</b></span>
                <span className="text-emerald-600 dark:text-emerald-400">Nhập <b>+{formatQty(data.totalIn)}</b></span>
                <span className="text-red-600 dark:text-red-400">Xuất <b>−{formatQty(data.totalOut)}</b></span>
                <span>Cuối kỳ <b>{formatQty(data.closing)}</b></span>
              </div>
            </div>

            <div className={cn('overflow-x-auto rounded-md border', isFetching && 'opacity-60')}>
              <Table aria-label="Thẻ kho">
                <TableHeader>
                  <TableRow>
                    <TableHead>Thời gian</TableHead>
                    <TableHead>Chứng từ</TableHead>
                    <TableHead>Nghiệp vụ</TableHead>
                    {!warehouseId && <TableHead>Kho</TableHead>}
                    <TableHead className="text-right">Nhập</TableHead>
                    <TableHead className="text-right">Xuất</TableHead>
                    <TableHead className="text-right">Tồn cuối</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {showOpeningRow && (
                    <TableRow className="bg-muted/40">
                      <TableCell>{dayjs(data.from).format('DD/MM/YYYY')}</TableCell>
                      <TableCell colSpan={warehouseId ? 4 : 5} className="font-medium">
                        Tồn đầu kỳ
                      </TableCell>
                      <TableCell className="text-right font-semibold tabular-nums">{formatQty(data.opening)}</TableCell>
                    </TableRow>
                  )}
                  {data.rows.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell className="whitespace-nowrap">{formatDateTime(r.occurredAt)}</TableCell>
                      <TableCell>
                        <Link
                          to={`/${DOCUMENT_CONFIG[r.documentType].path}/${r.documentId}`}
                          className="font-mono text-xs font-medium text-primary hover:underline"
                        >
                          {r.documentCode}
                        </Link>
                      </TableCell>
                      <TableCell>{MOVEMENT_LABEL[r.movementType]}</TableCell>
                      {!warehouseId && <TableCell>{r.warehouseCode}</TableCell>}
                      <TableCell className="text-right tabular-nums text-emerald-600 dark:text-emerald-400">
                        {r.inQty ? `+${formatQty(r.inQty)}` : ''}
                      </TableCell>
                      <TableCell className="text-right tabular-nums text-red-600 dark:text-red-400">
                        {r.outQty ? `−${formatQty(r.outQty)}` : ''}
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">{formatQty(r.balance)}</TableCell>
                    </TableRow>
                  ))}
                  {data.rows.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                        Không có phát sinh trong kỳ
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
                <TableFooter>
                  <TableRow>
                    <TableCell colSpan={warehouseId ? 3 : 4} className="text-right">
                      Cộng kỳ ({data.totalCount} dòng)
                    </TableCell>
                    <TableCell className="text-right tabular-nums">+{formatQty(data.totalIn)}</TableCell>
                    <TableCell className="text-right tabular-nums">−{formatQty(data.totalOut)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatQty(data.closing)}</TableCell>
                  </TableRow>
                </TableFooter>
              </Table>
            </div>
            {data.totalCount > pageSize && (
              <DataTablePagination
                page={page}
                pageSize={pageSize}
                totalCount={data.totalCount}
                onPageChange={(p) => update({ page: p }, false)}
                onPageSizeChange={(s) => update({ pageSize: s })}
                pageSizeOptions={[20, 50, 100]}
              />
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
