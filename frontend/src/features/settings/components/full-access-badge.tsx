import { Badge } from '@/components/ui/badge';

/** "Toàn quyền" marker for the Admin role / admin users. */
export function FullAccessBadge() {
  return (
    <Badge
      variant="outline"
      className="border-fuchsia-200 bg-fuchsia-50 text-fuchsia-700 dark:border-fuchsia-900 dark:bg-fuchsia-950 dark:text-fuchsia-300"
    >
      Toàn quyền
    </Badge>
  );
}
