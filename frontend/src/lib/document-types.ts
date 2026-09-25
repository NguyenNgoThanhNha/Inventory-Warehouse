/**
 * Document types (route segment, permission activity, labels) — shared metadata: the router and sidebar need it
 * without pulling the documents feature into the entry chunk. The documents feature re-exports it as ./config.
 */
import type { LucideIcon } from 'lucide-react';
import { ArrowDownToLine, ArrowLeftRight, ArrowUpFromLine, ClipboardCheck } from 'lucide-react';
import type { DocumentStatus, DocumentType, IssueReason } from '@/types';

export interface DocumentTypeConfig {
  type: DocumentType;
  /** API + FE route segment */
  path: 'goods-receipts' | 'goods-issues' | 'transfers' | 'stock-takes';
  /** permission activity code */
  activity: 'GOODS_RECEIPT' | 'GOODS_ISSUE' | 'TRANSFER' | 'STOCK_TAKE';
  title: string;
  /** "phiếu nhập" — used in sentences */
  noun: string;
  icon: LucideIcon;
  /** true when posting lowers stock at the source warehouse → the form checks quantity against current stock */
  consumesStock: boolean;
}

export const DOCUMENT_CONFIG: Record<DocumentType, DocumentTypeConfig> = {
  GoodsReceipt: {
    type: 'GoodsReceipt',
    path: 'goods-receipts',
    activity: 'GOODS_RECEIPT',
    title: 'Phiếu nhập kho',
    noun: 'phiếu nhập',
    icon: ArrowDownToLine,
    consumesStock: false,
  },
  GoodsIssue: {
    type: 'GoodsIssue',
    path: 'goods-issues',
    activity: 'GOODS_ISSUE',
    title: 'Phiếu xuất kho',
    noun: 'phiếu xuất',
    icon: ArrowUpFromLine,
    consumesStock: true,
  },
  Transfer: {
    type: 'Transfer',
    path: 'transfers',
    activity: 'TRANSFER',
    title: 'Chuyển kho',
    noun: 'phiếu chuyển kho',
    icon: ArrowLeftRight,
    consumesStock: true,
  },
  StockTake: {
    type: 'StockTake',
    path: 'stock-takes',
    activity: 'STOCK_TAKE',
    title: 'Kiểm kê',
    noun: 'phiếu kiểm kê',
    icon: ClipboardCheck,
    consumesStock: false,
  },
};

export const DOCUMENT_TYPE_LIST = Object.values(DOCUMENT_CONFIG);

export const STATUS_LABEL: Record<DocumentStatus, string> = {
  Draft: 'Nháp',
  Posted: 'Đã ghi sổ',
  Cancelled: 'Đã hủy',
};

export const REASON_LABEL: Record<IssueReason, string> = {
  Sale: 'Bán hàng',
  Disposal: 'Hủy hàng',
  Production: 'Sản xuất',
  Other: 'Khác',
};

/** Idempotency-Key for one form session. crypto.randomUUID needs a secure context, so fall back when absent. */
export function newIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}
