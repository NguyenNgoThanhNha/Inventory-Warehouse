import { Boxes, CircleDollarSign, FileClock, RefreshCw, Send, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { PageHeader } from '@/components/common/page-header';
import { useWarehouses } from '@/features/catalog';
import { dayjs } from '@/lib/date';
import { formatMoney } from '@/lib/format';
import { toPositiveInt, useUrlParams } from '@/lib/hooks/use-url-params';
import { InOutChart, Kpi, LowStockList, SlowMovingList, ValueBreakdown, formatCompactMoney } from '../components/dashboard-widgets';
import { useDashboard } from '../hooks/use-reports';

const ALL = 'all';

/** Tổng quan kho (spec §4.4) — toàn hệ thống hoặc một kho. */
export function DashboardPage() {
  const [params, update] = useUrlParams();
  const warehouseId = toPositiveInt(params.get('warehouseId'));
  const { data: warehouses = [] } = useWarehouses();
  const { data, isPending, isFetching, refetch } = useDashboard(warehouseId);
  const loading = isPending;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Tổng quan kho"
        description={data ? `Số liệu lúc ${dayjs(data.generatedAt).format('HH:mm:ss DD/MM')}` : undefined}
        actions={
          <>
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
            <Button variant="outline" size="icon" aria-label="Làm mới" disabled={isFetching} onClick={() => void refetch()}>
              <RefreshCw className={isFetching ? 'animate-spin' : undefined} />
            </Button>
          </>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <Kpi
          title="Giá trị tồn"
          value={data ? `${formatCompactMoney(data.stockValue)}` : '—'}
          hint={data && `${formatMoney(data.stockValue)} đ`}
          icon={<CircleDollarSign />}
          loading={loading}
        />
        <Kpi title="Mã còn hàng" value={data?.productsInStock.toLocaleString('vi-VN') ?? '—'} icon={<Boxes />} loading={loading} to="/stock" />
        <Kpi
          title="Dòng tồn dưới ngưỡng"
          value={data?.lowStockCount.toLocaleString('vi-VN') ?? '—'}
          icon={<TriangleAlert />}
          loading={loading}
          tone={data?.lowStockCount ? 'danger' : undefined}
          to={`/stock?belowThreshold=true${warehouseId ? `&warehouseId=${warehouseId}` : ''}`}
        />
        <Kpi
          title="Phiếu nháp chờ duyệt"
          value={data?.draftDocuments ?? '—'}
          icon={<FileClock />}
          loading={loading}
          tone={data?.draftDocuments ? 'warning' : undefined}
          to="/goods-issues?status=Draft"
        />
        <Kpi title="Phiếu ghi sổ hôm nay" value={data?.postedToday ?? '—'} icon={<Send />} loading={loading} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <InOutChart data={data?.inOutByDay} loading={loading} />
        </div>
        <ValueBreakdown
          title="Giá trị tồn theo kho"
          loading={loading}
          rows={data?.valueByWarehouse.map((w) => ({ key: String(w.warehouseId), label: w.name, value: w.value }))}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <LowStockList rows={data?.topLowStock} loading={loading} />
        <SlowMovingList rows={data?.slowMoving} days={data?.slowMovingDays} loading={loading} />
        <ValueBreakdown
          title="Giá trị tồn theo nhóm hàng"
          loading={loading}
          rows={data?.valueByGroup.map((g) => ({ key: String(g.groupId), label: g.name, value: g.value }))}
        />
      </div>
    </div>
  );
}
