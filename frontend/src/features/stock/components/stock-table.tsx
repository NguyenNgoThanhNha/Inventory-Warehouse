import { useEffect, useRef, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useVirtualizer } from '@tanstack/react-virtual';
import { ArrowDown, ArrowUp, ArrowUpDown, Loader2, PackageSearch, TriangleAlert } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatQty } from '@/lib/format';
import { useIsMobile } from '@/lib/hooks/use-mobile';
import type { StockCellDto, StockRowDto, StockSort, WarehouseDto } from '@/types';
import type { ThresholdTarget } from './threshold-dialog';

export const STOCK_SORT_LABEL: Record<StockSort, string> = {
  Sku: 'Theo SKU',
  Name: 'Theo tên A→Z',
  TotalDesc: 'Tồn nhiều nhất',
  TotalAsc: 'Tồn ít nhất',
};

export const ROW_HEIGHT = 48;
const MOBILE_ROW_HEIGHT = 84;
/** Start loading the next page when this many rows remain below the viewport. */
const PREFETCH_ROWS = 30;

interface StockTableProps {
  rows: StockRowDto[];
  warehouses: WarehouseDto[];
  totalCount: number;
  loading: boolean;
  error: boolean;
  hasNextPage: boolean;
  fetchingNextPage: boolean;
  onLoadMore: () => void;
  onRetry: () => void;
  /** set when the user may edit thresholds (WAREHOUSE:U) */
  onEditThreshold?: (target: ThresholdTarget) => void;
  /** shown in the empty state when filters are active */
  onClearFilters?: () => void;
  sort: StockSort;
  onSortChange: (sort: StockSort) => void;
}

const cellFor = (row: StockRowDto, warehouseId: number) => row.warehouses.find((w) => w.warehouseId === warehouseId);

function thresholdTarget(row: StockRowDto, w: WarehouseDto, cell: StockCellDto | undefined): ThresholdTarget {
  return {
    productId: row.productId,
    sku: row.sku,
    productName: row.name,
    warehouseId: w.id,
    warehouseName: w.name,
    quantity: cell?.quantity ?? 0,
    minThreshold: cell?.minThreshold ?? 0,
  };
}

/** One warehouse quantity; a button (opens the threshold dialog) when the user may edit thresholds. */
function QuantityCell({
  row,
  warehouse,
  onEditThreshold,
  compact,
}: {
  row: StockRowDto;
  warehouse: WarehouseDto;
  onEditThreshold?: (target: ThresholdTarget) => void;
  compact?: boolean;
}) {
  const cell = cellFor(row, warehouse.id);
  const content = (
    <>
      {compact && <span className="mr-1 text-muted-foreground">{warehouse.code}</span>}
      <span className={cn('tabular-nums', cell?.isLow && 'font-semibold text-red-600 dark:text-red-400')}>
        {cell ? formatQty(cell.quantity) : '—'}
      </span>
      {cell && cell.minThreshold > 0 && <span className="ml-0.5 text-[11px] text-muted-foreground">/{formatQty(cell.minThreshold)}</span>}
    </>
  );
  const className = compact ? 'shrink-0 whitespace-nowrap rounded border px-1.5 py-0.5 text-xs' : 'w-full rounded px-2 py-1 text-right';
  if (!onEditThreshold) return <span className={cn(className, !compact && 'block')}>{content}</span>;
  return (
    <button
      type="button"
      className={cn(className, 'hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none')}
      title="Đặt ngưỡng tồn tối thiểu"
      aria-label={`${row.sku} tại ${warehouse.code}: ${cell ? formatQty(cell.quantity) : 0}${cell?.isLow ? ', dưới ngưỡng' : ''}. Đặt ngưỡng`}
      onClick={() => onEditThreshold(thresholdTarget(row, warehouse, cell))}
    >
      {content}
    </button>
  );
}

