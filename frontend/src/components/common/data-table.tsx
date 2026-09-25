import type { ReactNode } from 'react';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type OnChangeFn,
  type Row,
  type SortingState,
} from '@tanstack/react-table';
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronLeft, ChevronRight, RotateCw, TriangleAlert } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

/** Per-column presentation options (tanstack `meta`). */
declare module '@tanstack/react-table' {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData, TValue> {
    headerClassName?: string;
    cellClassName?: string;
  }
}

export interface DataTablePaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
  pageSizeOptions?: number[];
}

export interface DataTableProps<TData> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  columns: ColumnDef<TData, any>[];
  data: TData[];
  getRowId?: (row: TData) => string;
  /** true while (re)fetching — shows skeleton rows when there is no data yet, dims rows otherwise */
  loading?: boolean;
  /** server-side sorting (controlled) */
  sorting?: SortingState;
  onSortingChange?: (sorting: SortingState) => void;
  /** server-side pagination; omit to hide the footer */
  pagination?: DataTablePaginationProps;
  onRowClick?: (row: TData) => void;
  /** pointer enters / keyboard focus lands on a row (e.g. to prefetch its detail) */
  onRowHover?: (row: TData) => void;
  emptyText?: ReactNode;
  /** shown instead of the empty text when loading failed and there is no data to show */
  error?: ReactNode;
  onRetry?: () => void;
  className?: string;
  'aria-label'?: string;
}

/** Page numbers with ellipses, e.g. 1 … 4 5 6 … 12 */
export function pageWindow(page: number, pageCount: number): (number | 'ellipsis')[] {
  if (pageCount <= 7) return Array.from({ length: pageCount }, (_, i) => i + 1);
  const pages = new Set([1, pageCount, page - 1, page, page + 1]);
  const sorted = [...pages].filter((p) => p >= 1 && p <= pageCount).sort((a, b) => a - b);
  const out: (number | 'ellipsis')[] = [];
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) out.push('ellipsis');
    out.push(p);
  });
  return out;
}

export function DataTablePagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 20, 50, 100],
}: DataTablePaginationProps) {
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
  return (
    <div className="flex flex-col-reverse items-center justify-between gap-3 px-1 py-3 sm:flex-row">
      <div className="text-sm text-muted-foreground">Tổng: {totalCount}</div>
      <div className="flex flex-wrap items-center gap-3">
        {onPageSizeChange && (
          <div className="flex items-center gap-2 text-sm">
            <Select value={String(pageSize)} onValueChange={(v) => onPageSizeChange(Number(v))}>
              <SelectTrigger size="sm" aria-label="Số dòng mỗi trang" className="w-[4.5rem]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {pageSizeOptions.map((s) => (
                  <SelectItem key={s} value={String(s)}>
                    {s}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <span className="text-muted-foreground">/ trang</span>
          </div>
        )}
        <nav aria-label="Phân trang" className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label="Trang trước"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
          >
            <ChevronLeft />
          </Button>
          {pageWindow(page, pageCount).map((p, i) =>
            p === 'ellipsis' ? (
              <span key={`e${i}`} className="px-1 text-muted-foreground">
                …
              </span>
            ) : (
              <Button
                key={p}
                variant={p === page ? 'outline' : 'ghost'}
                size="icon-sm"
                aria-label={`Trang ${p}`}
                aria-current={p === page ? 'page' : undefined}
                onClick={() => p !== page && onPageChange(p)}
              >
                {p}
              </Button>
            ),
          )}
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label="Trang sau"
            disabled={page >= pageCount}
            onClick={() => onPageChange(page + 1)}
          >
            <ChevronRight />
          </Button>
        </nav>
      </div>
    </div>
  );
}

/**
 * Generic server-side data table (TanStack Table + shadcn Table).
 * Sorting and pagination are "manual": the parent owns the state (usually URL params) and fetches.
 */
