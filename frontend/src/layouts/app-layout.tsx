import { Suspense, useState } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { LogOut, Search } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { SidebarInset, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar';
import { ThemeToggle } from '@/components/common/theme-toggle';
import { useLogout, useSyncCurrentUser } from '@/features/auth';
import { NotificationBell } from '@/features/notifications';
import { useAuthStore } from '@/stores/auth-store';
import { AppSidebar } from './app-sidebar';

const initials = (name: string | undefined) =>
  (name ?? '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0]?.toUpperCase())
    .join('');

function UserMenu() {
  const user = useAuthStore((s) => s.user);
  const logout = useLogout();
  const navigate = useNavigate();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-9 gap-2 px-1.5" data-testid="user-menu" aria-label="Tài khoản">
          <Avatar className="size-7">
            <AvatarFallback className="bg-primary text-xs text-primary-foreground">{initials(user?.fullName)}</AvatarFallback>
          </Avatar>
          <span className="hidden max-w-40 truncate text-sm font-medium md:inline">{user?.fullName}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel className="space-y-1 font-normal">
          <div className="text-sm font-medium text-foreground">{user?.fullName}</div>
          <div className="text-xs text-muted-foreground">{user?.email}</div>
          <div className="flex flex-wrap gap-1 pt-1">
            {user?.isAdmin && (
              <Badge variant="outline" className="border-fuchsia-200 bg-fuchsia-50 text-fuchsia-700 dark:border-fuchsia-900 dark:bg-fuchsia-950 dark:text-fuchsia-300">
                Toàn quyền
              </Badge>
            )}
            {user?.roles.map((r) => (
              <Badge key={r} variant="secondary">
                {r}
              </Badge>
            ))}
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          variant="destructive"
          onSelect={() =>
            void logout().then(() => {
              navigate('/login', { replace: true });
            })
          }
        >
          <LogOut /> Đăng xuất
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function GlobalSearch() {
  const navigate = useNavigate();
  const [value, setValue] = useState('');
  return (
    <form
      role="search"
      className="relative w-full max-w-sm"
      onSubmit={(e) => {
        e.preventDefault();
        const q = value.trim();
        navigate(q ? `/stock?search=${encodeURIComponent(q)}` : '/stock');
      }}
    >
      <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        aria-label="Tìm hàng trong kho"
        placeholder="Tìm SKU / tên hàng..."
        className="pl-8"
        value={value}
        onChange={(e) => setValue(e.target.value)}
      />
    </form>
  );
}

/** Authenticated shell: collapsible shadcn Sidebar (off-canvas sheet on mobile) + topbar. */
export function AppLayout() {
  useSyncCurrentUser();
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset className="min-w-0 bg-muted/30">
        <header className="sticky top-0 z-20 flex h-14 shrink-0 items-center gap-2 border-b bg-background/95 px-3 backdrop-blur sm:px-4">
          <SidebarTrigger aria-label="Thu gọn / mở menu" />
          <Separator orientation="vertical" className="mr-1 hidden h-5! sm:block" />
          <div className="flex min-w-0 flex-1 justify-end sm:justify-start">
            <GlobalSearch />
          </div>
          <div className="flex items-center gap-1">
            <NotificationBell />
            <ThemeToggle />
            <UserMenu />
          </div>
        </header>
        <div className="mx-auto w-full max-w-screen-2xl min-w-0 flex-1 p-3 sm:p-6">
          <Suspense
            fallback={
              <div className="space-y-4" aria-busy>
                <Skeleton className="h-8 w-48" />
                <Skeleton className="h-64 w-full" />
              </div>
            }
          >
            <Outlet />
          </Suspense>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}
