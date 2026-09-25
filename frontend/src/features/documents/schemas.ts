import { z } from 'zod';
import type { CreateDocumentRequest, DocumentType, StockDocumentDto, UpdateDocumentRequest } from '@/types';
import { ISSUE_REASONS } from '@/types';

export const MAX_LINES = 500;
const MAX_QTY = 1_000_000_000;

const pickedProduct = z.object({ id: z.number(), sku: z.string(), name: z.string(), unit: z.string(), cost: z.number() });

const lineSchema = z.object({
  product: pickedProduct.nullable(),
  quantity: z.string(),
  unitCost: z.string(),
  note: z.string().max(200, 'Tối đa 200 ký tự'),
});

const baseSchema = z.object({
  warehouseId: z.string(),
  toWarehouseId: z.string(),
  supplierId: z.string(),
  reason: z.enum(ISSUE_REASONS).or(z.literal('')),
  note: z.string().max(500, 'Tối đa 500 ký tự'),
  lines: z.array(lineSchema).min(1, 'Phiếu phải có ít nhất một dòng hàng').max(MAX_LINES, `Tối đa ${MAX_LINES} dòng`),
});

export type DocumentFormValues = z.infer<typeof baseSchema>;
export type DocumentLineValues = z.infer<typeof lineSchema>;

export const emptyLine = (): DocumentLineValues => ({ product: null, quantity: '', unitCost: '', note: '' });

export const emptyDocumentForm = (): DocumentFormValues => ({
  warehouseId: '',
  toWarehouseId: '',
  supplierId: '',
  reason: '',
  note: '',
  lines: [emptyLine()],
});

const toNumber = (v: string) => (v.trim() === '' ? NaN : Number(v));

/**
 * Per-type rules on top of the shared shape: which header fields are required, quantity > 0
 * (stock take: counted ≥ 0), no duplicate products. Stock sufficiency is checked separately (needs server data).
 */
export function documentSchema(type: DocumentType) {
  return baseSchema.superRefine((v, ctx) => {
    const required = (path: keyof DocumentFormValues, message: string) => {
      if (!v[path]) ctx.addIssue({ code: z.ZodIssueCode.custom, path: [path], message });
    };

    required('warehouseId', type === 'Transfer' ? 'Chọn kho xuất' : 'Chọn kho');
    if (type === 'GoodsReceipt') required('supplierId', 'Chọn nhà cung cấp');
    if (type === 'GoodsIssue') required('reason', 'Chọn lý do xuất');
    if (type === 'Transfer') {
      required('toWarehouseId', 'Chọn kho nhận');
      if (v.toWarehouseId && v.toWarehouseId === v.warehouseId)
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['toWarehouseId'], message: 'Kho nhận phải khác kho xuất' });
    }

    const seen = new Set<number>();
    v.lines.forEach((line, i) => {
      if (!line.product) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['lines', i, 'product'], message: 'Chọn sản phẩm' });
      } else if (seen.has(line.product.id)) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['lines', i, 'product'], message: 'Sản phẩm bị lặp' });
      } else {
        seen.add(line.product.id);
      }

      const qty = toNumber(line.quantity);
      const okQty = type === 'StockTake' ? qty >= 0 : qty > 0;
      if (!Number.isFinite(qty) || !okQty || qty > MAX_QTY)
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['lines', i, 'quantity'],
          message: type === 'StockTake' ? 'Nhập số đếm (≥ 0)' : 'Số lượng phải > 0',
        });

      if (type === 'GoodsReceipt' && line.unitCost.trim() !== '') {
        const cost = toNumber(line.unitCost);
        if (!Number.isFinite(cost) || cost < 0)
          ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['lines', i, 'unitCost'], message: 'Đơn giá không hợp lệ' });
      }
    });
  });
}

/** Form values (already validated) → API body for the given type. */
export function toCreateRequest(type: DocumentType, v: DocumentFormValues, post: boolean): CreateDocumentRequest {
  const lines = v.lines.map((l) => ({
    productId: l.product!.id,
    quantity: Number(l.quantity),
    unitCost: type === 'GoodsReceipt' && l.unitCost.trim() !== '' ? Number(l.unitCost) : null,
    note: l.note.trim() || null,
  }));
  const note = v.note.trim() || null;
  const warehouseId = Number(v.warehouseId);

  switch (type) {
    case 'GoodsReceipt':
      return { warehouseId, supplierId: Number(v.supplierId), note, lines, post };
    case 'GoodsIssue':
      return { warehouseId, reason: v.reason || undefined, note, lines, post };
    case 'Transfer':
      return { fromWarehouseId: warehouseId, toWarehouseId: Number(v.toWarehouseId), note, lines, post };
    case 'StockTake':
      return { warehouseId, note, lines, post };
  }
}

/** Lines whose quantity exceeds the current stock (issue / transfer). Returns line index → available. */
export function findOverStock(lines: DocumentLineValues[], available: Map<number, number> | undefined): Map<number, number> {
  const over = new Map<number, number>();
  if (!available) return over;
  lines.forEach((l, i) => {
    if (!l.product) return;
    const qty = toNumber(l.quantity);
    const have = available.get(l.product.id) ?? 0;
    if (Number.isFinite(qty) && qty > have) over.set(i, have);
  });
  return over;
}

/** A draft loaded from the API → form values (edit mode). */
export function fromDocument(doc: StockDocumentDto): DocumentFormValues {
  return {
    warehouseId: String(doc.warehouse.id),
    toWarehouseId: doc.toWarehouse ? String(doc.toWarehouse.id) : '',
    supplierId: doc.supplier ? String(doc.supplier.id) : '',
    reason: doc.reason ?? '',
    note: doc.note ?? '',
    lines: doc.lines.map((l) => ({
      product: { id: l.productId, sku: l.sku, name: l.productName, unit: l.unit, cost: l.unitCost ?? 0 },
      quantity: String(l.quantity),
      unitCost: l.unitCost === null ? '' : String(l.unitCost),
      note: l.note ?? '',
    })),
  };
}

/** Form values → PUT body for editing a draft. */
export function toUpdateRequest(type: DocumentType, v: DocumentFormValues, rowVersion: string): UpdateDocumentRequest {
  const create = toCreateRequest(type, v, false);
  return {
    rowVersion,
    warehouseId: create.warehouseId ?? create.fromWarehouseId!,
    toWarehouseId: create.toWarehouseId ?? null,
    supplierId: create.supplierId ?? null,
    reason: create.reason ?? null,
    note: create.note ?? null,
    lines: create.lines,
  };
}
