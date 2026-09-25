import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ChartContainer, ChartLegend, ChartLegendContent, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart';
import { Skeleton } from '@/components/ui/skeleton';
import { dayjs, formatDate } from '@/lib/date';
import { formatMoney, formatQty } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { DashboardDto } from '@/types';

/** 2,4 tỷ · 350 tr · 12 N — compact VND for KPI cards and chart axes. */
export function formatCompactMoney(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 1e9) return `${(value / 1e9).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tỷ`;
  if (abs >= 1e6) return `${(value / 1e6).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tr`;
  if (abs >= 1e3) return `${(value / 1e3).toLocaleString('vi-VN', { maximumFractionDigits: 0 })} N`;
  return value.toLocaleString('vi-VN');
}

export function Kpi({
  title,
  value,
  hint,
  icon,
  loading,
  to,
  tone,
}: {
  title: string;
  value: ReactNode;
  hint?: ReactNode;
  icon: ReactNode;
  loading: boolean;
  to?: string;
  tone?: 'danger' | 'warning';
}) {
  const card = (
    <Card className={cn('h-full', to && 'transition-shadow group-hover:shadow-md')}>
      <CardContent className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="text-sm text-muted-foreground">{title}</div>
          {loading ? (
            <Skeleton className="h-7 w-24" />
          ) : (
            <div
              className={cn(
                'truncate text-2xl font-semibold tabular-nums',
                tone === 'danger' && 'text-red-600 dark:text-red-400',
                tone === 'warning' && 'text-amber-600 dark:text-amber-400',
              )}
            >
              {value}
            </div>
          )}
          {hint && <div className="truncate text-xs text-muted-foreground">{hint}</div>}
        </div>
        <div className="rounded-lg bg-muted p-2 text-muted-foreground [&_svg]:size-5">{icon}</div>
      </CardContent>
    </Card>
  );
  if (!to) return card;
  return (
    <Link to={to} className="group block rounded-xl outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
      {card}
    </Link>
  );
}

const inOutConfig = {
  inValue: { label: 'Nhập', color: 'var(--chart-2)' },
  outValue: { label: 'Xuất', color: 'var(--chart-1)' },
} satisfies ChartConfig;

export function InOutChart({ data, loading }: { data: DashboardDto['inOutByDay'] | undefined; loading: boolean }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Nhập – Xuất 30 ngày</CardTitle>
        <CardDescription>Giá trị theo giá vốn, theo ngày</CardDescription>
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-72 w-full" />
        ) : (
          <ChartContainer config={inOutConfig} className="aspect-auto h-72 w-full">
            <BarChart data={data ?? []} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
              <CartesianGrid vertical={false} />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={false}
                minTickGap={20}
                tickFormatter={(d: string) => dayjs(d).format('DD/MM')}
              />
              <YAxis tickLine={false} axisLine={false} width={56} tickFormatter={(v: number) => formatCompactMoney(v)} />
              <ChartTooltip
                content={
                  <ChartTooltipContent
                    labelFormatter={(d) => dayjs(String(d)).format('DD/MM/YYYY')}
                    formatter={(value, name) => (
                      <span className="flex w-full justify-between gap-4">
                        <span className="text-muted-foreground">{inOutConfig[name as keyof typeof inOutConfig]?.label}</span>
                        <span className="font-mono tabular-nums">{formatMoney(Number(value))} đ</span>
                      </span>
                    )}
                  />
                }
              />
              <ChartLegend content={<ChartLegendContent />} />
              <Bar dataKey="inValue" fill="var(--color-inValue)" radius={[3, 3, 0, 0]} />
              <Bar dataKey="outValue" fill="var(--color-outValue)" radius={[3, 3, 0, 0]} />
            </BarChart>
          </ChartContainer>
        )}
      </CardContent>
    </Card>
  );
}

