import { useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Ban, FileQuestion, Send } from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { getProblem, getStatus, showError } from '@/lib/api-errors';
import { formatDateTime } from '@/lib/date';
import { formatMoney, formatQty, formatSigned } from '@/lib/format';
import { cn } from '@/lib/utils';
import { useCan } from '@/stores/auth-store';
import type { DocumentType, StockShortage } from '@/types';
import { DocumentStatusBadge } from '../components/document-badges';
import { DOCUMENT_CONFIG, REASON_LABEL } from '../config';
import { useCancelDocument, useDocument, usePostDocument } from '../hooks/use-documents';

function Info({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-0.5">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-sm font-medium">{children}</div>
    </div>
  );
}

/** Chi tiết phiếu + duyệt (ghi sổ) / hủy phiếu nháp, gửi kèm rowVersion. */
export function DocumentDetailPage({ type }: { type: DocumentType }) {
  const cfg = DOCUMENT_CONFIG[type];
  const id = Number(useParams().id) || undefined;
  const { data: doc, isPending, isError, error, refetch } = useDocument(cfg, id);
  const post = usePostDocument(cfg);
  const cancel = useCancelDocument(cfg);
  const canPost = useCan(cfg.activity, 'U');
  const canCancel = useCan(cfg.activity, 'D');
  const [confirm, setConfirm] = useState<'post' | 'cancel' | null>(null);
  const [shortages, setShortages] = useState<StockShortage[]>([]);

  if (isPending) {
    return (
      <div className="space-y-4" aria-busy>
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }
  if (isError || !doc) {
    return (
      <EmptyState
        icon={<FileQuestion />}
        title={getStatus(error) === 404 ? 'Không tìm thấy phiếu' : 'Không tải được phiếu'}
        action={
          <Button asChild>
            <Link to={`/${cfg.path}`}>Về danh sách</Link>
          </Button>
        }
      />
    );
  }

  const isDraft = doc.status === 'Draft';
  const isReceipt = type === 'GoodsReceipt';
  const isStockTake = type === 'StockTake';
  const shortageOf = (productId: number) => shortages.find((s) => s.productId === productId);
  const totalQty = doc.lines.reduce((s, l) => s + l.quantity, 0);
  const totalAmount = doc.lines.reduce((s, l) => s + l.quantity * (l.unitCost ?? 0), 0);

  const doPost = () =>
    post.mutateAsync(doc).then(
      (d) => {
        setShortages([]);
        toast.success(`Đã ghi sổ ${d.code}`);
      },
      (err: unknown) => {
        const problem = getProblem(err);
        if (problem?.shortages?.length) setShortages(problem.shortages);
        else if (getStatus(err) === 409) void refetch(); // stale rowVersion: someone changed the document
        showError(err);
        throw err;
      },
    );

  const doCancel = () => cancel.mutateAsync(doc).then((d) => toast.success(`Đã hủy ${d.code}`));

  return (
    <div className="space-y-4">
      <PageHeader
        title={
          <span className="flex flex-wrap items-center gap-2">
            {cfg.title} <span className="font-mono">{doc.code}</span> <DocumentStatusBadge status={doc.status} />
          </span>
        }
        actions={
          <>
            <Button variant="ghost" asChild>
              <Link to={`/${cfg.path}`}>
                <ArrowLeft /> Danh sách
              </Link>
            </Button>
            {isDraft && canCancel && (
              <Button variant="outline" onClick={() => setConfirm('cancel')}>
                <Ban /> Hủy phiếu
              </Button>
            )}
            {isDraft && canPost && (
              <Button onClick={() => setConfirm('post')}>
                <Send /> Ghi sổ
              </Button>
            )}
          </>
        }
      />

      {shortages.length > 0 && (
        <Alert variant="destructive">
          <AlertDescription>
            Không đủ hàng để ghi sổ:{' '}
            {shortages.map((s) => `${s.sku ?? `#${s.productId}`} còn ${formatQty(s.available)}, cần ${formatQty(s.requested)}`).join('; ')}.
            Hãy hủy phiếu này và lập phiếu mới với số lượng phù hợp.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Info label={type === 'Transfer' ? 'Kho xuất' : 'Kho'}>{doc.warehouse.name}</Info>
          {doc.toWarehouse && <Info label="Kho nhận">{doc.toWarehouse.name}</Info>}
          {doc.supplier && <Info label="Nhà cung cấp">{doc.supplier.name}</Info>}
          {doc.reason && <Info label="Lý do">{REASON_LABEL[doc.reason]}</Info>}
          <Info label="Người lập">
            {doc.createdName ?? 'Hệ thống'} · {formatDateTime(doc.createdDate)}
          </Info>
          {doc.postedAt && (
            <Info label="Ghi sổ">
              {doc.postedByName ?? 'Hệ thống'} · {formatDateTime(doc.postedAt)}
            </Info>
          )}
          {doc.note && (
            <div className="sm:col-span-2 lg:col-span-4">
              <Info label="Ghi chú">{doc.note}</Info>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dòng hàng ({doc.lines.length})</CardTitle>
        </CardHeader>
        <CardContent className="overflow-x-auto">
          <Table aria-label="Dòng hàng">
            <TableHeader>
              <TableRow>
                <TableHead className="w-10">#</TableHead>
                <TableHead>SKU</TableHead>
                <TableHead>Sản phẩm</TableHead>
                <TableHead>ĐVT</TableHead>
                {isStockTake && <TableHead className="text-right">Tồn sổ sách</TableHead>}
                <TableHead className="text-right">{isStockTake ? 'Số đếm' : 'Số lượng'}</TableHead>
                {isStockTake && <TableHead className="text-right">Chênh lệch</TableHead>}
                {isReceipt && <TableHead className="text-right">Đơn giá</TableHead>}
                {isReceipt && <TableHead className="text-right">Thành tiền</TableHead>}
                <TableHead>Ghi chú</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {doc.lines.map((l, i) => {
                const short = shortageOf(l.productId);
                return (
                  <TableRow key={l.id} className={cn(short && 'bg-red-50/70 dark:bg-red-950/20')}>
                    <TableCell className="text-muted-foreground">{i + 1}</TableCell>
                    <TableCell className="font-mono text-xs">{l.sku}</TableCell>
                    <TableCell className="font-medium">{l.productName}</TableCell>
                    <TableCell>{l.unit}</TableCell>
                    {isStockTake && (
                      <TableCell className="text-right tabular-nums">
                        {l.systemQuantity === null ? <span className="text-muted-foreground">khi ghi sổ</span> : formatQty(l.systemQuantity)}
                      </TableCell>
                    )}
                    <TableCell className={cn('text-right tabular-nums', short && 'font-semibold text-destructive')}>
                      {formatQty(l.quantity)}
                      {short && <div className="text-xs font-normal">còn {formatQty(short.available)}</div>}
                    </TableCell>
                    {isStockTake && (
                      <TableCell
                        className={cn(
                          'text-right font-medium tabular-nums',
                          (l.difference ?? 0) < 0 && 'text-red-600 dark:text-red-400',
                          (l.difference ?? 0) > 0 && 'text-emerald-600 dark:text-emerald-400',
                        )}
                      >
                        {formatSigned(l.difference)}
                      </TableCell>
                    )}
                    {isReceipt && <TableCell className="text-right tabular-nums">{formatMoney(l.unitCost)}</TableCell>}
                    {isReceipt && <TableCell className="text-right tabular-nums">{formatMoney(l.quantity * (l.unitCost ?? 0))}</TableCell>}
                    <TableCell className="text-muted-foreground">{l.note ?? ''}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell colSpan={isStockTake ? 5 : 4} className="text-right">
                  Tổng
                </TableCell>
                <TableCell className="text-right tabular-nums">{formatQty(totalQty)}</TableCell>
                {isStockTake && <TableCell />}
                {isReceipt && <TableCell />}
                {isReceipt && <TableCell className="text-right tabular-nums">{formatMoney(totalAmount)}</TableCell>}
                <TableCell />
              </TableRow>
            </TableFooter>
          </Table>
        </CardContent>
      </Card>

      <ConfirmDialog
        open={confirm === 'post'}
        onOpenChange={(o) => !o && setConfirm(null)}
        title={`Ghi sổ ${doc.code}?`}
        description={
          isStockTake
            ? 'Tồn kho sẽ được điều chỉnh theo số đếm thực tế. Phiếu đã ghi sổ không sửa hay hủy được.'
            : 'Tồn kho sẽ thay đổi ngay. Phiếu đã ghi sổ không sửa hay hủy được.'
        }
        confirmText="Ghi sổ"
        onConfirm={doPost}
      />
      <ConfirmDialog
        open={confirm === 'cancel'}
        onOpenChange={(o) => !o && setConfirm(null)}
        title={`Hủy ${doc.code}?`}
        description="Phiếu nháp bị hủy sẽ không thể ghi sổ nữa. Tồn kho không thay đổi."
        confirmText="Hủy phiếu"
        destructive
        onConfirm={doCancel}
      />
    </div>
  );
}
