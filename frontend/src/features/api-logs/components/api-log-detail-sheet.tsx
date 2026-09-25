import type { ReactNode } from 'react';
import { AlertCircle } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { CopyButton } from '@/components/common/copy-button';
import { getErrorMessage } from '@/lib/api-errors';
import { formatDateTimeSeconds } from '@/lib/date';
import { useApiLog } from '../hooks/use-api-logs';
import { MethodBadge, StatusCodeBadge } from './http-badges';
import { JsonBlock } from './json-block';

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid grid-cols-[7rem_1fr] gap-2 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="min-w-0 break-all">{children}</dd>
    </div>
  );
}

export function ApiLogDetailSheet({ id, onClose }: { id: number | undefined; onClose: () => void }) {
  const { data, isLoading, isError, error } = useApiLog(id);

  return (
    <Sheet open={!!id} onOpenChange={(o) => !o && onClose()}>
      <SheetContent className="w-full gap-0 sm:max-w-2xl">
        <SheetHeader>
          <SheetTitle>API log #{id}</SheetTitle>
          <SheetDescription>Chi tiết request / response</SheetDescription>
        </SheetHeader>
        <div className="flex-1 space-y-4 overflow-y-auto px-4 pb-6">
          {isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-4 w-2/3" />
              <Skeleton className="h-4 w-1/2" />
              <Skeleton className="h-40 w-full" />
            </div>
          ) : isError ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{getErrorMessage(error)}</AlertDescription>
            </Alert>
          ) : data ? (
            <>
              <div className="flex flex-wrap items-center gap-2">
                <MethodBadge method={data.method} />
                <StatusCodeBadge code={data.statusCode} />
                <code className="min-w-0 font-mono text-sm break-all">{data.url}</code>
              </div>
              <dl className="space-y-2">
                <Field label="Thời gian">{formatDateTimeSeconds(data.createdDate)}</Field>
                <Field label="Thời lượng">{data.durationMs} ms</Field>
                <Field label="traceId">
                  <div className="flex flex-wrap items-center gap-2">
                    <code className="font-mono text-xs">{data.traceId}</code>
                    <CopyButton value={data.traceId} label="Copy traceId" />
                  </div>
                </Field>
                <Field label="User">{data.userName ?? data.userId ?? '—'}</Field>
                <Field label="IP">{data.ip ?? '—'}</Field>
                <Field label="Module">{data.module}</Field>
                <Field label="User agent">
                  <span className="text-xs">{data.userAgent ?? '—'}</span>
                </Field>
              </dl>
              <Separator />
              <JsonBlock title="Request" value={data.request} />
              <JsonBlock title="Response" value={data.response} />
            </>
          ) : null}
        </div>
      </SheetContent>
    </Sheet>
  );
}