export function DataTable<TData>({
  columns,
  data,
  getRowId,
  loading,
  sorting = [],
  onSortingChange,
  pagination,
  onRowClick,
  onRowHover,
  emptyText = 'Không có dữ liệu',
  error,
  onRetry,
  className,
  'aria-label': ariaLabel,
}: DataTableProps<TData>) {
  const handleSortingChange: OnChangeFn<SortingState> = (updater) => {
    const next = typeof updater === 'function' ? updater(sorting) : updater;
    onSortingChange?.(next);
  };

  const table = useReactTable({
    data,
    columns,
    getRowId: getRowId ? (row) => getRowId(row) : undefined,
    getCoreRowModel: getCoreRowModel(),
    manualSorting: true,
    manualPagination: true,
    state: { sorting },
    onSortingChange: handleSortingChange,
  });

  /**
   * Server-side sort: a column is sortable unless it sets `enableSorting: false`
   * (TanStack's getCanSort() needs an accessor, but our columns are display columns keyed by id).
   * Cycle: none → asc → desc → none.
   */
  const canSortColumn = (enableSorting: boolean | undefined) => !!onSortingChange && enableSorting !== false;
  const toggleSort = (id: string) => {
    const current = sorting.find((s) => s.id === id);
    onSortingChange?.(!current ? [{ id, desc: false }] : !current.desc ? [{ id, desc: true }] : []);
  };

  const rows = table.getRowModel().rows;
  const showSkeleton = loading && rows.length === 0;

  const handleRowKey = (e: React.KeyboardEvent, row: Row<TData>) => {
    if (onRowClick && (e.key === 'Enter' || e.key === ' ')) {
      e.preventDefault();
      onRowClick(row.original);
    }
  };

  return (
    <div className={cn('w-full', className)}>
      <div className="overflow-hidden rounded-lg border">
        <Table aria-label={ariaLabel} aria-busy={loading || undefined}>
          <TableHeader className="bg-muted/50">
            {table.getHeaderGroups().map((hg) => (
              <TableRow key={hg.id}>
                {hg.headers.map((header) => {
                  const canSort = canSortColumn(header.column.columnDef.enableSorting);
                  const sort = sorting.find((s) => s.id === header.column.id);
                  const sorted = sort ? (sort.desc ? 'desc' : 'asc') : false;
                  const content = header.isPlaceholder
                    ? null
                    : flexRender(header.column.columnDef.header, header.getContext());
                  return (
                    <TableHead
                      key={header.id}
                      className={header.column.columnDef.meta?.headerClassName}
                      aria-sort={sorted === 'asc' ? 'ascending' : sorted === 'desc' ? 'descending' : undefined}
                    >
                      {canSort ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="-ml-2 h-7 px-2"
                          onClick={() => toggleSort(header.column.id)}
                        >
                          {content}
                          {sorted === 'asc' ? (
                            <ArrowUp className="size-3.5" />
                          ) : sorted === 'desc' ? (
                            <ArrowDown className="size-3.5" />
                          ) : (
                            <ArrowUpDown className="size-3.5 opacity-40" />
                          )}
                        </Button>
                      ) : (
                        content
                      )}
                    </TableHead>
                  );
                })}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody className={cn(loading && rows.length > 0 && 'opacity-60 transition-opacity')}>
            {showSkeleton ? (
              Array.from({ length: 5 }, (_, i) => (
                <TableRow key={`sk${i}`}>
                  {columns.map((c, j) => (
                    <TableCell key={j} className={c.meta?.cellClassName}>
                      <Skeleton className="h-4 w-full" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : rows.length ? (
              rows.map((row) => (
                <TableRow
                  key={row.id}
                  className={cn(
                    onRowClick &&
                      'cursor-pointer outline-none focus-visible:bg-muted/50 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring',
                  )}
                  tabIndex={onRowClick ? 0 : undefined}
                  onClick={onRowClick ? () => onRowClick(row.original) : undefined}
                  onKeyDown={onRowClick ? (e) => handleRowKey(e, row) : undefined}
                  onMouseEnter={onRowHover ? () => onRowHover(row.original) : undefined}
                  onFocus={onRowHover ? () => onRowHover(row.original) : undefined}
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} className={cell.column.columnDef.meta?.cellClassName}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : error ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-24 text-center">
                  <div role="alert" className="flex flex-col items-center gap-2 text-destructive">
                    <span className="inline-flex items-center gap-1.5">
                      <TriangleAlert className="size-4" /> {error}
                    </span>
                    {onRetry && (
                      <Button variant="outline" size="sm" onClick={onRetry}>
                        <RotateCw /> Thử lại
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ) : (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                  {emptyText}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
      {pagination && <DataTablePagination {...pagination} />}
    </div>
  );
}
