import { adminUser, managerUser, authResponse, staffUser } from '@/test/fixtures';
import { can, canAny, PERMISSIONS, SETTINGS_PERMISSIONS } from '@/lib/permissions';
import { useAuthStore } from './auth-store';
import type { CurrentUserDto } from '@/types';

describe('can() permission helper', () => {
  it('returns false when not logged in', () => {
    expect(can(null, 'GOODS_ISSUE', 'R')).toBe(false);
    expect(can(undefined, 'GOODS_ISSUE', 'C')).toBe(false);
  });

  it('grants everything to admins even without explicit permissions', () => {
    expect(adminUser.permissions).toHaveLength(0);
    expect(can(adminUser, 'ROLE', 'D')).toBe(true);
    expect(can(adminUser, 'ANY_UNKNOWN_CODE', 'U')).toBe(true);
  });

  it('checks the specific C/R/U/D flag of the activity', () => {
    expect(can(managerUser, 'GOODS_ISSUE', 'U')).toBe(true);
    expect(can(managerUser, 'WAREHOUSE', 'U')).toBe(true);
    expect(can(managerUser, 'PRODUCT', 'C')).toBe(false);
    expect(can(managerUser, 'USER', 'R')).toBe(false);

    expect(can(staffUser, 'GOODS_ISSUE', 'C')).toBe(true);
    expect(can(staffUser, 'GOODS_ISSUE', 'U')).toBe(false); // staff drafts, manager posts
    expect(can(staffUser, 'WAREHOUSE', 'U')).toBe(false);
  });

  it('works for a user with only individual (UserActivity) permissions', () => {
    const user: CurrentUserDto = {
      ...staffUser,
      roles: [],
      permissions: [{ code: 'USER', c: false, r: true, u: false, d: false }],
    };
    expect(can(user, 'USER', 'R')).toBe(true);
    expect(can(user, 'USER', 'U')).toBe(false);
    expect(canAny(user, SETTINGS_PERMISSIONS)).toBe(true);
    expect(canAny(staffUser, SETTINGS_PERMISSIONS)).toBe(false);
  });

  it('store.can() reflects the current session and resets on logout', () => {
    const store = useAuthStore.getState();
    expect(store.can('GOODS_ISSUE', 'C')).toBe(false);
    store.setSession(authResponse(managerUser));
    expect(useAuthStore.getState().can('STOCK_REPORT', 'R')).toBe(true);
    useAuthStore.getState().setUser({ ...managerUser, permissions: [] });
    expect(useAuthStore.getState().can('STOCK_REPORT', 'R')).toBe(false);
    useAuthStore.getState().logout();
    expect(useAuthStore.getState().can('STOCK_REPORT', 'R')).toBe(false);
    expect(useAuthStore.getState().accessToken).toBeNull();
  });
});

describe('route / menu permission gates', () => {
  it('staff sees stock + document lists but not settings / API logs; admin sees everything', () => {
    expect(canAny(staffUser, PERMISSIONS.stock)).toBe(true);
    expect(canAny(staffUser, PERMISSIONS.goodsIssues)).toBe(true);
    expect(canAny(staffUser, PERMISSIONS.catalog)).toBe(true);
    expect(canAny(staffUser, PERMISSIONS.settings)).toBe(false);
    expect(canAny(staffUser, PERMISSIONS.apiLogs)).toBe(false);
    expect(canAny(managerUser, PERMISSIONS.settings)).toBe(false);
    expect(canAny(adminUser, PERMISSIONS.apiLogs)).toBe(true);
  });

  it('an empty requirement list means "any logged-in user"', () => {
    expect(canAny(staffUser, [])).toBe(true);
    expect(canAny(null, [])).toBe(false);
  });
});
