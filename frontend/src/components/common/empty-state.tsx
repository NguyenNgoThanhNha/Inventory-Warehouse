import type { ReactNode } from 'react';
import { Inbox } from 'lucide-react';
import { cn } from '@/lib/utils';

/** Centered placeholder for empty lists and error / 403 / 404 screens. */
export function EmptyState({
  icon,
  title,
  description,
  action,
  className,
}: {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn('flex flex-col items-center justify-center gap-2 px-4 py-10 text-center', className)}>
      <div className="text-muted-foreground [&_svg]:size-10">{icon ?? <Inbox />}</div>
      <div className="font-medium">{title}</div>
      {description && <div className="max-w-md text-sm text-muted-foreground">{description}</div>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}
