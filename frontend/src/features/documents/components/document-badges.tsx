import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import type { DocumentStatus } from '@/types';
import { STATUS_LABEL } from '../config';

const STATUS_CLASS: Record<DocumentStatus, string> = {
  Draft: 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-300',
  Posted: 'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300',
  Cancelled: 'border-muted bg-muted text-muted-foreground line-through',
};

export function DocumentStatusBadge({ status, className }: { status: DocumentStatus; className?: string }) {
  return (
    <Badge variant="outline" className={cn(STATUS_CLASS[status], className)}>
      {STATUS_LABEL[status]}
    </Badge>
  );
}
