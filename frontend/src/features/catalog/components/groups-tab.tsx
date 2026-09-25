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
import { TextFormField } from '@/components/common/form-fields';
import { applyFieldErrors, showError } from '@/lib/api-errors';
import { useCan } from '@/stores/auth-store';
import type { ProductGroupDto } from '@/types';
import { useProductGroups, useSaveProductGroup } from '../hooks/use-catalog';
import { groupSchema, type GroupForm } from '../schemas';

function GroupDialog({
  group,
  open,
  onOpenChange,
}: {
  group: ProductGroupDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const save = useSaveProductGroup(group?.id ?? null);
  const form = useForm<GroupForm>({ resolver: zodResolver(groupSchema) });

  useEffect(() => {
    if (open) form.reset({ name: group?.name ?? '' });
  }, [open, group, form]);

  const onSubmit = form.handleSubmit((v) =>
    save.mutate(v.name, {
      onSuccess: () => {
        toast.success(group ? 'Đã đổi tên nhóm hàng' : 'Đã thêm nhóm hàng');
        onOpenChange(false);
      },
      onError: (err) => {
        if (!applyFieldErrors(err, ['name'] as const, form.setError)) showError(err);
      },
    }),
  );

  return (
    <Dialog open={open} onOpenChange={(o) => !save.isPending && onOpenChange(o)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{group ? 'Đổi tên nhóm hàng' : 'Thêm nhóm hàng'}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form id="group-form" onSubmit={onSubmit} noValidate>
            <TextFormField control={form.control} name="name" label="Tên nhóm" required />
          </form>
        </Form>
        <DialogFooter>
          <Button variant="outline" disabled={save.isPending} onClick={() => onOpenChange(false)}>
            Hủy
          </Button>
          <Button type="submit" form="group-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            Lưu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function GroupsTab() {
  const { data, isLoading } = useProductGroups();
  const canCreate = useCan('PRODUCT', 'C');
  const canUpdate = useCan('PRODUCT', 'U');
  const [dialog, setDialog] = useState<{ open: boolean; group: ProductGroupDto | null }>({ open: false, group: null });

  const columns: ColumnDef<ProductGroupDto>[] = [
    { id: 'name', header: 'Nhóm hàng', cell: ({ row }) => <span className="font-medium">{row.original.name}</span> },
    {
      id: 'actions',
      header: '',
      meta: { cellClassName: 'text-right' },
      cell: ({ row }) =>
        canUpdate && (
          <Button variant="ghost" size="icon-sm" aria-label={`Sửa ${row.original.name}`} onClick={() => setDialog({ open: true, group: row.original })}>
            <Pencil />
          </Button>
        ),
    },
  ];

  return (
    <div className="space-y-4">
      {canCreate && (
        <div className="flex justify-end">
          <Button onClick={() => setDialog({ open: true, group: null })}>
            <Plus /> Thêm nhóm hàng
          </Button>
        </div>
      )}
      <DataTable aria-label="Nhóm hàng" columns={columns} data={data ?? []} getRowId={(g) => String(g.id)} loading={isLoading} />
      <GroupDialog group={dialog.group} open={dialog.open} onOpenChange={(open) => setDialog((d) => ({ ...d, open }))} />
    </div>
  );
}
