import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowDownToLine, ArrowUpFromLine, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ExportButton } from '@/components/common/export-button';
import { PageHeader } from '@/components/common/page-header';
import { useProductGroups, useWarehouses } from '@/features/catalog';
import { useDebouncedCallback } from '@/lib/hooks/use-debounced-callback';
import { toPositiveInt, useUrlParams } from '@/lib/hooks/use-url-params';
import { useCan } from '@/stores/auth-store';
import { StockTable } from '../components/stock-table';
import { ThresholdDialog, type ThresholdTarget } from '../components/threshold-dialog';
import { useInfiniteStock } from '../hooks/use-stock';

const ALL = 'all';

/** Màn Tồn kho (spec §4.1): filter trên URL, bảng virtualized, tải trang kế tiếp khi cuộn gần cuối. */
export function StockPage() {
  const [params, update] = useUrlParams();
  const warehouseId = toPositiveInt(params.get('warehouseId'));
  const groupId = toPositiveInt(params.get('groupId'));
  const search = params.get('search') ?? '';
  const belowThreshold = params.get('belowThreshold') === 'true';

  const { data: warehouses = [] } = useWarehouses();
  const { data: groups = [] } = useProductGroups();
  const query = useMemo(
    () => ({ warehouseId, groupId, search: search || undefined, belowThreshold: belowThreshold || undefined }),
    [warehouseId, groupId, search, belowThreshold],
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
  const debouncedSearch = useDebouncedCallback((v: string) => update({ search: v.trim() }));
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = stock;
  const loadMore = useCallback(() => void fetchNextPage(), [fetchNextPage]);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Tồn kho"
        description="Tồn hiện tại theo từng kho. Bấm vào ô số lượng để đặt ngưỡng tồn tối thiểu."
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

      <div className="flex flex-wrap items-center gap-2">
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
        <Select value={groupId ? String(groupId) : ALL} onValueChange={(v) => update({ groupId: v === ALL ? undefined : v })}>
          <SelectTrigger aria-label="Nhóm hàng" className="w-48">
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
        <div className="relative w-full max-w-xs">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            key={search /* reset when the URL changes from outside (e.g. global search) */}
            aria-label="Tìm SKU hoặc tên"
            placeholder="SKU / tên..."
            className="pl-8"
            defaultValue={search}
            onChange={(e) => debouncedSearch.run(e.target.value)}
          />
        </div>
        <div className="flex items-center gap-2">
          <Checkbox
            id="below-threshold"
            checked={belowThreshold}
            onCheckedChange={(v) => update({ belowThreshold: v === true ? 'true' : undefined })}
          />
          <Label htmlFor="below-threshold">Chỉ hàng sắp hết</Label>
        </div>
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
      />
      <ThresholdDialog target={threshold} onClose={() => setThreshold(null)} />
    </div>
  );
}
