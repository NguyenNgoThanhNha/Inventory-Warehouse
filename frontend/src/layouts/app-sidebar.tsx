import { Link, useLocation } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';
import { ArrowDownToLine, BellRing, BookOpen, LayoutDashboard, ArrowLeftRight, ArrowUpFromLine, Boxes, ClipboardCheck, FileClock, Package, Settings, Warehouse } from 'lucide-react';
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  useSidebar,
} from '@/components/ui/sidebar';
import { canAny, PERMISSIONS, type PermissionRequirement } from '@/lib/permissions';
import { useAuthStore } from '@/stores/auth-store';

interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** visible when the user has any of these permissions (empty = everyone) */
  anyOf: readonly PermissionRequirement[];
}

export const NAV_MAIN: NavItem[] = [
  { to: '/dashboard', label: 'Tổng quan', icon: LayoutDashboard, anyOf: PERMISSIONS.stock },
  { to: '/stock', label: 'Tồn kho', icon: Boxes, anyOf: PERMISSIONS.stock },
  { to: '/kardex', label: 'Thẻ kho', icon: BookOpen, anyOf: PERMISSIONS.stock },
  { to: '/alerts', label: 'Cảnh báo tồn', icon: BellRing, anyOf: PERMISSIONS.stock },
  { to: '/goods-receipts', label: 'Nhập kho', icon: ArrowDownToLine, anyOf: PERMISSIONS.goodsReceipts },
  { to: '/goods-issues', label: 'Xuất kho', icon: ArrowUpFromLine, anyOf: PERMISSIONS.goodsIssues },
  { to: '/transfers', label: 'Chuyển kho', icon: ArrowLeftRight, anyOf: PERMISSIONS.transfers },
  { to: '/stock-takes', label: 'Kiểm kê', icon: ClipboardCheck, anyOf: PERMISSIONS.stockTakes },
];

export const NAV_ADMIN: NavItem[] = [
  { to: '/catalog', label: 'Danh mục', icon: Package, anyOf: PERMISSIONS.catalog },
  { to: '/settings', label: 'Phân quyền', icon: Settings, anyOf: PERMISSIONS.settings },
  { to: '/api-logs', label: 'API Logs', icon: FileClock, anyOf: PERMISSIONS.apiLogs },
];

function NavGroup({ label, items }: { label: string; items: NavItem[] }) {
  const user = useAuthStore((s) => s.user);
  const { pathname } = useLocation();
  const { isMobile, setOpenMobile } = useSidebar();
  const visible = items.filter((i) => canAny(user, i.anyOf));
  if (!visible.length) return null;

  return (
    <SidebarGroup>
      <SidebarGroupLabel>{label}</SidebarGroupLabel>
      <SidebarGroupContent>
        <SidebarMenu>
          {visible.map((item) => {
            const active = pathname === item.to || pathname.startsWith(`${item.to}/`);
            return (
              <SidebarMenuItem key={item.to}>
                <SidebarMenuButton asChild isActive={active} tooltip={item.label}>
                  <Link
                    to={item.to}
                    aria-current={active ? 'page' : undefined}
                    onClick={() => isMobile && setOpenMobile(false)}
                  >
                    <item.icon />
                    <span>{item.label}</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            );
          })}
        </SidebarMenu>
      </SidebarGroupContent>
    </SidebarGroup>
  );
}

export function AppSidebar() {
  return (
    <Sidebar collapsible="icon" aria-label="Điều hướng chính">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to="/">
                <span className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                  <Warehouse className="size-4" />
                </span>
                <span className="grid flex-1 text-left leading-tight">
                  <span className="truncate font-semibold">Quản lý kho</span>
                  <span className="truncate text-xs text-muted-foreground">Inventory system</span>
                </span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <NavGroup label="Kho" items={NAV_MAIN} />
        <NavGroup label="Quản trị" items={NAV_ADMIN} />
      </SidebarContent>
      <SidebarRail />
    </Sidebar>
  );
}
