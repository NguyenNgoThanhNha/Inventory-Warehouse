import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Form } from '@/components/ui/form';
import { Label } from '@/components/ui/label';
import { TextareaFormField, TextFormField } from '@/components/common/form-fields';
import { applyFieldErrors, getStatus, showError } from '@/lib/api-errors';
import type { ActivityPermissionInput, RoleDto } from '@/types';
import { normalizePermissions, PermissionMatrix, toPermissionInputs } from '../../components/permission-matrix';
import { useActivities, useRole, useSaveRole } from '../hooks/use-roles';

const roleSchema = z.object({
  name: z.string().trim().min(1, 'Vui lòng nhập tên role').max(100, 'Tối đa 100 ký tự'),
  description: z.string().trim().max(500, 'Tối đa 500 ký tự'),
});
type RoleForm = z.infer<typeof roleSchema>;

/** role === null → create; otherwise edit */
export function RoleDialog({
  open,
  role,
  onOpenChange,
}: {
  open: boolean;
  role: RoleDto | null;
  onOpenChange: (open: boolean) => void;
}) {
  const [activities, setActivities] = useState<ActivityPermissionInput[]>([]);
  const activityList = useActivities(open);
  const detail = useRole(role?.id, open);
  const save = useSaveRole(role?.id);

  const form = useForm<RoleForm>({ resolver: zodResolver(roleSchema), defaultValues: { name: '', description: '' } });

  useEffect(() => {
    if (!open) return;
    if (!role) {
      form.reset({ name: '', description: '' });
      setActivities([]);
    } else if (detail.data) {
      form.reset({ name: detail.data.name, description: detail.data.description ?? '' });
      setActivities(toPermissionInputs(detail.data.activities));
    }
  }, [open, role, detail.data, form]);

  const onSubmit = form.handleSubmit((v) =>
    save.mutate(
      { name: v.name, description: v.description || null, activities: normalizePermissions(activities) },
      {
        onSuccess: (r) => {
          toast.success(role ? `Đã cập nhật role ${r.name}` : `Đã tạo role ${r.name}`);
          onOpenChange(false);
        },
        onError: (err) => {
          if (getStatus(err) === 409) form.setError('name', { type: 'server', message: 'Tên role đã tồn tại' });
          else if (!applyFieldErrors(err, ['name', 'description'] as const, form.setError)) showError(err);
        },
      },
    ),
  );

  return (
    <Dialog open={open} onOpenChange={(o) => !save.isPending && onOpenChange(o)}>
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{role ? `Sửa role: ${role.name}` : 'Tạo role'}</DialogTitle>
          <DialogDescription>Chọn quyền C/R/U/D cho từng chức năng.</DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form id="role-form" onSubmit={onSubmit} noValidate className="space-y-4">
            <TextFormField control={form.control} name="name" label="Tên role" required />
            <TextareaFormField control={form.control} name="description" label="Mô tả" rows={2} />
            <div className="space-y-2">
              <Label>Quyền</Label>
              <PermissionMatrix
                activities={activityList.data ?? []}
                value={activities}
                onChange={setActivities}
                loading={activityList.isLoading || (!!role && detail.isLoading)}
              />
            </div>
          </form>
        </Form>
        <DialogFooter>
          <Button variant="outline" disabled={save.isPending} onClick={() => onOpenChange(false)}>
            Hủy
          </Button>
          <Button type="submit" form="role-form" disabled={save.isPending || (!!role && !detail.data)}>
            {save.isPending && <Loader2 className="animate-spin" />}
            Lưu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
