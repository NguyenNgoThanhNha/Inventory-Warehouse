import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Download, FileSpreadsheet, Loader2, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { RowErrors } from '@/components/common/row-errors';
import { api } from '@/lib/api-client';
import { showError } from '@/lib/api-errors';
import { downloadFile } from '@/lib/download';
import { formatFileSize } from '@/lib/file';
import { queryKeys } from '@/lib/query-client';
import type { ImportProductsResultDto } from '@/types';

const MAX_BYTES = 5 * 1024 * 1024;

const importProducts = (file: File, dryRun: boolean) => {
  const form = new FormData();
  form.append('file', file);
  return api.post<ImportProductsResultDto>('/imports/products', form, { params: { dryRun } }).then((r) => r.data);
};

/**
 * Two steps: "Kiểm tra" (dry run — nothing is written, every bad row is listed) then "Import" the valid rows.
 * Re-importing the same file is safe: products are upserted by SKU.
 */
export function ImportProductsDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportProductsResultDto | null>(null);

  const run = useMutation({
    mutationFn: ({ f, dryRun }: { f: File; dryRun: boolean }) => importProducts(f, dryRun),
    meta: { suppressGlobalError: true },
    onError: (err) => showError(err, 'Không import được file'),
  });

  const reset = () => {
    setFile(null);
    setPreview(null);
    run.reset();
    if (inputRef.current) inputRef.current.value = '';
  };

  const pick = (f: File | undefined) => {
    if (!f) return;
    if (f.size > MAX_BYTES) {
      toast.error(`File tối đa ${formatFileSize(MAX_BYTES)}`);
      return;
    }
    setFile(f);
    setPreview(null);
    run.mutate({ f, dryRun: true }, { onSuccess: setPreview });
  };

  const doImport = () =>
    file &&
    run.mutate(
      { f: file, dryRun: false },
      {
        onSuccess: (r) => {
          toast.success(`Đã import: ${r.created} thêm mới, ${r.updated} cập nhật${r.errors.length ? `, bỏ qua ${new Set(r.errors.map((e) => e.row)).size} dòng lỗi` : ''}`);
          void queryClient.invalidateQueries({ queryKey: queryKeys.products });
          void queryClient.invalidateQueries({ queryKey: queryKeys.productGroups });
          void queryClient.invalidateQueries({ queryKey: queryKeys.stock });
          onOpenChange(false);
          reset();
        },
      },
    );

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        if (run.isPending) return;
        if (!o) reset();
        onOpenChange(o);
      }}
    >
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Import sản phẩm từ Excel</DialogTitle>
          <DialogDescription>
            SKU đã có sẽ được cập nhật, SKU mới được thêm; nhóm hàng chưa có sẽ được tạo. File sẽ được kiểm tra trước, chưa ghi gì cho tới khi bạn bấm Import.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <input
              ref={inputRef}
              type="file"
              accept=".xlsx"
              className="hidden"
              aria-label="Chọn file Excel"
              onChange={(e) => pick(e.target.files?.[0])}
            />
            <Button type="button" variant="outline" disabled={run.isPending} onClick={() => inputRef.current?.click()}>
              <Upload /> Chọn file .xlsx
            </Button>
            <Button
              type="button"
              variant="link"
              onClick={() => void downloadFile('/imports/templates/Products', {}, 'mau-import-san-pham.xlsx').catch((e: unknown) => showError(e))}
            >
              <Download /> Tải file mẫu
            </Button>
          </div>

          {file && (
            <div className="flex items-center gap-2 text-sm">
              <FileSpreadsheet className="size-4 text-emerald-600" />
              <span className="font-medium">{file.name}</span>
              <span className="text-muted-foreground">({formatFileSize(file.size)})</span>
              {run.isPending && <Loader2 className="size-4 animate-spin" />}
            </div>
          )}

          {preview && (
            <>
              <Alert>
                <AlertDescription>
                  {preview.totalRows.toLocaleString('vi-VN')} dòng · <b>{preview.created.toLocaleString('vi-VN')}</b> thêm mới ·{' '}
                  <b>{preview.updated.toLocaleString('vi-VN')}</b> cập nhật
                  {preview.groupsCreated.length > 0 && <> · nhóm hàng mới: {preview.groupsCreated.join(', ')}</>}
                </AlertDescription>
              </Alert>
              <RowErrors errors={preview.errors} />
            </>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" disabled={run.isPending} onClick={() => onOpenChange(false)}>
            Hủy
          </Button>
          <Button disabled={!preview || preview.validRows === 0 || run.isPending} onClick={doImport}>
            {run.isPending && run.variables?.dryRun === false && <Loader2 className="animate-spin" />}
            Import {preview ? preview.validRows.toLocaleString('vi-VN') : ''} dòng hợp lệ
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
