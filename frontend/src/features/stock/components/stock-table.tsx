import { useEffect, useRef } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import { Loader2, TriangleAlert } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatQty } from '@/lib/format';
import type { StockRowDto, WarehouseDto } from '@/types';
import type { ThresholdTarget } from './threshold-dialog';

export const ROW_HEIGHT = 40;
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
}

/**
 * Virtualized stock grid: only the rows in (and just around) the viewport are rendered, so scrolling through
 * tens of thousands of products stays smooth. Warehouse columns are dynamic (one per warehouse shown).
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
}: StockTableProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const gridTemplateColumns = `7.5rem minmax(14rem,1fr) 10rem repeat(${warehouses.length}, 7.5rem) 7.5rem`;

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 12,
  });
  const items = virtualizer.getVirtualItems();
  const lastIndex = items.at(-1)?.index ?? -1;

  useEffect(() => {
    if (hasNextPage && !fetchingNextPage && lastIndex >= rows.length - PREFETCH_ROWS) onLoadMore();
  }, [lastIndex, rows.length, hasNextPage, fetchingNextPage, onLoadMore]);

  const cellFor = (row: StockRowDto, warehouseId: number) => row.warehouses.find((w) => w.warehouseId === warehouseId);

  return (
    <div className="overflow-hidden rounded-md border bg-background">
      <div
        ref={scrollRef}
        className="relative h-[calc(100svh-18rem)] min-h-80 overflow-auto"
        role="table"
        aria-label="Tồn kho"
        aria-rowcount={totalCount + 1}
      >
        <div
          role="row"
          aria-rowindex={1}
          className="sticky top-0 z-10 grid min-w-max border-b bg-muted text-xs font-medium text-muted-foreground"
          style={{ gridTemplateColumns }}
        >
          <div role="columnheader" className="px-3 py-2.5">SKU</div>
          <div role="columnheader" className="px-3 py-2.5">Tên sản phẩm</div>
          <div role="columnheader" className="px-3 py-2.5">Nhóm</div>
          {warehouses.map((w) => (
            <div key={w.id} role="columnheader" className="truncate px-3 py-2.5 text-right" title={w.name}>
              {w.code}
            </div>
          ))}
          <div role="columnheader" className="px-3 py-2.5 text-right">Tổng</div>
        </div>

        {loading && rows.length === 0 ? (
          <div className="space-y-2 p-3" aria-busy>
            {Array.from({ length: 10 }, (_, i) => (
              <Skeleton key={i} className="h-7 w-full" />
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
          <div className="py-16 text-center text-sm text-muted-foreground">Không có sản phẩm phù hợp</div>
        ) : (
          <div className="relative min-w-max" style={{ height: virtualizer.getTotalSize() }}>
            {items.map((item) => {
              const row = rows[item.index];
              return (
                <div
                  key={row.productId}
                  role="row"
                  aria-rowindex={item.index + 2}
                  data-low={row.isLow || undefined}
                  className={cn(
                    'absolute inset-x-0 grid items-center border-b text-sm hover:bg-muted/50',
                    row.isLow && 'bg-red-50/60 dark:bg-red-950/20',
                  )}
                  style={{ gridTemplateColumns, height: ROW_HEIGHT, transform: `translateY(${item.start}px)` }}
                >
                  <div role="cell" className="truncate px-3 font-mono text-xs">{row.sku}</div>
                  <div role="cell" className="truncate px-3 font-medium" title={row.name}>
                    {row.name} <span className="font-normal text-muted-foreground">({row.unit})</span>
                  </div>
                  <div role="cell" className="truncate px-3 text-muted-foreground">{row.groupName}</div>
                  {warehouses.map((w) => {
                    const cell = cellFor(row, w.id);
                    const content = (
                      <>
                        <span className={cn('tabular-nums', cell?.isLow && 'font-semibold text-red-600 dark:text-red-400')}>
                          {cell ? formatQty(cell.quantity) : '—'}
                        </span>
                        {cell && cell.minThreshold > 0 && (
                          <span className="ml-1 text-[11px] text-muted-foreground">/{formatQty(cell.minThreshold)}</span>
                        )}
                      </>
                    );
                    return (
                      <div key={w.id} role="cell" className="px-1 text-right">
                        {onEditThreshold ? (
                          <button
                            type="button"
                            className="w-full rounded px-2 py-1 text-right hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                            title="Đặt ngưỡng tồn tối thiểu"
                            aria-label={`${row.sku} tại ${w.code}: ${cell ? formatQty(cell.quantity) : 0}${cell?.isLow ? ', dưới ngưỡng' : ''}. Đặt ngưỡng`}
                            onClick={() =>
                              onEditThreshold({
                                productId: row.productId,
                                sku: row.sku,
                                productName: row.name,
                                warehouseId: w.id,
                                warehouseName: w.name,
                                quantity: cell?.quantity ?? 0,
                                minThreshold: cell?.minThreshold ?? 0,
                              })
                            }
                          >
                            {content}
                          </button>
                        ) : (
                          <span className="px-2">{content}</span>
                        )}
                      </div>
                    );
                  })}
                  <div role="cell" className="px-3 text-right font-medium tabular-nums">
                    {formatQty(row.total)}
                    {row.isLow && <span className="ml-1.5 rounded bg-red-600 px-1 py-0.5 text-[10px] font-semibold text-white">LOW</span>}
                  </div>
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
