import { Link, Navigate, Outlet, useLocation } from 'react-router-dom';
import { FileQuestion, ShieldX } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/empty-state';
import { canAny, PERMISSIONS, type PermissionRequirement } from '@/lib/permissions';
import { useCanAny, useCurrentUser, useIsAuthenticated } from '@/stores/auth-store';

/** Requires an authenticated user; otherwise redirects to /login (remembering where to return). */
export function ProtectedRoute() {
  const isAuthenticated = useIsAuthenticated();
  const location = useLocation();
  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }
  return <Outlet />;
}

/** For login/register pages: an authenticated user is sent to the home page. */
export function PublicOnlyRoute() {
  const isAuthenticated = useIsAuthenticated();
  if (isAuthenticated) return <Navigate to="/" replace />;
  return <Outlet />;
}

/** Renders child routes only when the user has at least one of the required permissions. */
export function PermissionRoute({ anyOf }: { anyOf: readonly PermissionRequirement[] }) {
  const allowed = useCanAny(anyOf);
  if (!allowed) {
    return (
      <EmptyState
        icon={<ShieldX />}
        title="403 — Không có quyền"
        description="Bạn không có quyền truy cập trang này."
        action={
          <Button asChild>
            <Link to="/">Về trang chủ</Link>
          </Button>
        }
      />
    );
  }
  return <Outlet />;
}

/** First page the user may open: dashboard, else the first document list, else catalog. */
export function HomeRedirect() {
  const user = useCurrentUser();
  const target = HOME_CANDIDATES.find(([, anyOf]) => canAny(user, anyOf))?.[0] ?? '/catalog';
  return <Navigate to={target} replace />;
}

const HOME_CANDIDATES: [string, readonly PermissionRequirement[]][] = [
  ['/dashboard', PERMISSIONS.stock],
  ['/goods-receipts', PERMISSIONS.goodsReceipts],
  ['/goods-issues', PERMISSIONS.goodsIssues],
  ['/transfers', PERMISSIONS.transfers],
  ['/stock-takes', PERMISSIONS.stockTakes],
  ['/settings', PERMISSIONS.settings],
];

export function NotFound() {
  return (
    <EmptyState
      icon={<FileQuestion />}
      title="404"
      description="Trang không tồn tại."
      action={
        <Button asChild>
          <Link to="/">Về trang chủ</Link>
        </Button>
      }
    />
  );
}