/** Desktop column header that sorts on click; aria-sort sits on the columnheader, the button carries the action. */
function SortableHeader({
  children,
  hint,
  direction,
  next,
  onSort,
  className,
}: {
  children: ReactNode;
  hint?: string;
  direction?: 'ascending' | 'descending';
  next: StockSort;
  onSort: (sort: StockSort) => void;
  className?: string;
}) {
  const Icon = direction === 'descending' ? ArrowDown : direction === 'ascending' ? ArrowUp : ArrowUpDown;
  return (
    <div role="columnheader" aria-sort={direction ?? 'none'} className={className}>
      <button
        type="button"
        title={`Sắp xếp: ${STOCK_SORT_LABEL[next]}`}
        onClick={() => onSort(next)}
        className={cn(
          'inline-flex items-center gap-1 rounded-sm hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
          direction && 'text-foreground',
        )}
      >
        {children}
        {hint && <span className="font-normal text-muted-foreground">· {hint}</span>}
        <Icon className={cn('size-3.5', !direction && 'opacity-40')} aria-hidden />
      </button>
    </div>
  );
}

function LowBadge() {
  return <span className="rounded bg-red-600 px-1 py-0.5 text-[10px] font-semibold text-white">LOW</span>;
}

/**
 * Virtualized stock grid: only the rows in (and just around) the viewport are rendered, so scrolling through
 * tens of thousands of products stays smooth.
 * Desktop: "Sản phẩm" (sticky left) · Tổng · one column per warehouse (scrolls horizontally when there are many).
 * Mobile: one compact card per product (total on the right, warehouses as chips).
 * "Sản phẩm" / "Tổng" headers sort on the server (the page also offers a select, since mobile has no header row).
 */
