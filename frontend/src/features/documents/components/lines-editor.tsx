import { useRef, useState, type KeyboardEvent } from 'react';
import { useFieldArray, useFormContext, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { Loader2, Plus, ScanBarcode, Trash2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { RowErrors } from '@/components/common/row-errors';
import { findProductBySku, ProductPicker } from '@/features/catalog';
import { formatMoney, formatQty, formatSigned } from '@/lib/format';
import { useIsMobile } from '@/lib/hooks/use-mobile';
import type { DocumentType, ParsedLinesDto, RowErrorDto } from '@/types';
import { emptyLine, MAX_LINES, type DocumentFormValues, type DocumentLineValues } from '../schemas';
import { ImportLinesButton } from './import-lines-button';

interface LinesEditorProps {
  type: DocumentType;
  /** productId → current quantity at the source warehouse (undefined while unknown) */
  available: Map<number, number> | undefined;
  availableLoading: boolean;
  /** line index → available quantity, for lines asking more than the stock */
  overStock: Map<number, number>;
}

/**
 * Editable document lines (RHF field array). Columns depend on the document type.
 * Keyboard / scanner flow: "Quét mã" (Enter) adds a line or bumps the quantity of an existing one; picking a product
 * moves focus to its quantity; Enter in a quantity goes back to the scan box. Desktop = table, mobile = one card per line.
 */
export function LinesEditor({ type, available, availableLoading, overStock }: LinesEditorProps) {
  const { control, setValue, getValues, setFocus } = useFormContext<DocumentFormValues>();
  const { fields, append, remove, replace, update } = useFieldArray({ control, name: 'lines' });
  const lines = useWatch({ control, name: 'lines' });
  const isMobile = useIsMobile();
  const [importErrors, setImportErrors] = useState<RowErrorDto[]>([]);
  const scanRef = useRef<HTMLInputElement>(null);
  const [scan, setScan] = useState('');
  const [scanning, setScanning] = useState(false);
  const [scanStatus, setScanStatus] = useState<{ ok: boolean; text: string } | null>(null);

  const showAvailable = type !== 'GoodsReceipt';
  const isReceipt = type === 'GoodsReceipt';
  const isStockTake = type === 'StockTake';
  const pickedIds = lines.flatMap((l) => (l.product ? [l.product.id] : []));
  const totalQty = lines.reduce((s, l) => s + (Number(l.quantity) || 0), 0);
  const totalAmount = lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.unitCost) || 0), 0);

  const focusQuantity = (index: number) => setTimeout(() => setFocus(`lines.${index}.quantity`, { shouldSelect: true }), 0);

  /** Scan / type an exact SKU: existing line → quantity + 1, otherwise fill the first empty line or append one. */
  const addBySku = async () => {
    const sku = scan.trim().toUpperCase();
    if (!sku || scanning) return;
    setScanning(true);
    try {
      const product = await findProductBySku(sku);
      if (!product) {
        setScanStatus({ ok: false, text: `Không tìm thấy ${sku} (hoặc đã ngừng kinh doanh)` });
        return;
      }
      const current = getValues('lines');
      const existing = current.findIndex((l) => l.product?.id === product.id);
      if (existing >= 0) {
        const qty = (Number(current[existing].quantity) || 0) + 1;
        setValue(`lines.${existing}.quantity`, String(qty), { shouldDirty: true, shouldValidate: true });
        setScanStatus({ ok: true, text: `${product.sku}: số lượng → ${formatQty(qty)}` });
      } else if (current.length >= MAX_LINES && current.every((l) => l.product)) {
        setScanStatus({ ok: false, text: `Tối đa ${MAX_LINES} dòng mỗi phiếu` });
        return;
      } else {
        const line: DocumentLineValues = {
          product: { id: product.id, sku: product.sku, name: product.name, unit: product.unit, cost: product.cost },
          quantity: isStockTake ? '' : '1',
          unitCost: isReceipt ? String(product.cost) : '',
          note: '',
        };
        const empty = current.findIndex((l) => !l.product);
        if (empty >= 0) update(empty, line);
        else append(line, { shouldFocus: false });
        setScanStatus({ ok: true, text: `Đã thêm ${product.sku} — ${product.name}` });
        // stock take: the counted number must be typed, so go straight to it
        if (isStockTake) focusQuantity(empty >= 0 ? empty : current.length);
      }
      setScan('');
    } catch {
      setScanStatus({ ok: false, text: 'Không tra được sản phẩm, thử lại' });
    } finally {
      setScanning(false);
    }
  };

  /** Keep the lines already typed, append the imported ones; a product already in the form is reported, not duplicated. */
  const mergeImported = ({ lines: parsed, errors }: ParsedLinesDto) => {
    const current = getValues('lines').filter((l) => l.product);
    const taken = new Set(current.map((l) => l.product!.id));
    const duplicates: RowErrorDto[] = [];
    const added: DocumentLineValues[] = [];
    for (const p of parsed) {
      if (taken.has(p.productId)) {
        duplicates.push({ row: p.row, column: 'SKU', message: `${p.sku} đã có trong phiếu — bỏ qua.` });
        continue;
      }
      taken.add(p.productId);
      added.push({
        product: { id: p.productId, sku: p.sku, name: p.productName, unit: p.unit, cost: p.productCost },
        quantity: String(p.quantity),
        unitCost: isReceipt ? String(p.unitCost ?? p.productCost) : '',
        note: p.note ?? '',
      });
    }
    const merged = [...current, ...added].slice(0, MAX_LINES);
    replace(merged.length ? merged : [emptyLine()]);
    setImportErrors([...errors, ...duplicates].sort((a, b) => a.row - b.row));
    if (added.length) toast.success(`Đã thêm ${added.length} dòng từ Excel`);
  };

  const availableText = (productId: number | undefined) => {
    // no product, or no warehouse chosen yet (nothing is being fetched) → unknown, not "0"
    if (!productId || (!available && !availableLoading)) return '—';
    if (availableLoading && !available?.has(productId)) return '…';
    return formatQty(available?.get(productId) ?? 0);
  };

  const onQuantityKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      scanRef.current?.focus();
    }
  };

  // ---- field renderers shared by the table (desktop) and the cards (mobile) ----
  const productField = (index: number) => (
    <FormField
      control={control}
      name={`lines.${index}.product`}
      render={({ field: f, fieldState }) => (
        <FormItem>
          <ProductPicker
            aria-label={`Sản phẩm dòng ${index + 1}`}
            value={f.value}
            invalid={!!fieldState.error}
            excludeIds={pickedIds}
            onChange={(p) => {
              f.onChange(p);
              // receipt: prefill the unit cost with the product's standard cost
              if (isReceipt && !getValues(`lines.${index}.unitCost`)) setValue(`lines.${index}.unitCost`, String(p.cost));
              focusQuantity(index);
            }}
          />
          <FormMessage />
        </FormItem>
      )}
    />
  );

  const quantityField = (index: number, over: number | undefined) => (
    <FormField
      control={control}
      name={`lines.${index}.quantity`}
      render={({ field: f, fieldState }) => (
        <FormItem>
          <FormControl>
            <Input
              {...f}
              type="number"
              min={0}
              step="any"
              inputMode="decimal"
              enterKeyHint="next"
              aria-label={`Số lượng dòng ${index + 1}`}
              aria-invalid={!!fieldState.error || over !== undefined || undefined}
              className={cn('text-right tabular-nums', over !== undefined && 'border-destructive text-destructive')}
              onKeyDown={onQuantityKeyDown}
            />
          </FormControl>
          {over !== undefined && !fieldState.error && (
            <p className="text-xs text-destructive" role="alert">
              Vượt tồn — chỉ còn {formatQty(over)}
            </p>
          )}
          <FormMessage />
        </FormItem>
      )}
    />
  );

  const unitCostField = (index: number) => (
    <FormField
      control={control}
      name={`lines.${index}.unitCost`}
      render={({ field: f }) => (
        <FormItem>
          <FormControl>
            <Input {...f} type="number" min={0} step="any" inputMode="decimal" aria-label={`Đơn giá dòng ${index + 1}`} className="text-right tabular-nums" />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  );

  const noteField = (index: number) => (
    <FormField
      control={control}
      name={`lines.${index}.note`}
      render={({ field: f }) => (
        <FormItem>
          <FormControl>
            <Input {...f} aria-label={`Ghi chú dòng ${index + 1}`} placeholder={isMobile ? 'Ghi chú' : undefined} maxLength={200} />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  );

  const removeButton = (index: number) => (
    <Button type="button" variant="ghost" size="icon-sm" aria-label={`Xóa dòng ${index + 1}`} disabled={fields.length === 1} onClick={() => remove(index)}>
      <Trash2 />
    </Button>
  );

  const lineInfo = (index: number) => {
    const line = lines[index];
    const productId = line?.product?.id;
    const diff =
      isStockTake && productId && available?.has(productId) && line.quantity.trim() !== ''
        ? Number(line.quantity) - (available.get(productId) ?? 0)
        : null;
    return { line, productId, diff, over: overStock.get(index) };
  };

  const diffClass = (diff: number | null) =>
    cn(diff !== null && diff < 0 && 'text-red-600 dark:text-red-400', diff !== null && diff > 0 && 'text-emerald-600 dark:text-emerald-400');

  return (
    <div className="space-y-3">
      {/* Scanner / keyboard quick add */}
      <div className="flex flex-col gap-1.5 sm:flex-row sm:items-center">
        <div className="relative w-full sm:max-w-sm">
          <ScanBarcode className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            ref={scanRef}
            aria-label="Quét mã hoặc nhập SKU"
            placeholder="Quét mã / nhập SKU rồi Enter"
            className="pl-8 font-mono"
            autoComplete="off"
            enterKeyHint="done"
            value={scan}
            onChange={(e) => setScan(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                void addBySku();
              }
            }}
          />
          {scanning && <Loader2 className="absolute top-1/2 right-2.5 size-4 -translate-y-1/2 animate-spin text-muted-foreground" />}
        </div>
        <p
          role="status"
          aria-live="polite"
          className={cn('min-h-5 text-sm', scanStatus?.ok ? 'text-emerald-600 dark:text-emerald-400' : 'text-destructive')}
        >
          {scanStatus?.text}
        </p>
      </div>

      {isMobile ? (
        <ul className="space-y-3" aria-label="Dòng hàng">
          {fields.map((field, index) => {
            const { line, productId, diff, over } = lineInfo(index);
            return (
              <li
                key={field.id}
                data-over={over !== undefined || undefined}
                className={cn('space-y-2 rounded-lg border p-3', over !== undefined && 'border-destructive/50 bg-red-50/70 dark:bg-red-950/20')}
              >
                <div className="flex items-start gap-2">
                  <span className="pt-2 text-sm text-muted-foreground">{index + 1}.</span>
                  <div className="min-w-0 flex-1">{productField(index)}</div>
                  <div className="pt-0.5">{removeButton(index)}</div>
                </div>
                <div className="grid grid-cols-2 gap-2">
                  {showAvailable && (
                    <div className="text-sm">
                      <div className="text-xs text-muted-foreground">{isStockTake ? 'Tồn sổ sách' : 'Tồn hiện'}</div>
                      <div className="tabular-nums">
                        {availableText(productId)} {productId && available && <span className="text-xs text-muted-foreground">{line.product?.unit}</span>}
                      </div>
                    </div>
                  )}
                  <div>
                    <div className="mb-1 text-xs text-muted-foreground">{isStockTake ? 'Số đếm' : 'Số lượng'}</div>
                    {quantityField(index, over)}
                  </div>
                  {isStockTake && (
                    <div className="text-sm">
                      <div className="text-xs text-muted-foreground">Chênh lệch</div>
                      <div className={cn('font-medium tabular-nums', diffClass(diff))}>{formatSigned(diff)}</div>
                    </div>
                  )}
                  {isReceipt && (
                    <div>
                      <div className="mb-1 text-xs text-muted-foreground">Đơn giá (đ)</div>
                      {unitCostField(index)}
                    </div>
                  )}
                </div>
                {noteField(index)}
              </li>
            );
          })}
          <li className="flex justify-between rounded-lg bg-muted/50 px-3 py-2 text-sm font-medium">
            <span>Tổng ({fields.length} dòng)</span>
            <span className="tabular-nums">
              {formatQty(totalQty)}
              {isReceipt && ` · ${formatMoney(totalAmount)} đ`}
            </span>
          </li>
        </ul>
      ) : (
        <div className="overflow-x-auto rounded-md border">
          <Table aria-label="Dòng hàng">
            <TableHeader>
              <TableRow>
                <TableHead className="w-10">#</TableHead>
                <TableHead className="min-w-72">Sản phẩm</TableHead>
                {showAvailable && <TableHead className="w-28 text-right">{isStockTake ? 'Tồn sổ sách' : 'Tồn hiện'}</TableHead>}
                <TableHead className="w-36 text-right">{isStockTake ? 'Số đếm thực tế' : 'Số lượng'}</TableHead>
                {isStockTake && <TableHead className="w-28 text-right">Chênh lệch</TableHead>}
                {isReceipt && <TableHead className="w-40 text-right">Đơn giá (đ)</TableHead>}
                {isReceipt && <TableHead className="w-36 text-right">Thành tiền</TableHead>}
                <TableHead className="min-w-40">Ghi chú</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {fields.map((field, index) => {
                const { line, productId, diff, over } = lineInfo(index);
                return (
                  <TableRow key={field.id} data-over={over !== undefined || undefined} className={cn(over !== undefined && 'bg-red-50/70 dark:bg-red-950/20')}>
                    <TableCell className="pt-4 align-top text-muted-foreground">{index + 1}</TableCell>
                    <TableCell className="align-top">{productField(index)}</TableCell>
                    {showAvailable && (
                      <TableCell className="pt-4 text-right align-top tabular-nums text-muted-foreground">
                        {availableText(productId)}
                        {productId && available && <span className="ml-1 text-xs">{line.product?.unit}</span>}
                      </TableCell>
                    )}
                    <TableCell className="align-top">{quantityField(index, over)}</TableCell>
                    {isStockTake && <TableCell className={cn('pt-4 text-right align-top font-medium tabular-nums', diffClass(diff))}>{formatSigned(diff)}</TableCell>}
                    {isReceipt && <TableCell className="align-top">{unitCostField(index)}</TableCell>}
                    {isReceipt && (
                      <TableCell className="pt-4 text-right align-top tabular-nums">
                        {formatMoney((Number(line?.quantity) || 0) * (Number(line?.unitCost) || 0))}
                      </TableCell>
                    )}
                    <TableCell className="align-top">{noteField(index)}</TableCell>
                    <TableCell className="pt-3 align-top">{removeButton(index)}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell colSpan={showAvailable ? 3 : 2} className="text-right">
                  Tổng ({fields.length} dòng)
                </TableCell>
                <TableCell className="text-right tabular-nums">{formatQty(totalQty)}</TableCell>
                {isStockTake && <TableCell />}
                {isReceipt && <TableCell />}
                {isReceipt && <TableCell className="text-right tabular-nums">{formatMoney(totalAmount)}</TableCell>}
                <TableCell colSpan={2} />
              </TableRow>
            </TableFooter>
          </Table>
        </div>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <Button type="button" variant="outline" size="sm" disabled={fields.length >= MAX_LINES} onClick={() => append(emptyLine())}>
          <Plus /> Thêm dòng
        </Button>
        <ImportLinesButton type={type} onParsed={mergeImported} />
      </div>
      <RowErrors
        errors={importErrors}
        title={importErrors.length ? `${new Set(importErrors.map((e) => e.row)).size} dòng trong file không được thêm` : undefined}
      />
    </div>
  );
}
