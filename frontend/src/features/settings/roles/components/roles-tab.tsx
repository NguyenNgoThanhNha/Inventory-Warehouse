import { useState } from 'react';
import type { ColumnDef } from '@tanstack/react-table';
import { Info, Lock, Pencil, Plus, Trash2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { DataTable } from '@/components/common/data-table';
import { FullAccessBadge } from '../../components/full-access-badge';
import { useCan } from '@/stores/auth-store';
import type { RoleDto } from '@/types';
import { useDeleteRole, useRoles } from '../hooks/use-roles';
import { RoleDialog } from './role-dialog';

export function RolesTab() {
  const canCreate = useCan('ROLE', 'C');
  const canUpdate = useCan('ROLE', 'U');
  const canDelete = useCan('ROLE', 'D');
  const { data, isLoading } = useRoles();
  const remove = useDeleteRole();
  const [dialog, setDialog] = useState<{ open: boolean; role: RoleDto | null }>({ open: false, role: null });
  const [deleting, setDeleting] = useState<RoleDto | null>(null);

  const columns: ColumnDef<RoleDto>[] = [
    {
      id: 'name',
      header: 'Tên',
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          <span className="font-medium">{row.original.name}</span>
          {row.original.isAdmin && (
            <FullAccessBadge />
          )}
        </div>
      ),
    },
    {
      id: 'description',
      header: 'Mô tả',
      cell: ({ row }) => row.original.description ?? <span className="text-muted-foreground">—</span>,
      meta: { cellClassName: 'whitespace-normal' },
    },
    {
      id: 'userCount',
      header: 'Số user',
      cell: ({ row }) => row.original.userCount,
      meta: { headerClassName: 'text-right w-24', cellClassName: 'text-right tabular-nums' },
    },
    {
      id: 'actions',
      header: '',
      meta: { cellClassName: 'text-right' },
      cell: ({ row }) => {
        const r = row.original;
        if (r.isAdmin) {
          return (
            <Badge variant="secondary">
              <Lock /> Không thể sửa
            </Badge>
          );
        }
        return (
          <div className="flex justify-end gap-2">
            {canUpdate && (
              <Button variant="outline" size="sm" onClick={() => setDialog({ open: true, role: r })}>
                <Pencil /> Sửa
              </Button>
            )}
            {canDelete && (
              <Button variant="destructive" size="icon-sm" aria-label={`Xóa ${r.name}`} onClick={() => setDeleting(r)}>
                <Trash2 />
              </Button>
            )}
          </div>
        );
      },
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start">
        <Alert className="flex-1">
          <Info />
          <AlertDescription>
            Quyền hiệu lực của user = OR(quyền các role, quyền riêng). Role Admin có toàn quyền.
          </AlertDescription>
        </Alert>
        {canCreate && (
          <Button onClick={() => setDialog({ open: true, role: null })}>
            <Plus /> Tạo role
          </Button>
        )}
      </div>
      <DataTable aria-label="Roles" columns={columns} data={data ?? []} getRowId={(r) => r.id} loading={isLoading} />
      <RoleDialog open={dialog.open} role={dialog.role} onOpenChange={(open) => setDialog((d) => ({ ...d, open }))} />
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Xóa role "${deleting?.name ?? ''}"?`}
        description={
          deleting?.userCount
            ? `${deleting.userCount} user đang có role này sẽ mất quyền tương ứng.`
            : 'Thao tác này không thể hoàn tác.'
        }
        confirmText="Xóa"
        destructive
        onConfirm={() => deleting && remove.mutateAsync(deleting.id)}
      />
    </div>
  );
}