/** Horizontal share bars (value by warehouse / group) — plain CSS, no chart library needed. */
export function ValueBreakdown({
  title,
  rows,
  loading,
}: {
  title: string;
  rows: { key: string; label: string; value: number }[] | undefined;
  loading: boolean;
}) {
  const max = Math.max(1, ...(rows ?? []).map((r) => r.value));
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {loading
          ? Array.from({ length: 3 }, (_, i) => <Skeleton key={i} className="h-8 w-full" />)
          : (rows ?? []).map((r) => (
              <div key={r.key} className="space-y-1">
                <div className="flex justify-between gap-2 text-sm">
                  <span className="truncate">{r.label}</span>
                  <span className="shrink-0 font-medium tabular-nums">{formatCompactMoney(r.value)}</span>
                </div>
                <div className="h-2 rounded-full bg-muted">
                  <div className="h-2 rounded-full bg-primary" style={{ width: `${(r.value / max) * 100}%` }} />
                </div>
              </div>
            ))}
        {!loading && !rows?.length && <p className="text-sm text-muted-foreground">Chưa có tồn kho</p>}
      </CardContent>
    </Card>
  );
}

/** Two lines: name (truncated, full text on hover), then SKU · extra in small print. */
function ItemName({ sku, name, extra }: { sku: string; name: string; extra?: string }) {
  return (
    <span className="block min-w-0">
      <span className="block truncate font-medium" title={name}>
        {name}
      </span>
      <span className="block truncate text-xs text-muted-foreground">
        <span className="font-mono">{sku}</span>
        {extra && <> · {extra}</>}
      </span>
    </span>
  );
}

export function LowStockList({ rows, loading }: { rows: DashboardDto['topLowStock'] | undefined; loading: boolean }) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2">
        <CardTitle>Hàng sắp hết</CardTitle>
        <Link to="/stock?belowThreshold=true" className="text-sm text-primary hover:underline">
          Xem tất cả
        </Link>
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-40 w-full" />
        ) : rows?.length ? (
          <ul className="divide-y">
            {rows.map((r) => (
              <li key={`${r.productId}-${r.warehouseId}`} className="flex items-center gap-3 py-2 text-sm">
                <Link to={`/kardex?productId=${r.productId}&warehouseId=${r.warehouseId}`} className="min-w-0 flex-1 hover:underline">
                  <ItemName sku={r.sku} name={r.name} />
                </Link>
                <Badge variant="outline">{r.warehouseCode}</Badge>
                <span className="w-24 shrink-0 text-right tabular-nums">
                  <span className="font-semibold text-red-600 dark:text-red-400">{formatQty(r.quantity)}</span>
                  <span className="text-muted-foreground">/{formatQty(r.minThreshold)}</span>
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">Không có mã nào dưới ngưỡng 🎉</p>
        )}
      </CardContent>
    </Card>
  );
}

export function SlowMovingList({
  rows,
  days,
  loading,
}: {
  rows: DashboardDto['slowMoving'] | undefined;
  days: number | undefined;
  loading: boolean;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Hàng chậm luân chuyển</CardTitle>
        <CardDescription>Còn tồn nhưng không xuất trong {days ?? 30} ngày — giá trị tồn lớn nhất</CardDescription>
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-40 w-full" />
        ) : rows?.length ? (
          <ul className="divide-y">
            {rows.map((r) => (
              <li key={r.productId} className="flex items-center gap-3 py-2 text-sm">
                <Link to={`/kardex?productId=${r.productId}`} className="min-w-0 flex-1 hover:underline">
                  <ItemName sku={r.sku} name={r.name} extra={r.lastOutAt ? `xuất lần cuối ${formatDate(r.lastOutAt)}` : 'chưa từng xuất'} />
                </Link>
                <span className="w-20 shrink-0 text-right font-medium tabular-nums">{formatCompactMoney(r.value)}</span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">Không có</p>
        )}
      </CardContent>
    </Card>
  );
}
