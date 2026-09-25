import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { ColumnDef } from '@tanstack/react-table';
import { Loader2, Pencil, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Form } from '@/components/ui/form';
import { DataTable } from '@/components/common/data-table';
import { SwitchFormField, TextFormField } from '@/components/common/form-fields';
import { applyFieldErrors, showError } from '@/lib/api-errors';
import { useCan } from '@/stores/auth-store';
import type { WarehouseDto } from '@/types';
import { useSaveWarehouse, useWarehouses } from '../hooks/use-catalog';
import { warehouseSchema, type WarehouseForm, type WarehouseFormInput } from '../schemas';

function WarehouseDialog({
  warehouse,
  open,
  onOpenChange,
}: {
  warehouse: WarehouseDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const save = useSaveWarehouse(warehouse?.id ?? null);
  const form = useForm<WarehouseFormInput, unknown, WarehouseForm>({ resolver: zodResolver(warehouseSchema) });

  useEffect(() => {
    if (open)
      form.reset({
        code: warehouse?.code ?? '',
        name: warehouse?.name ?? '',
        address: warehouse?.address ?? '',
        isActive: warehouse?.isActive ?? true,
      });
  }, [open, warehouse, form]);

  const onSubmit = form.handleSubmit((v) =>
    save.mutate(v, {
      onSuccess: (w) => {
        toast.success(warehouse ? `Đã cập nhật ${w.code}` : `Đã thêm kho ${w.code}`);
        onOpenChange(false);
      },
      onError: (err) => {
        if (!applyFieldErrors(err, ['code', 'name', 'address'] as const, form.setError)) showError(err);
      },
    }),
  );

  return (
    <Dialog open={open} onOpenChange={(o) => !save.isPending && onOpenChange(o)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{warehouse ? `Sửa kho ${warehouse.code}` : 'Thêm kho'}</DialogTitle>
          <DialogDescription>Kho còn hàng không thể ngừng hoạt động — hãy chuyển hết hàng sang kho khác trước.</DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form id="warehouse-form" onSubmit={onSubmit} noValidate className="space-y-4">
            <TextFormField control={form.control} name="code" label="Mã kho" required disabled={!!warehouse} />
            <TextFormField control={form.control} name="name" label="Tên kho" required />
            <TextFormField control={form.control} name="address" label="Địa chỉ" />
            {warehouse && <SwitchFormField control={form.control} name="isActive" label="Đang hoạt động" />}
          </form>
        </Form>
        <DialogFooter>
          <Button variant="outline" disabled={save.isPending} onClick={() => onOpenChange(false)}>
            Hủy
          </Button>
          <Button type="submit" form="warehouse-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            Lưu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function WarehousesTab() {
  const { data, isLoading } = useWarehouses(true);
  const canCreate = useCan('WAREHOUSE', 'C');
  const canUpdate = useCan('WAREHOUSE', 'U');
  const [dialog, setDialog] = useState<{ open: boolean; warehouse: WarehouseDto | null }>({ open: false, warehouse: null });

  const columns: ColumnDef<WarehouseDto>[] = [
    { id: 'code', header: 'Mã', cell: ({ row }) => <span className="font-mono text-xs">{row.original.code}</span> },
    {
      id: 'name',
      header: 'Tên kho',
      cell: ({ row }) => (
        <span className="flex items-center gap-2 font-medium">
          {row.original.name}
          {!row.original.isActive && <Badge variant="secondary">Ngừng hoạt động</Badge>}
        </span>
      ),
    },
    { id: 'address', header: 'Địa chỉ', cell: ({ row }) => row.original.address ?? '—' },
    {
      id: 'actions',
      header: '',
      meta: { cellClassName: 'text-right' },
      cell: ({ row }) =>
        canUpdate && (
          <Button variant="ghost" size="icon-sm" aria-label={`Sửa ${row.original.code}`} onClick={() => setDialog({ open: true, warehouse: row.original })}>
            <Pencil />
          </Button>
        ),
    },
  ];

  return (
    <div className="space-y-4">
      {canCreate && (
        <div className="flex justify-end">
          <Button onClick={() => setDialog({ open: true, warehouse: null })}>
            <Plus /> Thêm kho
          </Button>
        </div>
      )}
      <DataTable aria-label="Kho" columns={columns} data={data ?? []} getRowId={(w) => String(w.id)} loading={isLoading} />
      <WarehouseDialog warehouse={dialog.warehouse} open={dialog.open} onOpenChange={(open) => setDialog((d) => ({ ...d, open }))} />
    </div>
  );
}
