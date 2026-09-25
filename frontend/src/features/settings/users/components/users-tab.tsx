import { useState } from 'react';
import type { ColumnDef } from '@tanstack/react-table';
import { KeyRound, Search, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { DataTable } from '@/components/common/data-table';
import { formatDate } from '@/lib/date';
import { useDebouncedCallback } from '@/lib/hooks/use-debounced-callback';
import { useCan, useCurrentUser } from '@/stores/auth-store';
import type { UserListItemDto, UsersQuery } from '@/types';
import { useRoles } from '../../roles/hooks/use-roles';
import { useToggleUserActive, useUsers } from '../hooks/use-users';
import { AssignRolesDialog } from './assign-roles-dialog';
import { UserPermissionsSheet } from './user-permissions-sheet';

const ALL = '__all__';

export function UsersTab() {
  const me = useCurrentUser();
  const canEdit = useCan('USER', 'U');
  const [query, setQuery] = useState<UsersQuery>({ page: 1, pageSize: 20 });
  const [searchText, setSearchText] = useState('');
  const [rolesFor, setRolesFor] = useState<UserListItemDto | null>(null);
  const [permsFor, setPermsFor] = useState<UserListItemDto | null>(null);
  /** a locked user can no longer sign in, so locking is confirmed first */
  const [locking, setLocking] = useState<UserListItemDto | null>(null);
  const roles = useRoles();
  const users = useUsers(query);
  const toggleActive = useToggleUserActive();
  const pendingId = toggleActive.isPending ? toggleActive.variables?.id : undefined;
  const applySearch = useDebouncedCallback((text: string) =>
    setQuery((q) => {
      const search = text.trim() || undefined;
      return search === q.search ? q : { ...q, search, page: 1 };
    }),
  );

  const columns: ColumnDef<UserListItemDto>[] = [
    { id: 'fullName', header: 'Họ tên', cell: ({ row }) => <span className="font-medium">{row.original.fullName}</span> },
    { id: 'email', header: 'Email', cell: ({ row }) => row.original.email },
    {
      id: 'roles',
      header: 'Roles',
      cell: ({ row }) =>
        row.original.roles.length ? (
          <div className="flex flex-wrap gap-1">
            {row.original.roles.map((r) => (
              <Badge key={r.id} variant="secondary">
                {r.name}
              </Badge>
            ))}
          </div>
        ) : (
          <Badge variant="outline">Chưa có role</Badge>
        ),
    },
    { id: 'createdDate', header: 'Ngày tạo', cell: ({ row }) => formatDate(row.original.createdDate) },
    {
      id: 'isActive',
      header: 'Active',
      cell: ({ row }) => {
        const u = row.original;
        return (
          <Switch
            aria-label={`Active ${u.email}`}
            checked={u.isActive}
            disabled={!canEdit || u.id === me?.id || pendingId === u.id}
            onCheckedChange={(checked) =>
              checked ? toggleActive.mutate({ id: u.id, isActive: true }) : setLocking(u)
            }
          />
        );
      },
    },
    {
      id: 'actions',
      header: '',
      meta: { cellClassName: 'text-right' },
      cell: ({ row }) => (
        <div className="flex justify-end gap-2">
          {canEdit && (
            <Button variant="outline" size="sm" onClick={() => setRolesFor(row.original)}>
              <Users /> Gán role
            </Button>
          )}
          <Button variant="outline" size="sm" onClick={() => setPermsFor(row.original)}>
            <KeyRound /> Quyền riêng
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <form
          role="search"
          className="relative w-full sm:w-72"
          onSubmit={(e) => {
            e.preventDefault();
            applySearch.flush(searchText);
          }}
        >
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            aria-label="Tìm user"
            placeholder="Tìm theo tên / email"
            className="pl-8"
            value={searchText}
            onChange={(e) => {
              setSearchText(e.target.value);
              applySearch.run(e.target.value);
            }}
          />
        </form>
        <Select
          value={query.roleId ?? ALL}
          onValueChange={(v) => setQuery((q) => ({ ...q, roleId: v === ALL ? undefined : v, page: 1 }))}
        >
          <SelectTrigger aria-label="Lọc theo role" className="w-44">
            <SelectValue placeholder="Role" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>
              <span className="text-muted-foreground">Role: tất cả</span>
            </SelectItem>
            {(roles.data ?? []).map((r) => (
              <SelectItem key={r.id} value={r.id}>
                {r.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <DataTable
        aria-label="Người dùng"
        columns={columns}
        data={users.data?.items ?? []}
        getRowId={(u) => u.id}
        loading={users.isFetching}
        error={users.isError ? 'Không tải được danh sách người dùng' : undefined}
        onRetry={() => void users.refetch()}
        pagination={{
          page: query.page ?? 1,
          pageSize: query.pageSize ?? 20,
          totalCount: users.data?.totalCount ?? 0,
          onPageChange: (page) => setQuery((q) => ({ ...q, page })),
          onPageSizeChange: (pageSize) => setQuery((q) => ({ ...q, pageSize, page: 1 })),
        }}
      />
      <AssignRolesDialog user={rolesFor} onClose={() => setRolesFor(null)} />
      <UserPermissionsSheet user={permsFor} onClose={() => setPermsFor(null)} />
      <ConfirmDialog
        open={!!locking}
        onOpenChange={(o) => !o && setLocking(null)}
        title={`Khóa tài khoản ${locking?.fullName ?? ''}?`}
        description={`${locking?.email ?? ''} sẽ không đăng nhập được cho đến khi được mở khóa lại.`}
        confirmText="Khóa"
        destructive
        onConfirm={() => locking && toggleActive.mutateAsync({ id: locking.id, isActive: false })}
      />
    </div>
  );
}
