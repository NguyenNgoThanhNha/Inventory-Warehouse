import type { ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/common/page-header';
import { canAny, type PermissionRequirement } from '@/lib/permissions';
import { useAuthStore } from '@/stores/auth-store';
import { GroupsTab } from '../components/groups-tab';
import { ProductsTab } from '../components/products-tab';
import { SuppliersTab } from '../components/suppliers-tab';
import { WarehousesTab } from '../components/warehouses-tab';

const TABS: { key: string; label: string; anyOf: readonly PermissionRequirement[]; render: () => ReactNode }[] = [
  { key: 'products', label: 'Sản phẩm', anyOf: [['PRODUCT', 'R']], render: () => <ProductsTab /> },
  { key: 'groups', label: 'Nhóm hàng', anyOf: [['PRODUCT', 'R']], render: () => <GroupsTab /> },
  { key: 'warehouses', label: 'Kho', anyOf: [['WAREHOUSE', 'R']], render: () => <WarehousesTab /> },
  { key: 'suppliers', label: 'Nhà cung cấp', anyOf: [['SUPPLIER', 'R']], render: () => <SuppliersTab /> },
];

export function CatalogPage() {
  const user = useAuthStore((s) => s.user);
  const [params, setParams] = useSearchParams();
  const visible = TABS.filter((t) => canAny(user, t.anyOf));
  const active = visible.find((t) => t.key === params.get('tab'))?.key ?? visible[0]?.key;

  return (
    <div className="space-y-4">
      <PageHeader title="Danh mục" description="Sản phẩm, nhóm hàng, kho và nhà cung cấp" />
      <Tabs value={active} onValueChange={(key) => setParams({ tab: key }, { replace: true })}>
        <TabsList className="max-w-full overflow-x-auto">
          {visible.map((t) => (
            <TabsTrigger key={t.key} value={t.key}>
              {t.label}
            </TabsTrigger>
          ))}
        </TabsList>
        {visible.map((t) => (
          <TabsContent key={t.key} value={t.key}>
            <Card>
              <CardContent>{t.render()}</CardContent>
            </Card>
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
