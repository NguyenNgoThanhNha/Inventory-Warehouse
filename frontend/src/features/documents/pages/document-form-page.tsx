import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, Save, Send } from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Form, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Combobox } from '@/components/common/combobox';
import { REQUIRED_LABEL_CLASS, SelectFormField, TextareaFormField } from '@/components/common/form-fields';
import { PageHeader } from '@/components/common/page-header';
import { UnsavedChangesGuard } from '@/components/common/unsaved-changes-guard';
import { useSuppliers, useWarehouses } from '@/features/catalog';
import { useAvailableStock } from '@/features/stock';
import { applyFieldErrors, getProblem, getStatus, showError } from '@/lib/api-errors';
import { formatQty } from '@/lib/format';
import { useCan } from '@/stores/auth-store';
import { ISSUE_REASONS, type DocumentType, type StockDocumentDto, type StockShortage } from '@/types';
import { LinesEditor } from '../components/lines-editor';
import { DOCUMENT_CONFIG, newIdempotencyKey, REASON_LABEL } from '../config';
import { useCreateDocument, useUpdateDocument } from '../hooks/use-documents';
import {
  documentSchema,
  emptyDocumentForm,
  findOverStock,
  fromDocument,
  toCreateRequest,
  toUpdateRequest,
  type DocumentFormValues,
} from '../schemas';

/** localStorage can be unavailable (private mode, blocked storage) — the form works without it. */
const readLocal = (key: string) => {
  try {
    return localStorage.getItem(key) ?? undefined;
  } catch {
    return undefined;
  }
};
const writeLocal = (key: string, value: string) => {
  try {
    localStorage.setItem(key, value);
  } catch {
    // ignore: only a convenience
  }
};

const HEADER_FIELDS = ['warehouseId', 'toWarehouseId', 'supplierId', 'reason', 'note', 'lines'] as const;

