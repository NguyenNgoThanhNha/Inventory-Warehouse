import { useState } from 'react';
import { FileSpreadsheet, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { showError } from '@/lib/api-errors';
import { downloadFile } from '@/lib/download';

/** Downloads an Excel export from the API (with the current filters) and saves it. */
export function ExportButton({
  url,
  params,
  fallbackName,
  label = 'Xuất Excel',
  disabled,
}: {
  url: string;
  params?: object;
  fallbackName: string;
  label?: string;
  disabled?: boolean;
}) {
  const [pending, setPending] = useState(false);
  return (
    <Button
      type="button"
      variant="outline"
      disabled={disabled || pending}
      onClick={() => {
        setPending(true);
        downloadFile(url, params, fallbackName)
          .catch((err: unknown) => showError(err, 'Không xuất được file'))
          .finally(() => setPending(false));
      }}
    >
      {pending ? <Loader2 className="animate-spin" /> : <FileSpreadsheet />}
      {label}
    </Button>
  );
}
