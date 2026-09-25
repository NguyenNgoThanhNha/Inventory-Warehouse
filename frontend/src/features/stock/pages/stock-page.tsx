import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowDownToLine, ArrowUpFromLine, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ExportButton } from '@/components/common/export-button';
import { PageHeader } from '@/components/common/page-header';
import { SearchInput } from '@/components/common/search-input';
import { useProductGroups, useWarehouses } from '@/features/catalog';
import { oneOf, toPositiveInt, useUrlParams } from '@/lib/hooks/use-url-params';
import { useCan } from '@/stores/auth-store';
import type { StockSort } from '@/types';
import { STOCK_SORT_LABEL, StockTable } from '../components/stock-table';
import { ThresholdDialog, type ThresholdTarget } from '../components/threshold-dialog';
import { useInfiniteStock } from '../hooks/use-stock';

const ALL = 'all';
const SORTS = Object.keys(STOCK_SORT_LABEL) as StockSort[];

/** Màn Tồn kho (spec §4.1): filter trên URL, bảng virtualized, tải trang kế tiếp khi cuộn gần cuối. */
export function StockPage() {
  const [params, update] = useUrlParams();
  const warehouseId = toPositiveInt(params.get('warehouseId'));
  const groupId = toPositiveInt(params.get('groupId'));
  const search = params.get('search') ?? '';
  const belowThreshold = params.get('belowThreshold') === 'true';
  const sort = oneOf(SORTS, params.get('sort')) ?? 'Sku';

  const { data: warehouses = [] } = useWarehouses();
  const { data: groups = [] } = useProductGroups();
  const query = useMemo(
    () => ({
      warehouseId,
      groupId,
      search: search || undefined,
      belowThreshold: belowThreshold || undefined,
      sort: sort === 'Sku' ? undefined : sort,
    }),
    [warehouseId, groupId, search, belowThreshold, sort],
  );
  const stock = useInfiniteStock(query);
  const rows = useMemo(() => stock.data?.pages.flatMap((p) => p.items) ?? [], [stock.data]);
  const totalCount = stock.data?.pages[0]?.totalCount ?? 0;
  const shownWarehouses = warehouseId ? warehouses.filter((w) => w.id === warehouseId) : warehouses;

  const canEditThreshold = useCan('WAREHOUSE', 'U');
  const canReceive = useCan('GOODS_RECEIPT', 'C');
  const canIssue = useCan('GOODS_ISSUE', 'C');
  const canExport = useCan('IMPORT_EXPORT', 'R');
  const [threshold, setThreshold] = useState<ThresholdTarget | null>(null);
  const hasFilters = !!(warehouseId || groupId || search || belowThreshold);
  const clearFilters = () => update({ warehouseId: undefined, groupId: undefined, search: undefined, belowThreshold: undefined });
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = stock;
  const loadMore = useCallback(() => void fetchNextPage(), [fetchNextPage]);
  const setSort = useCallback((s: StockSort) => update({ sort: s === 'Sku' ? undefined : s }), [update]);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Tồn kho"
        description={canEditThreshold ? 'Tồn hiện tại theo từng kho. Bấm vào ô số lượng để đặt ngưỡng tồn tối thiểu.' : 'Tồn hiện tại theo từng kho.'}
        actions={
          <>
            {canExport && <ExportButton url="/exports/stock" params={query} fallbackName="ton-kho.xlsx" />}
            {canReceive && (
              <Button variant="outline" asChild>
                <Link to="/goods-receipts/new">
                  <ArrowDownToLine /> Phiếu nhập
                </Link>
              </Button>
            )}
            {canIssue && (
              <Button asChild>
                <Link to="/goods-issues/new">
                  <ArrowUpFromLine /> Phiếu xuất
                </Link>
              </Button>
            )}
          </>
        }
      />

      <div className="grid grid-cols-2 items-center gap-2 sm:flex sm:flex-wrap">
        <Select value={warehouseId ? String(warehouseId) : ALL} onValueChange={(v) => update({ warehouseId: v === ALL ? undefined : v })}>
          <SelectTrigger aria-label="Kho" className="w-full sm:w-52">
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
        <Select value={groupId ? String(groupId) : ALL} onValueChange={(v) => update({ groupId: v === ALL ? undefined : v })}>
          <SelectTrigger aria-label="Nhóm hàng" className="w-full sm:w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tất cả nhóm</SelectItem>
            {groups.map((g) => (
              <SelectItem key={g.id} value={String(g.id)}>
                {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <SearchInput
          className="col-span-2"
          aria-label="Tìm SKU hoặc tên"
          placeholder="SKU / tên..."
          value={search}
          onChange={(v) => update({ search: v })}
        />
        <Select value={sort} onValueChange={(v) => setSort(v as StockSort)}>
          <SelectTrigger aria-label="Sắp xếp" className="w-full sm:w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {SORTS.map((s) => (
              <SelectItem key={s} value={s}>
                {STOCK_SORT_LABEL[s]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-2">
          <Checkbox
            id="below-threshold"
            checked={belowThreshold}
            onCheckedChange={(v) => update({ belowThreshold: v === true ? 'true' : undefined })}
          />
          <Label htmlFor="below-threshold">Chỉ hàng sắp hết</Label>
        </div>
        {hasFilters && (
          <Button variant="ghost" size="sm" className="justify-self-end" onClick={clearFilters}>
            <X /> Xóa lọc
          </Button>
        )}
      </div>

      <StockTable
        rows={rows}
        warehouses={shownWarehouses}
        totalCount={totalCount}
        loading={stock.isPending}
        error={stock.isError}
        hasNextPage={!!hasNextPage}
        fetchingNextPage={isFetchingNextPage}
        onLoadMore={loadMore}
        onRetry={() => void stock.refetch()}
        onEditThreshold={canEditThreshold ? setThreshold : undefined}
        onClearFilters={hasFilters ? clearFilters : undefined}
        sort={sort}
        onSortChange={setSort}
      />
      <ThresholdDialog target={threshold} onClose={() => setThreshold(null)} />
    </div>
  );
}
