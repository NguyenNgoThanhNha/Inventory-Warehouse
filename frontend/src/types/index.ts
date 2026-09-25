// Types mirroring the Inventory API (v1, permission-based authorization) — see README "API"

export const ACTIVITY_ACTIONS = ['C', 'R', 'U', 'D'] as const;
export type ActivityAction = (typeof ACTIVITY_ACTIONS)[number];

/** Activity codes shared by FE/BE (see API contract "Phân quyền"). */
export const ACTIVITY = {
  PRODUCT: 'PRODUCT',
  WAREHOUSE: 'WAREHOUSE',
  SUPPLIER: 'SUPPLIER',
  GOODS_RECEIPT: 'GOODS_RECEIPT',
  GOODS_ISSUE: 'GOODS_ISSUE',
  TRANSFER: 'TRANSFER',
  STOCK_TAKE: 'STOCK_TAKE',
  STOCK_REPORT: 'STOCK_REPORT',
  IMPORT_EXPORT: 'IMPORT_EXPORT',
  USER: 'USER',
  ROLE: 'ROLE',
  API_LOG: 'API_LOG',
} as const;
export type ActivityCode = (typeof ACTIVITY)[keyof typeof ACTIVITY];

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  /** 409 "Không đủ hàng": the lines that are short */
  shortages?: StockShortage[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ---- Auth / permissions ----
export interface CrudFlags {
  c: boolean;
  r: boolean;
  u: boolean;
  d: boolean;
}

export interface PermissionDto extends CrudFlags {
  code: string;
}

export interface CurrentUserDto {
  id: string;
  email: string;
  fullName: string;
  isAdmin: boolean;
  /** role names */
  roles: string[];
  /** effective permissions (only activities with at least one flag set) */
  permissions: PermissionDto[];
}

export interface UserSummaryDto {
  id: string;
  fullName: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: CurrentUserDto;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

// ---- Users / roles / activities ----
export interface RoleRefDto {
  id: string;
  name: string;
}

export interface UserListItemDto {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  roles: RoleRefDto[];
  createdDate: string;
}

export interface UsersQuery {
  search?: string;
  roleId?: string;
  page?: number;
  pageSize?: number;
}

export interface UpdateUserRequest {
  isActive: boolean;
}

export interface ActivityPermissionInput extends CrudFlags {
  activityId: string;
}

export interface ActivityPermissionDto extends CrudFlags {
  activityId: string;
  code: string;
  name: string;
}

export interface UserPermissionDetailDto {
  userId: string;
  isAdmin: boolean;
  roles: RoleRefDto[];
  /** permissions granted directly to the account (UserActivity) */
  userActivities: ActivityPermissionDto[];
  /** effective permissions = roles OR user-specific */
  effective: ActivityPermissionDto[];
}

export interface ActivityDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
}

export interface RoleDto {
  id: string;
  name: string;
  description: string | null;
  isAdmin: boolean;
  userCount: number;
}

export interface RoleDetailDto extends RoleDto {
  activities: ActivityPermissionDto[];
}

export interface RoleRequest {
  name: string;
  description?: string | null;
  activities: ActivityPermissionInput[];
}

// ---- Notifications ----
export interface NotificationDto {
  id: number;
  message: string;
  /** FE route to open, e.g. /stock?belowThreshold=true */
  link: string | null;
  isRead: boolean;
  createdAt: string;
}

// ---- API logs (API_LOG:R) ----
export interface ApiLogListItemDto {
  id: number;
  module: string;
  traceId: string;
  ip: string | null;
  userId: string | null;
  userName: string | null;
  method: string;
  url: string;
  statusCode: number;
  durationMs: number;
  createdDate: string;
}

export interface ApiLogDetailDto extends ApiLogListItemDto {
  request: string | null;
  response: string | null;
  userAgent: string | null;
}

export interface ApiLogsQuery {
  traceId?: string;
  userId?: string;
  url?: string;
  method?: string;
  statusCode?: number;
  /** ISO date-time (the API compares against CreatedDate as DateTime) */
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

// ---- Catalog ----
export interface ProductDto {
  id: number;
  sku: string;
  name: string;
  unit: string;
  groupId: number;
  groupName: string;
  cost: number;
  price: number;
  imageUrl: string | null;
  isActive: boolean;
}

export interface ProductRequest {
  sku: string;
  name: string;
  unit: string;
  groupId: number;
  cost: number;
  price: number;
  imageUrl?: string | null;
  isActive: boolean;
}

export interface ProductsQuery {
  search?: string;
  groupId?: number;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ProductGroupDto {
  id: number;
  name: string;
}

export interface WarehouseDto {
  id: number;
  code: string;
  name: string;
  address: string | null;
  isActive: boolean;
}

export interface WarehouseRequest {
  code: string;
  name: string;
  address?: string | null;
  isActive: boolean;
}

export interface SupplierDto {
  id: number;
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
}

export interface SupplierRequest {
  name: string;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
}

// ---- Stock ----
export interface StockCellDto {
  warehouseId: number;
  quantity: number;
  minThreshold: number;
  isLow: boolean;
}

export interface StockRowDto {
  productId: number;
  sku: string;
  name: string;
  unit: string;
  groupName: string;
  total: number;
  isLow: boolean;
  warehouses: StockCellDto[];
}

export interface StockQuery {
  warehouseId?: number;
  groupId?: number;
  search?: string;
  belowThreshold?: boolean;
  pageSize?: number;
}

export interface AvailableStockDto {
  productId: number;
  quantity: number;
}

export interface StockThresholdRequest {
  productId: number;
  warehouseId: number;
  minThreshold: number;
}

export interface StockShortage {
  productId: number;
  sku: string | null;
  warehouseId: number;
  available: number;
  requested: number;
}

// ---- Stock documents ----
export const DOCUMENT_TYPES = ['GoodsReceipt', 'GoodsIssue', 'Transfer', 'StockTake'] as const;
export type DocumentType = (typeof DOCUMENT_TYPES)[number];

export const DOCUMENT_STATUSES = ['Draft', 'Posted', 'Cancelled'] as const;
export type DocumentStatus = (typeof DOCUMENT_STATUSES)[number];

export const ISSUE_REASONS = ['Sale', 'Disposal', 'Production', 'Other'] as const;
export type IssueReason = (typeof ISSUE_REASONS)[number];

export interface RefDto {
  id: number;
  name: string;
}

export interface DocumentLineInput {
  productId: number;
  /** stock take: counted quantity */
  quantity: number;
  unitCost?: number | null;
  note?: string | null;
}

export interface StockDocumentLineDto {
  id: number;
  productId: number;
  sku: string;
  productName: string;
  unit: string;
  quantity: number;
  unitCost: number | null;
  systemQuantity: number | null;
  difference: number | null;
  note: string | null;
}

export interface StockDocumentDto {
  id: number;
  code: string;
  type: DocumentType;
  status: DocumentStatus;
  warehouse: RefDto;
  toWarehouse: RefDto | null;
  supplier: RefDto | null;
  reason: IssueReason | null;
  note: string | null;
  createdDate: string;
  createdName: string | null;
  postedAt: string | null;
  postedByName: string | null;
  rowVersion: string;
  lines: StockDocumentLineDto[];
}

export interface StockDocumentListItemDto {
  id: number;
  code: string;
  type: DocumentType;
  status: DocumentStatus;
  warehouseName: string;
  toWarehouseName: string | null;
  supplierName: string | null;
  reason: IssueReason | null;
  lineCount: number;
  totalQuantity: number;
  createdDate: string;
  createdName: string | null;
  postedAt: string | null;
}

export interface StockDocumentsQuery {
  status?: DocumentStatus;
  warehouseId?: number;
  search?: string;
  /** yyyy-MM-dd */
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

/** Body of POST /goods-receipts | /goods-issues | /transfers | /stock-takes (fields depend on type). */
export interface CreateDocumentRequest {
  warehouseId?: number;
  fromWarehouseId?: number;
  toWarehouseId?: number;
  supplierId?: number;
  reason?: IssueReason;
  note?: string | null;
  lines: DocumentLineInput[];
  post: boolean;
}

// ---- Reports ----
export type MovementType = 'In' | 'Out' | 'TransferIn' | 'TransferOut' | 'Adjust';

export interface KardexRowDto {
  id: number;
  occurredAt: string;
  documentId: number;
  documentCode: string;
  documentType: DocumentType;
  movementType: MovementType;
  warehouseId: number;
  warehouseCode: string;
  inQty: number;
  outQty: number;
  balance: number;
}

export interface KardexDto {
  productId: number;
  sku: string;
  productName: string;
  unit: string;
  warehouseId: number | null;
  /** yyyy-MM-dd (business-local day) */
  from: string;
  to: string;
  opening: number;
  totalIn: number;
  totalOut: number;
  closing: number;
  totalCount: number;
  page: number;
  pageSize: number;
  rows: KardexRowDto[];
}

export interface KardexQuery {
  productId: number;
  warehouseId?: number;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface DashboardDto {
  generatedAt: string;
  stockValue: number;
  productsInStock: number;
  lowStockCount: number;
  draftDocuments: number;
  postedToday: number;
  slowMovingDays: number;
  valueByWarehouse: { warehouseId: number; code: string; name: string; value: number; quantity: number }[];
  valueByGroup: { groupId: number; name: string; value: number }[];
  topLowStock: {
    productId: number;
    sku: string;
    name: string;
    warehouseId: number;
    warehouseCode: string;
    quantity: number;
    minThreshold: number;
  }[];
  inOutByDay: { date: string; inValue: number; outValue: number }[];
  slowMoving: { productId: number; sku: string; name: string; quantity: number; value: number; lastOutAt: string | null }[];
}

// ---- Import / export / alerts ----
export interface RowErrorDto {
  /** row number as seen in Excel */
  row: number;
  column: string | null;
  message: string;
}

export interface ImportProductsResultDto {
  dryRun: boolean;
  totalRows: number;
  validRows: number;
  created: number;
  updated: number;
  groupsCreated: string[];
  errors: RowErrorDto[];
}

export interface ParsedLineDto {
  row: number;
  productId: number;
  sku: string;
  productName: string;
  unit: string;
  productCost: number;
  quantity: number;
  unitCost: number | null;
  note: string | null;
}

export interface ParsedLinesDto {
  lines: ParsedLineDto[];
  errors: RowErrorDto[];
}

export type ImportTemplateKind = 'Products' | 'DocumentLines';

export interface StockAlertDto {
  id: number;
  productId: number;
  sku: string;
  productName: string;
  warehouseId: number;
  warehouseCode: string;
  quantityAtAlert: number;
  currentQuantity: number | null;
  minThreshold: number;
  createdAt: string;
  isResolved: boolean;
  resolvedAt: string | null;
}

export interface ScanLowStockResultDto {
  opened: number;
  resolved: number;
  stillOpen: number;
  notified: number;
}
