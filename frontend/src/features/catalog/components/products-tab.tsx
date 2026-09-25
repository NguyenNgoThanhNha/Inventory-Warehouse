import { useState } from 'react';
import type { ColumnDef } from '@tanstack/react-table';
import { Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { DataTable } from '@/components/common/data-table';
import { useDebouncedCallback } from '@/lib/hooks/use-debounced-callback';
import { formatMoney } from '@/lib/format';
import { useCan } from '@/stores/auth-store';
import type { ProductDto } from '@/types';
import { useDeleteProduct, useProductGroups, useProducts } from '../hooks/use-catalog';
import { ProductDialog } from './product-dialog';

const ALL = 'all';

export function ProductsTab() {
  const [search, setSearch] = useState('');
  const [groupId, setGroupId] = useState<number | undefined>();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const debounced = useDebouncedCallback((v: string) => {
    setSearch(v);
    setPage(1);
  });

  const { data, isFetching, isError, refetch } = useProducts({ search, groupId, page, pageSize });
  const { data: groups = [] } = useProductGroups();
  const remove = useDeleteProduct();
  const canCreate = useCan('PRODUCT', 'C');
  const canUpdate = useCan('PRODUCT', 'U');
  const canDelete = useCan('PRODUCT', 'D');
  const [dialog, setDialog] = useState<{ open: boolean; product: ProductDto | null }>({ open: false, product: null });
  const [deleting, setDeleting] = useState<ProductDto | null>(null);

  const columns: ColumnDef<ProductDto>[] = [
    { id: 'sku', header: 'SKU', cell: ({ row }) => <span className="font-mono text-xs">{row.original.sku}</span> },
    {
      id: 'name',
      header: 'Tên',
      cell: ({ row }) => (
        <span className="flex items-center gap-2 font-medium">
          {row.original.name}
          {!row.original.isActive && <Badge variant="secondary">Ngừng KD</Badge>}
        </span>
      ),
    },
    { id: 'unit', header: 'ĐVT', cell: ({ row }) => row.original.unit },
    { id: 'group', header: 'Nhóm', cell: ({ row }) => row.original.groupName },
    {
      id: 'cost',
      header: 'Giá vốn',
      meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
      cell: ({ row }) => formatMoney(row.original.cost),
    },
    {
      id: 'price',
      header: 'Giá bán',
      meta: { headerClassName: 'text-right', cellClassName: 'text-right tabular-nums' },
      cell: ({ row }) => formatMoney(row.original.price),
    },
    {
      id: 'actions',
      header: '',
      meta: { cellClassName: 'text-right whitespace-nowrap' },
      cell: ({ row }) => (
        <div className="flex justify-end gap-1">
          {canUpdate && (
            <Button variant="ghost" size="icon-sm" aria-label={`Sửa ${row.original.sku}`} onClick={() => setDialog({ open: true, product: row.original })}>
              <Pencil />
            </Button>
          )}
          {canDelete && (
            <Button variant="ghost" size="icon-sm" aria-label={`Xóa ${row.original.sku}`} onClick={() => setDeleting(row.original)}>
              <Trash2 />
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full max-w-xs">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input aria-label="Tìm sản phẩm" placeholder="SKU hoặc tên..." className="pl-8" onChange={(e) => debounced.run(e.target.value)} />
        </div>
        <Select
          value={groupId ? String(groupId) : ALL}
          onValueChange={(v) => {
            setGroupId(v === ALL ? undefined : Number(v));
            setPage(1);
          }}
        >
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
        {canCreate && (
          <Button className="ml-auto" onClick={() => setDialog({ open: true, product: null })}>
            <Plus /> Thêm sản phẩm
          </Button>
        )}
      </div>
      <DataTable
        aria-label="Sản phẩm"
        columns={columns}
        data={data?.items ?? []}
        getRowId={(p) => String(p.id)}
        loading={isFetching}
        error={isError ? 'Không tải được danh sách sản phẩm' : undefined}
        onRetry={() => void refetch()}
        pagination={{
          page,
          pageSize,
          totalCount: data?.totalCount ?? 0,
          onPageChange: setPage,
          onPageSizeChange: (s) => {
            setPageSize(s);
            setPage(1);
          },
        }}
      />
      <ProductDialog product={dialog.product} open={dialog.open} onOpenChange={(open) => setDialog((d) => ({ ...d, open }))} />
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Xóa sản phẩm ${deleting?.sku}?`}
        description="Chỉ xóa được sản phẩm chưa phát sinh chứng từ. Sản phẩm đã có phiếu thì hãy chuyển sang ngừng kinh doanh."
        confirmText="Xóa"
        destructive
        onConfirm={() =>
          remove.mutateAsync(deleting!.id).then(() => {
            toast.success(`Đã xóa ${deleting!.sku}`);
          })
        }
      />
    </div>
  );
}
