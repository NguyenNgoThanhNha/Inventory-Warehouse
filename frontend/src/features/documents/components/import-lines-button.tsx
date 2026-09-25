import { useRef } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Download, FileUp, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { api } from '@/lib/api-client';
import { showError } from '@/lib/api-errors';
import { downloadFile } from '@/lib/download';
import type { DocumentType, ParsedLinesDto } from '@/types';

const parseLines = (file: File, type: DocumentType) => {
  const form = new FormData();
  form.append('file', file);
  return api.post<ParsedLinesDto>('/imports/document-lines', form, { params: { type } }).then((r) => r.data);
};

/** Reads lines (SKU, Số lượng, Đơn giá, Ghi chú) from an .xlsx — nothing is saved until the user saves the document. */
export function ImportLinesButton({ type, onParsed }: { type: DocumentType; onParsed: (result: ParsedLinesDto) => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const parse = useMutation({
    mutationFn: (file: File) => parseLines(file, type),
    meta: { suppressGlobalError: true },
    onSuccess: onParsed,
    onError: (err) => showError(err, 'Không đọc được file'),
    onSettled: () => {
      if (inputRef.current) inputRef.current.value = '';
    },
  });

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept=".xlsx"
        className="hidden"
        aria-label="File Excel dòng hàng"
        onChange={(e) => e.target.files?.[0] && parse.mutate(e.target.files[0])}
      />
      <Button type="button" variant="outline" size="sm" disabled={parse.isPending} onClick={() => inputRef.current?.click()}>
        {parse.isPending ? <Loader2 className="animate-spin" /> : <FileUp />}
        Nhập từ Excel
      </Button>
      <Button
        type="button"
        variant="link"
        size="sm"
        onClick={() => void downloadFile('/imports/templates/DocumentLines', {}, 'mau-dong-phieu.xlsx').catch((e: unknown) => showError(e))}
      >
        <Download /> File mẫu
      </Button>
    </>
  );
}
