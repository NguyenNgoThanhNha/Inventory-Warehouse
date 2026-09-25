import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { MultiSelect } from '@/components/common/combobox';
import type { UserListItemDto } from '@/types';
import { useRoles } from '../../roles/hooks/use-roles';
import { useSetUserRoles } from '../hooks/use-users';

export function AssignRolesDialog({ user, onClose }: { user: UserListItemDto | null; onClose: () => void }) {
  const roles = useRoles(!!user);
  const save = useSetUserRoles();
  const [roleIds, setRoleIds] = useState<string[]>([]);

  useEffect(() => {
    if (user) setRoleIds(user.roles.map((r) => r.id));
  }, [user]);

  return (
    <Dialog open={!!user} onOpenChange={(o) => !o && !save.isPending && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Gán role — {user?.fullName}</DialogTitle>
          <DialogDescription>{user?.email}</DialogDescription>
        </DialogHeader>
        <MultiSelect
          aria-label="Chọn role"
          placeholder="Chọn role"
          searchPlaceholder="Tìm role..."
          loading={roles.isLoading}
          value={roleIds}
          onChange={setRoleIds}
          options={(roles.data ?? []).map((r) => ({ value: r.id, label: r.isAdmin ? `${r.name} (Toàn quyền)` : r.name }))}
        />
        <DialogFooter>
          <Button variant="outline" disabled={save.isPending} onClick={onClose}>
            Hủy
          </Button>
          <Button
            disabled={save.isPending || !user}
            onClick={() => user && save.mutate({ id: user.id, roleIds }, { onSuccess: onClose })}
          >
            {save.isPending && <Loader2 className="animate-spin" />}
            Lưu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
