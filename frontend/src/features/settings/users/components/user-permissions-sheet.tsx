import { useEffect, useState } from 'react';
import { AlertCircle, Info, Loader2, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { getErrorMessage } from '@/lib/api-errors';
import { useCan } from '@/stores/auth-store';
import type { ActivityPermissionInput, UserListItemDto } from '@/types';
import { FlagsText, normalizePermissions, PermissionMatrix, toPermissionInputs } from '../../components/permission-matrix';
import { FullAccessBadge } from '../../components/full-access-badge';
import { useActivities } from '../../roles/hooks/use-roles';
import { useSetUserPermissions, useUserPermissions } from '../hooks/use-users';

/** "Quyền riêng" sheet: edits UserActivity permissions and shows the read-only effective permissions. */
export function UserPermissionsSheet({ user, onClose }: { user: UserListItemDto | null; onClose: () => void }) {
  const canEdit = useCan('USER', 'U');
  const activities = useActivities(!!user);
  const detail = useUserPermissions(user?.id);
  const save = useSetUserPermissions();
  const [draft, setDraft] = useState<ActivityPermissionInput[]>([]);

  useEffect(() => {
    if (detail.data) setDraft(toPermissionInputs(detail.data.userActivities));
  }, [detail.data]);

  const effective = detail.data?.effective ?? [];

  return (
    <Sheet open={!!user} onOpenChange={(o) => !o && onClose()}>
      <SheetContent className="w-full gap-0 sm:max-w-3xl">
        <SheetHeader>
          <SheetTitle>Quyền riêng — {user?.fullName}</SheetTitle>
          <SheetDescription>{user?.email}</SheetDescription>
        </SheetHeader>
        <div className="flex-1 space-y-3 overflow-y-auto px-4 pb-4">
          {detail.isError && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{getErrorMessage(detail.error)}</AlertDescription>
            </Alert>
          )}
          {detail.data && (
            <>
              <div className="flex flex-wrap items-center gap-1.5 text-sm">
                <span className="text-muted-foreground">Role:</span>
                {detail.data.roles.length
                  ? detail.data.roles.map((r) => (
                      <Badge key={r.id} variant="secondary">
                        {r.name}
                      </Badge>
                    ))
                  : '—'}
              </div>
              {detail.data.isAdmin && (
                <Alert>
                  <Info />
                  <AlertDescription>Tài khoản có role Admin → toàn quyền, quyền riêng không có tác dụng.</AlertDescription>
                </Alert>
              )}
              <Alert>
                <TriangleAlert />
                <AlertDescription>
                  Quyền riêng được cộng thêm vào quyền của role. Cột “Quyền hiệu lực” là kết quả sau khi gộp (cập nhật sau khi
                  Lưu).
                </AlertDescription>
              </Alert>
            </>
          )}
          <PermissionMatrix
            activities={activities.data ?? []}
            value={draft}
            onChange={setDraft}
            readOnly={!canEdit}
            loading={activities.isLoading || detail.isLoading}
            extraColumn={{
              title: 'Quyền hiệu lực',
              render: (a) =>
                detail.data?.isAdmin ? (
                  <FullAccessBadge />
                ) : (
                  <FlagsText flags={effective.find((e) => e.activityId === a.id)} />
                ),
            }}
          />
        </div>
        <SheetFooter className="flex-row justify-end border-t">
          <Button variant="outline" onClick={onClose}>
            Đóng
          </Button>
          {canEdit && (
            <Button
              disabled={save.isPending || !detail.data || !user}
              onClick={() => user && save.mutate({ id: user.id, activities: normalizePermissions(draft) })}
            >
              {save.isPending && <Loader2 className="animate-spin" />}
              Lưu
            </Button>
          )}
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
