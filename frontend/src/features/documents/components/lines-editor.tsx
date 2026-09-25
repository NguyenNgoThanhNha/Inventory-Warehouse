import { useFieldArray, useFormContext, useWatch } from 'react-hook-form';
import { Plus, Trash2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ProductPicker } from '@/features/catalog';
import { formatMoney, formatQty, formatSigned } from '@/lib/format';
import type { DocumentType } from '@/types';
import { emptyLine, MAX_LINES, type DocumentFormValues } from '../schemas';

interface LinesEditorProps {
  type: DocumentType;
  /** productId → current quantity at the source warehouse (undefined while unknown) */
  available: Map<number, number> | undefined;
  availableLoading: boolean;
  /** line index → available quantity, for lines asking more than the stock */
  overStock: Map<number, number>;
}

/** Editable document lines (RHF field array). Columns depend on the document type. */
export function LinesEditor({ type, available, availableLoading, overStock }: LinesEditorProps) {
  const { control, setValue, getValues } = useFormContext<DocumentFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });
  const lines = useWatch({ control, name: 'lines' });

  const showAvailable = type !== 'GoodsReceipt';
  const isReceipt = type === 'GoodsReceipt';
  const isStockTake = type === 'StockTake';
  const pickedIds = lines.flatMap((l) => (l.product ? [l.product.id] : []));
  const totalQty = lines.reduce((s, l) => s + (Number(l.quantity) || 0), 0);
  const totalAmount = lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.unitCost) || 0), 0);

  const availableText = (productId: number | undefined) => {
    if (!productId) return '—';
    if (availableLoading && !available?.has(productId)) return '…';
    return formatQty(available?.get(productId) ?? 0);
  };

  return (
    <div className="space-y-3">
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
              const line = lines[index];
              const over = overStock.get(index);
              const productId = line?.product?.id;
              const diff =
                isStockTake && productId && available?.has(productId) && line.quantity.trim() !== ''
                  ? Number(line.quantity) - (available.get(productId) ?? 0)
                  : null;
              return (
                <TableRow key={field.id} data-over={over !== undefined || undefined} className={cn(over !== undefined && 'bg-red-50/70 dark:bg-red-950/20')}>
                  <TableCell className="align-top pt-4 text-muted-foreground">{index + 1}</TableCell>
                  <TableCell className="align-top">
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
                            }}
                          />
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </TableCell>
                  {showAvailable && (
                    <TableCell className="align-top pt-4 text-right tabular-nums text-muted-foreground">
                      {availableText(productId)}
                      {productId && <span className="ml-1 text-xs">{line.product?.unit}</span>}
                    </TableCell>
                  )}
                  <TableCell className="align-top">
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
                              aria-label={`Số lượng dòng ${index + 1}`}
                              aria-invalid={!!fieldState.error || over !== undefined || undefined}
                              className={cn('text-right tabular-nums', over !== undefined && 'border-destructive text-destructive')}
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
                  </TableCell>
                  {isStockTake && (
                    <TableCell
                      className={cn(
                        'align-top pt-4 text-right font-medium tabular-nums',
                        diff !== null && diff < 0 && 'text-red-600 dark:text-red-400',
                        diff !== null && diff > 0 && 'text-emerald-600 dark:text-emerald-400',
                      )}
                    >
                      {formatSigned(diff)}
                    </TableCell>
                  )}
                  {isReceipt && (
                    <TableCell className="align-top">
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
                    </TableCell>
                  )}
                  {isReceipt && (
                    <TableCell className="align-top pt-4 text-right tabular-nums">
                      {formatMoney((Number(line?.quantity) || 0) * (Number(line?.unitCost) || 0))}
                    </TableCell>
                  )}
                  <TableCell className="align-top">
                    <FormField
                      control={control}
                      name={`lines.${index}.note`}
                      render={({ field: f }) => (
                        <FormItem>
                          <FormControl>
                            <Input {...f} aria-label={`Ghi chú dòng ${index + 1}`} maxLength={200} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </TableCell>
                  <TableCell className="align-top pt-3">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      aria-label={`Xóa dòng ${index + 1}`}
                      disabled={fields.length === 1}
                      onClick={() => remove(index)}
                    >
                      <Trash2 />
                    </Button>
                  </TableCell>
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
      <Button type="button" variant="outline" size="sm" disabled={fields.length >= MAX_LINES} onClick={() => append(emptyLine())}>
        <Plus /> Thêm dòng
      </Button>
    </div>
  );
}
