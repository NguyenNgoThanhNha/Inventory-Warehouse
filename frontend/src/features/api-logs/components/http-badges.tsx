import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

const METHOD_CLASS: Record<string, string> = {
  GET: 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900 dark:bg-sky-950 dark:text-sky-300',
  POST: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300',
  PUT: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-300',
  PATCH: 'border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-900 dark:bg-violet-950 dark:text-violet-300',
  DELETE: 'border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-300',
};

export function MethodBadge({ method }: { method: string }) {
  return (
    <Badge variant="outline" className={cn('font-mono', METHOD_CLASS[method.toUpperCase()])}>
      {method.toUpperCase()}
    </Badge>
  );
}

export function statusCodeClass(code: number): string {
  if (code >= 500) return 'border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-300';
  if (code >= 400)
    return 'border-orange-200 bg-orange-50 text-orange-700 dark:border-orange-900 dark:bg-orange-950 dark:text-orange-300';
  if (code >= 300) return 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900 dark:bg-sky-950 dark:text-sky-300';
  return 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300';
}

export function StatusCodeBadge({ code }: { code: number }) {
  return (
    <Badge variant="outline" data-testid="status-code" className={cn('font-mono tabular-nums', statusCodeClass(code))}>
      {code}
    </Badge>
  );
}
