import { Outlet } from 'react-router-dom';
import { Warehouse } from 'lucide-react';
import { ThemeToggle } from '@/components/common/theme-toggle';

/** Centered shell for login / register / password pages. */
export function AuthLayout() {
  return (
    <div className="relative flex min-h-svh flex-col items-center justify-center gap-6 bg-gradient-to-br from-blue-50 via-background to-background p-4 dark:from-blue-950/40">
      <div className="absolute top-3 right-3">
        <ThemeToggle />
      </div>
      <div className="flex items-center gap-2 text-lg font-semibold">
        <span className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
          <Warehouse className="size-5" />
        </span>
        Quản lý kho
      </div>
      <Outlet />
    </div>
  );
}
