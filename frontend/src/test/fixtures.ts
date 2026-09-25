import type {
  ActivityDto,
  ApiLogDetailDto,
  ApiLogListItemDto,
  AuthResponse,
  CurrentUserDto,
  PermissionDto,
  ProductDto,
  ProductGroupDto,
  StockDocumentDto,
  StockRowDto,
  WarehouseDto,
} from '@/types';

const perm = (code: string, flags: string): PermissionDto => ({
  code,
  c: flags.includes('C'),
  r: flags.includes('R'),
  u: flags.includes('U'),
  d: flags.includes('D'),
});

export const adminUser: CurrentUserDto = {
  id: '00000000-0000-0000-0000-00000000a001',
  email: 'admin@inventory.local',
  fullName: 'Admin',
  isAdmin: true,
  roles: ['Admin'],
  permissions: [],
};

/** Seed "Manager" role: approves (U) documents, sets thresholds (WAREHOUSE:U), reads reports. */
export const managerUser: CurrentUserDto = {
  id: '00000000-0000-0000-0000-00000000a002',
  email: 'manager@inventory.local',
  fullName: 'Lê Quản Lý',
  isAdmin: false,
  roles: ['Manager'],
  permissions: [
    perm('PRODUCT', 'R'),
    perm('WAREHOUSE', 'RU'),
    perm('SUPPLIER', 'CRU'),
    perm('GOODS_RECEIPT', 'CRUD'),
    perm('GOODS_ISSUE', 'CRUD'),
    perm('TRANSFER', 'CRUD'),
    perm('STOCK_TAKE', 'CRUD'),
    perm('STOCK_REPORT', 'R'),
    perm('IMPORT_EXPORT', 'R'),
  ],
};

/** Seed "Staff" role: drafts documents (C, D) but cannot post them (no U). */
export const staffUser: CurrentUserDto = {
  id: '00000000-0000-0000-0000-00000000a003',
  email: 'staff@inventory.local',
  fullName: 'Phạm Thủ Kho',
  isAdmin: false,
  roles: ['Staff'],
  permissions: [
    perm('PRODUCT', 'R'),
    perm('WAREHOUSE', 'R'),
    perm('SUPPLIER', 'R'),
    perm('GOODS_RECEIPT', 'CRD'),
    perm('GOODS_ISSUE', 'CRD'),
    perm('TRANSFER', 'CRD'),
    perm('STOCK_TAKE', 'CRD'),
    perm('STOCK_REPORT', 'R'),
  ],
};

export const activities: ActivityDto[] = [
  { id: 'act-issue', code: 'GOODS_ISSUE', name: 'Phiếu xuất kho', description: null },
  { id: 'act-report', code: 'STOCK_REPORT', name: 'Tồn kho & báo cáo', description: null },
  { id: 'act-user', code: 'USER', name: 'Người dùng', description: null },
];

export function authResponse(user: CurrentUserDto = managerUser, suffix = '1'): AuthResponse {
  return {
    accessToken: `access-${suffix}`,
    refreshToken: `refresh-${suffix}`,
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user,
  };
}

export const warehouses: WarehouseDto[] = [
  { id: 1, code: 'KHO-A', name: 'Kho A — TP.HCM', address: null, isActive: true },
  { id: 2, code: 'KHO-B', name: 'Kho B — Hà Nội', address: null, isActive: true },
];

export const productGroups: ProductGroupDto[] = [
  { id: 1, name: 'Ốc vít & bu lông' },
  { id: 2, name: 'Phụ kiện cửa' },
];

export const products: ProductDto[] = [
  { id: 1, sku: 'P001', name: 'Ốc vít M6', unit: 'cái', groupId: 1, groupName: 'Ốc vít & bu lông', cost: 500, price: 800, imageUrl: null, isActive: true },
  { id: 2, sku: 'P002', name: 'Bản lề inox', unit: 'cái', groupId: 2, groupName: 'Phụ kiện cửa', cost: 35000, price: 52000, imageUrl: null, isActive: true },
];

/** Available quantity at warehouse 1 per product id (P001: 120, P002: 8 — like the spec wireframe). */
export const availableAtA: Record<number, number> = { 1: 120, 2: 8 };

export const stockRows: StockRowDto[] = [
  {
    productId: 1,
    sku: 'P001',
    name: 'Ốc vít M6',
    unit: 'cái',
    groupName: 'Ốc vít & bu lông',
    total: 160,
    isLow: false,
    warehouses: [
      { warehouseId: 1, quantity: 120, minThreshold: 50, isLow: false },
      { warehouseId: 2, quantity: 40, minThreshold: 0, isLow: false },
    ],
  },
  {
    productId: 2,
    sku: 'P002',
    name: 'Bản lề inox',
    unit: 'cái',
    groupName: 'Phụ kiện cửa',
    total: 10,
    isLow: true,
    warehouses: [
      { warehouseId: 1, quantity: 8, minThreshold: 30, isLow: true },
      { warehouseId: 2, quantity: 2, minThreshold: 0, isLow: false },
    ],
  },
];

export function stockDocument(overrides: Partial<StockDocumentDto> = {}): StockDocumentDto {
  return {
    id: 455,
    code: 'PX-2026-00455',
    type: 'GoodsIssue',
    status: 'Draft',
    warehouse: { id: 1, name: 'Kho A — TP.HCM' },
    toWarehouse: null,
    supplier: null,
    reason: 'Sale',
    note: null,
    createdDate: '2026-09-25T02:00:00Z',
    createdName: 'Phạm Thủ Kho',
    postedAt: null,
    postedByName: null,
    rowVersion: 'AAAAAAAAB9E=',
    lines: [
      {
        id: 1,
        productId: 1,
        sku: 'P001',
        productName: 'Ốc vít M6',
        unit: 'cái',
        quantity: 30,
        unitCost: null,
        systemQuantity: null,
        difference: null,
        note: null,
      },
    ],
    ...overrides,
  };
}

export const apiLogItems: ApiLogListItemDto[] = [
  {
    id: 88,
    module: 'Inventory',
    traceId: '00-8e50f81fa0e437c95984f29d2ce65ecc-14eb4b45a65c7a78-00',
    ip: '127.0.0.1',
    userId: adminUser.id,
    userName: 'admin@inventory.local',
    method: 'GET',
    url: '/api/v1/goods-issues/999999',
    statusCode: 404,
    durationMs: 23,
    createdDate: '2026-09-23T08:34:03.733Z',
  },
  {
    id: 86,
    module: 'Inventory',
    traceId: '00-870670991e289457a8ba52f2c55e04c9-071c692fc15725e3-00',
    ip: '::1',
    userId: null,
    userName: null,
    method: 'POST',
    url: '/api/v1/auth/login',
    statusCode: 200,
    durationMs: 620,
    createdDate: '2026-09-23T08:30:06.746Z',
  },
];

export function apiLogDetail(id: number): ApiLogDetailDto {
  const item = apiLogItems.find((l) => l.id === id) ?? apiLogItems[0];
  return {
    ...item,
    request: item.method === 'POST' ? JSON.stringify({ email: 'admin@inventory.local', password: '***' }) : null,
    response: JSON.stringify({ title: 'Không tìm thấy', status: 404 }),
    userAgent: 'Mozilla/5.0 (test)',
  };
}
