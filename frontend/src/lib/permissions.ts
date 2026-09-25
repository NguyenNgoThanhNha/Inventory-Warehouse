import type { ActivityAction, CurrentUserDto } from '@/types';

/** [activityCode, action], e.g. ['GOODS_ISSUE', 'R'] */
export type PermissionRequirement = readonly [code: string, action: ActivityAction];

const ACTION_KEY: Record<ActivityAction, 'c' | 'r' | 'u' | 'd'> = { C: 'c', R: 'r', U: 'u', D: 'd' };

/** Pure permission check: Admin → everything; otherwise the effective permission flag. */
export function can(user: CurrentUserDto | null | undefined, code: string, action: ActivityAction): boolean {
  if (!user) return false;
  if (user.isAdmin) return true;
  const key = ACTION_KEY[action];
  return user.permissions.some((p) => p.code === code && p[key]);
}

/** true when the user satisfies at least one of the requirements (an empty list = everyone). */
export function canAny(user: CurrentUserDto | null | undefined, requirements: readonly PermissionRequirement[]) {
  if (!user) return false;
  if (requirements.length === 0) return true;
  return requirements.some(([code, action]) => can(user, code, action));
}

/** Route / menu gates (single source of truth for the router and the sidebar). */
export const PERMISSIONS = {
  /** stock table, dashboard, kardex */
  stock: [['STOCK_REPORT', 'R']],
  goodsReceipts: [['GOODS_RECEIPT', 'R']],
  goodsIssues: [['GOODS_ISSUE', 'R']],
  transfers: [['TRANSFER', 'R']],
  stockTakes: [['STOCK_TAKE', 'R']],
  /** Catalog page is visible when the user can see at least one of its tabs. */
  catalog: [
    ['PRODUCT', 'R'],
    ['WAREHOUSE', 'R'],
    ['SUPPLIER', 'R'],
  ],
  apiLogs: [['API_LOG', 'R']],
  /** Settings page is visible when the user can manage at least one of its tabs. */
  settings: [
    ['USER', 'R'],
    ['ROLE', 'R'],
  ],
} as const satisfies Record<string, readonly PermissionRequirement[]>;

export const SETTINGS_PERMISSIONS: readonly PermissionRequirement[] = PERMISSIONS.settings;
