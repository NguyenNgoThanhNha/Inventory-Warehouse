import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { ColumnDef } from '@tanstack/react-table';
import { Loader2, Pencil, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Form } from '@/components/ui/form';
import { DataTable } from '@/components/common/data-table';
import { SearchInput } from '@/components/common/search-input';
import { TextFormField } from '@/components/common/form-fields';
import { applyFieldErrors, showError } from '@/lib/api-errors';
import { useCan } from '@/stores/auth-store';
import type { SupplierDto } from '@/types';
import { useSaveSupplier, useSuppliers } from '../hooks/use-catalog';
import { supplierSchema, type SupplierForm, type SupplierFormInput } from '../schemas';

function SupplierDialog({
  supplier,
  open,
  onOpenChange,
}: {
  supplier: SupplierDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const save = useSaveSupplier(supplier?.id ?? null);
  const form = useForm<SupplierFormInput, unknown, SupplierForm>({ resolver: zodResolver(supplierSchema) });

  useEffect(() => {
    if (open)
      form.reset({
        name: supplier?.name ?? '',
        phone: supplier?.phone ?? '',
        email: supplier?.email ?? '',
        address: supplier?.address ?? '',
      });
  }, [open, supplier, form]);

  const onSubmit = form.handleSubmit((v) =>
    save.mutate(v, {
      onSuccess: (s) => {
        toast.success(supplier ? `Đã cập nhật ${s.name}` : `Đã thêm ${s.name}`);
        onOpenChange(false);
      },
      onError: (err) => {
        if (!applyFieldErrors(err, ['name', 'phone', 'email', 'address'] as const, form.setError)) showError(err);
      },
    }),
  );

  return (
    <Dialog open={open} onOpenChange={(o) => !save.isPending && onOpenChange(o)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{supplier ? `Sửa nhà cung cấp` : 'Thêm nhà cung cấp'}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form id="supplier-form" onSubmit={onSubmit} noValidate className="space-y-4">
            <TextFormField control={form.control} name="name" label="Tên" required />
            <TextFormField control={form.control} name="phone" label="Điện thoại" inputMode="tel" />
            <TextFormField control={form.control} name="email" label="Email" type="email" />
            <TextFormField control={form.control} name="address" label="Địa chỉ" />
          </form>
        </Form>
        <DialogFooter>
          <Button variant="outline" disabled={save.isPending} onClick={() => onOpenChange(false)}>
            Hủy
          </Button>
          <Button type="submit" form="supplier-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            Lưu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function SuppliersTab() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const { data, isFetching } = useSuppliers({ search, page, pageSize: 20 });
  const canCreate = useCan('SUPPLIER', 'C');
  const canUpdate = useCan('SUPPLIER', 'U');
  const [dialog, setDialog] = useState<{ open: boolean; supplier: SupplierDto | null }>({ open: false, supplier: null });

  const columns: ColumnDef<SupplierDto>[] = [
    { id: 'name', header: 'Tên', cell: ({ row }) => <span className="font-medium">{row.original.name}</span> },
    { id: 'phone', header: 'Điện thoại', cell: ({ row }) => row.original.phone ?? '—' },
    { id: 'email', header: 'Email', cell: ({ row }) => row.original.email ?? '—' },
    { id: 'address', header: 'Địa chỉ', cell: ({ row }) => row.original.address ?? '—' },
    {
      id: 'actions',
      header: '',
      meta: { cellClassName: 'text-right' },
      cell: ({ row }) =>
        canUpdate && (
          <Button variant="ghost" size="icon-sm" aria-label={`Sửa ${row.original.name}`} onClick={() => setDialog({ open: true, supplier: row.original })}>
            <Pencil />
          </Button>
        ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <SearchInput
          aria-label="Tìm nhà cung cấp"
          placeholder="Tên hoặc số điện thoại..."
          value={search}
          onChange={(v) => {
            setSearch(v);
            setPage(1);
          }}
        />
        {canCreate && (
          <Button className="ml-auto" onClick={() => setDialog({ open: true, supplier: null })}>
            <Plus /> Thêm nhà cung cấp
          </Button>
        )}
      </div>
      <DataTable
        aria-label="Nhà cung cấp"
        columns={columns}
        data={data?.items ?? []}
        getRowId={(s) => String(s.id)}
        loading={isFetching}
        pagination={{ page, pageSize: 20, totalCount: data?.totalCount ?? 0, onPageChange: setPage }}
      />
      <SupplierDialog supplier={dialog.supplier} open={dialog.open} onOpenChange={(open) => setDialog((d) => ({ ...d, open }))} />
    </div>
  );
}