export function StockTable({
  rows,
  warehouses,
  totalCount,
  loading,
  error,
  hasNextPage,
  fetchingNextPage,
  onLoadMore,
  onRetry,
  onEditThreshold,
  onClearFilters,
  sort,
  onSortChange,
}: StockTableProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const isMobile = useIsMobile();
  const rowHeight = isMobile ? MOBILE_ROW_HEIGHT : ROW_HEIGHT;
  const gridTemplateColumns = `minmax(16rem,1.6fr) 7.5rem repeat(${warehouses.length}, minmax(7rem,1fr))`;

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => rowHeight,
    overscan: 12,
  });
  useEffect(() => virtualizer.measure(), [rowHeight, virtualizer]);
  const items = virtualizer.getVirtualItems();
  const lastIndex = items.at(-1)?.index ?? -1;

  useEffect(() => {
    if (hasNextPage && !fetchingNextPage && lastIndex >= rows.length - PREFETCH_ROWS) onLoadMore();
  }, [lastIndex, rows.length, hasNextPage, fetchingNextPage, onLoadMore]);

  const productCell = (row: StockRowDto) => (
    <div className="min-w-0">
      <div className="truncate font-medium" title={row.name}>
        {row.name} <span className="font-normal text-muted-foreground">({row.unit})</span>
      </div>
      <div className="flex min-w-0 items-center gap-1.5 text-xs text-muted-foreground">
        <Link
          to={`/kardex?productId=${row.productId}`}
          className="shrink-0 font-mono text-primary hover:underline"
          title="Xem thẻ kho"
        >
          {row.sku}
        </Link>
        <span aria-hidden>·</span>
        <span className="truncate">{row.groupName}</span>
      </div>
    </div>
  );

  return (
    <div className="overflow-hidden rounded-md border bg-background">
      <div
        ref={scrollRef}
        className="relative h-[calc(100svh-19rem)] min-h-96 overflow-auto"
        role="table"
        aria-label="Tồn kho"
        aria-rowcount={totalCount + 1}
      >
        {!isMobile && (
          <div
            role="row"
            aria-rowindex={1}
            className="sticky top-0 z-20 grid min-w-max border-b bg-muted text-xs font-medium text-muted-foreground"
            style={{ gridTemplateColumns }}
          >
            <SortableHeader
              className="sticky left-0 z-10 bg-muted px-3 py-2.5"
              direction={sort === 'Sku' || sort === 'Name' ? 'ascending' : undefined}
              hint={sort === 'Name' ? 'tên' : sort === 'Sku' ? 'SKU' : undefined}
              next={sort === 'Sku' ? 'Name' : 'Sku'}
              onSort={onSortChange}
            >
              Sản phẩm
            </SortableHeader>
            <SortableHeader
              className="px-3 py-2.5 text-right"
              direction={sort === 'TotalDesc' ? 'descending' : sort === 'TotalAsc' ? 'ascending' : undefined}
              next={sort === 'TotalDesc' ? 'TotalAsc' : 'TotalDesc'}
              onSort={onSortChange}
            >
              Tổng
            </SortableHeader>
            {warehouses.map((w) => (
              <div key={w.id} role="columnheader" className="truncate px-3 py-2.5 text-right" title={w.name}>
                {w.code}
              </div>
            ))}
          </div>
        )}

        {loading && rows.length === 0 ? (
          <div className="space-y-2 p-3" aria-busy>
            {Array.from({ length: 10 }, (_, i) => (
              <Skeleton key={i} className="h-9 w-full" />
            ))}
          </div>
        ) : error && rows.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-16 text-sm text-muted-foreground">
            <TriangleAlert className="size-6" />
            Không tải được tồn kho
            <Button variant="outline" size="sm" onClick={onRetry}>
              Thử lại
            </Button>
          </div>
        ) : rows.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-16 text-sm text-muted-foreground">
            <PackageSearch className="size-8" />
            Không có sản phẩm phù hợp
            {onClearFilters && (
              <Button variant="outline" size="sm" onClick={onClearFilters}>
                Xóa bộ lọc
              </Button>
            )}
          </div>
        ) : (
          <div className="relative min-w-max" style={{ height: virtualizer.getTotalSize() }}>
            {items.map((item) => {
              const row = rows[item.index];
              const rowClass = cn(
                'group absolute inset-x-0 border-b text-sm',
                // opaque: the sticky product cell inherits it and must hide what scrolls underneath
                row.isLow ? 'bg-red-50 dark:bg-red-950' : 'bg-background',
              );
              if (isMobile) {
                return (
                  <div
                    key={row.productId}
                    role="row"
                    aria-rowindex={item.index + 2}
                    data-low={row.isLow || undefined}
                    className={cn(rowClass, 'flex flex-col justify-center gap-1.5 px-3')}
                    style={{ height: rowHeight, transform: `translateY(${item.start}px)` }}
                  >
                    <div className="flex items-start gap-2">
                      <div role="cell" className="min-w-0 flex-1">{productCell(row)}</div>
                      <div role="cell" className="shrink-0 text-right font-semibold tabular-nums">
                        {formatQty(row.total)} {row.isLow && <LowBadge />}
                      </div>
                    </div>
                    <div role="cell" className="flex gap-1 overflow-x-auto">
                      {warehouses.map((w) => (
                        <QuantityCell key={w.id} row={row} warehouse={w} onEditThreshold={onEditThreshold} compact />
                      ))}
                    </div>
                  </div>
                );
              }
              return (
                <div
                  key={row.productId}
                  role="row"
                  aria-rowindex={item.index + 2}
                  data-low={row.isLow || undefined}
                  className={cn(rowClass, 'grid items-center')}
                  style={{ gridTemplateColumns, height: rowHeight, transform: `translateY(${item.start}px)` }}
                >
                  {/* sticky: stays visible while scrolling across many warehouse columns */}
                  <div role="cell" className="sticky left-0 z-10 flex h-full items-center bg-inherit px-3 group-hover:bg-muted">
                    {productCell(row)}
                  </div>
                  <div role="cell" className="flex items-center justify-end gap-1.5 px-3 font-semibold tabular-nums">
                    {formatQty(row.total)}
                    {row.isLow && <LowBadge />}
                  </div>
                  {warehouses.map((w) => (
                    <div key={w.id} role="cell" className="px-1 text-right">
                      <QuantityCell row={row} warehouse={w} onEditThreshold={onEditThreshold} />
                    </div>
                  ))}
                </div>
              );
            })}
          </div>
        )}
      </div>
      <div className="flex items-center justify-between gap-2 border-t px-3 py-2 text-sm text-muted-foreground">
        <span>
          Đã tải {rows.length.toLocaleString('vi-VN')} / {totalCount.toLocaleString('vi-VN')} sản phẩm
        </span>
        {fetchingNextPage && (
          <span className="flex items-center gap-1.5">
            <Loader2 className="size-4 animate-spin" /> Đang tải thêm...
          </span>
        )}
      </div>
    </div>
  );
}