/** Lập / sửa phiếu (spec §4.2): 4 loại phiếu dùng chung một form, cột/field thay đổi theo loại. `editing` = sửa phiếu nháp. */
export function DocumentFormPage({ type, editing }: { type: DocumentType; editing?: StockDocumentDto }) {
  const cfg = DOCUMENT_CONFIG[type];
  const navigate = useNavigate();
  const canPost = useCan(cfg.activity, 'U');
  const create = useCreateDocument(cfg);
  const update = useUpdateDocument(cfg);
  const pending = create.isPending || update.isPending;
  // One key per form session: a double click or a retry after a network error returns the same document.
  const [idempotencyKey] = useState(newIdempotencyKey);
  const [shortages, setShortages] = useState<StockShortage[]>([]);

  const form = useForm<DocumentFormValues>({
    resolver: zodResolver(documentSchema(type)),
    defaultValues: editing ? fromDocument(editing) : emptyDocumentForm(),
  });

  const { data: warehouses = [] } = useWarehouses();
  const { data: suppliers } = useSuppliers({ pageSize: 100 }, type === 'GoodsReceipt');
  const warehouseId = Number(useWatch({ control: form.control, name: 'warehouseId' })) || undefined;
  const lines = useWatch({ control: form.control, name: 'lines' });
  const productIds = useMemo(() => lines.flatMap((l) => (l.product ? [l.product.id] : [])), [lines]);

  const available = useAvailableStock(type === 'GoodsReceipt' ? undefined : warehouseId, productIds);
  const overStock = cfg.consumesStock ? findOverStock(lines, available.data) : new Map<number, number>();

  // Nhớ kho dùng lần trước (mỗi loại phiếu) — thủ kho thường làm việc ở một kho.
  const lastWarehouseKey = `inventory:last-warehouse:${type}`;
  useEffect(() => {
    if (editing || !warehouses.length || form.getValues('warehouseId')) return;
    const remembered = readLocal(lastWarehouseKey);
    const fallback = warehouses.length === 1 ? String(warehouses[0].id) : undefined;
    const pick = warehouses.some((w) => String(w.id) === remembered) ? remembered : fallback;
    if (pick) form.setValue('warehouseId', pick); // not dirty: a prefill is not a user change
  }, [editing, warehouses, form, lastWarehouseKey]);

  // Chặn rời trang khi đang nhập dở (đọc qua ref lúc điều hướng: lưu xong điều hướng ngay thì không bị chặn).
  const saved = useRef(false);
  const dirty = useRef(false);
  dirty.current = form.formState.isDirty;
  const shouldBlock = useCallback(() => dirty.current && !saved.current, []);

  const warehouseOptions = warehouses.map((w) => ({ value: String(w.id), label: `${w.code} — ${w.name}` }));

  const handleError = (err: unknown) => {
    const problem = getProblem(err);
    if (getStatus(err) === 409 && problem?.shortages?.length) {
      // someone took the stock in the meantime: show fresh numbers on the affected lines
      setShortages(problem.shortages);
      void available.refetch();
      form.getValues('lines').forEach((l, i) => {
        const s = problem.shortages!.find((x) => x.productId === l.product?.id);
        if (s) form.setError(`lines.${i}.quantity`, { type: 'server', message: `Không đủ hàng — chỉ còn ${formatQty(s.available)}` });
      });
      return;
    }
    if (!applyFieldErrors(err, HEADER_FIELDS, form.setError)) showError(err);
  };

  const submit = (post: boolean) =>
    form.handleSubmit((values) => {
      setShortages([]);
      if (overStock.size > 0) {
        toast.error('Có dòng vượt tồn hiện tại. Sửa số lượng trước khi lưu.');
        return;
      }
      if (editing) {
        update.mutate(
          { id: editing.id, body: toUpdateRequest(type, values, editing.rowVersion) },
          {
            onSuccess: (doc) => {
              saved.current = true;
              toast.success(`Đã lưu ${doc.code}`);
              navigate(`/${cfg.path}/${doc.id}`, { replace: true });
            },
            onError: handleError,
          },
        );
        return;
      }
      create.mutate(
        { body: toCreateRequest(type, values, post), idempotencyKey },
        {
          onSuccess: (doc) => {
            saved.current = true;
            writeLocal(lastWarehouseKey, values.warehouseId);
            toast.success(post ? `Đã ghi sổ ${doc.code}` : `Đã lưu nháp ${doc.code}`);
            navigate(`/${cfg.path}/${doc.id}`, { replace: true });
          },
          onError: handleError,
        },
      );
    });

  return (
    <div className="space-y-4">
      <UnsavedChangesGuard shouldBlock={shouldBlock} />
      <PageHeader
        title={editing ? `Sửa ${cfg.noun} ${editing.code}` : `Lập ${cfg.noun}`}
        description={
          editing
            ? 'Chỉ phiếu nháp mới sửa được. Lưu xong, duyệt / ghi sổ ở trang chi tiết phiếu.'
            : canPost
            ? '“Ghi sổ” thay đổi tồn kho ngay. “Lưu nháp” để người khác kiểm tra và duyệt sau.'
            : 'Bạn lập phiếu nháp; quản lý kho sẽ duyệt và ghi sổ.'
        }
        actions={
          <Button variant="ghost" asChild>
            <Link to={editing ? `/${cfg.path}/${editing.id}` : `/${cfg.path}`}>
              <ArrowLeft /> {editing ? 'Chi tiết phiếu' : 'Danh sách'}
            </Link>
          </Button>
        }
      />

      <Form {...form}>
        <form noValidate onSubmit={(e) => e.preventDefault()} className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Thông tin phiếu</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-3">
              <SelectFormField
                control={form.control}
                name="warehouseId"
                label={type === 'Transfer' ? 'Kho xuất' : 'Kho'}
                required
                options={warehouseOptions}
              />
              {type === 'Transfer' && (
                <SelectFormField control={form.control} name="toWarehouseId" label="Kho nhận" required options={warehouseOptions} />
              )}
              {type === 'GoodsIssue' && (
                <SelectFormField
                  control={form.control}
                  name="reason"
                  label="Lý do xuất"
                  required
                  options={ISSUE_REASONS.map((r) => ({ value: r, label: REASON_LABEL[r] }))}
                />
              )}
              {type === 'GoodsReceipt' && (
                <FormField
                  control={form.control}
                  name="supplierId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel className={REQUIRED_LABEL_CLASS}>Nhà cung cấp</FormLabel>
                      <Combobox
                        aria-label="Nhà cung cấp"
                        placeholder="Chọn nhà cung cấp..."
                        options={(suppliers?.items ?? []).map((s) => ({ value: String(s.id), label: s.name }))}
                        value={field.value || undefined}
                        onChange={(v) => field.onChange(v ?? '')}
                      />
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
              <div className="md:col-span-3">
                <TextareaFormField control={form.control} name="note" label="Ghi chú" rows={2} maxLength={500} />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Dòng hàng</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {shortages.length > 0 && (
                <Alert variant="destructive">
                  <AlertDescription>
                    Tồn kho vừa thay đổi (có người khác vừa xuất hàng). Các dòng tô đỏ đã được cập nhật số tồn mới — hãy
                    sửa số lượng rồi lưu lại.
                  </AlertDescription>
                </Alert>
              )}
              {type !== 'GoodsReceipt' && !warehouseId && (
                <p className="text-sm text-muted-foreground">Chọn kho để xem tồn hiện tại của từng dòng.</p>
              )}
              <LinesEditor type={type} available={available.data} availableLoading={available.isFetching} overStock={overStock} />
              {form.formState.errors.lines?.root?.message || form.formState.errors.lines?.message ? (
                <p className="text-sm text-destructive">{form.formState.errors.lines?.root?.message ?? form.formState.errors.lines?.message}</p>
              ) : null}
            </CardContent>
          </Card>

          <div className="sticky bottom-0 z-10 -mx-3 flex justify-end gap-2 border-t bg-background/95 px-3 py-3 backdrop-blur sm:-mx-6 sm:px-6">
            <Button
              type="button"
              variant={canPost && !editing ? 'outline' : 'default'}
              disabled={pending}
              onClick={() => void submit(false)()}
            >
              {update.isPending || (create.isPending && !create.variables?.body.post) ? <Loader2 className="animate-spin" /> : <Save />}
              {editing ? 'Lưu thay đổi' : 'Lưu nháp'}
            </Button>
            {canPost && !editing && (
              <Button type="button" disabled={pending || overStock.size > 0} onClick={() => void submit(true)()}>
                {create.isPending && create.variables?.body.post ? <Loader2 className="animate-spin" /> : <Send />}
                Ghi sổ
              </Button>
            )}
          </div>
        </form>
      </Form>
    </div>
  );
}
