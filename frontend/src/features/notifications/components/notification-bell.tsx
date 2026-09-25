import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, BellOff, CheckCheck, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/common/empty-state';
import { formatDateTime, fromNow } from '@/lib/date';
import { cn } from '@/lib/utils';
import type { NotificationDto } from '@/types';
import { useMarkAllRead, useMarkRead, useNotificationList, useUnreadCount } from '../hooks/use-notifications';

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const { data: unread = 0 } = useUnreadCount();
  const list = useNotificationList(open);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();

  const handleClick = (n: NotificationDto) => {
    if (!n.isRead) markRead.mutate(n.id);
    setOpen(false);
    if (n.link) navigate(n.link);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={unread ? `Thông báo (${unread} chưa đọc)` : 'Thông báo'}
        >
          <Bell />
          {unread > 0 && (
            <span
              data-testid="unread-count"
              className="absolute -top-0.5 -right-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] leading-none font-semibold text-white"
            >
              {unread > 99 ? '99+' : unread}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[min(22rem,calc(100vw-2rem))] gap-0 p-0">
        <div className="flex items-center justify-between px-3 py-2">
          <span className="font-medium">Thông báo</span>
          <Button
            variant="ghost"
            size="sm"
            disabled={!unread || markAll.isPending}
            onClick={() => markAll.mutate()}
          >
            {markAll.isPending ? <Loader2 className="animate-spin" /> : <CheckCheck />}
            Đánh dấu đã đọc tất cả
          </Button>
        </div>
        <Separator />
        <div className="max-h-96 overflow-y-auto">
          {list.isLoading ? (
            <div className="space-y-2 p-3">
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-2/3" />
            </div>
          ) : !list.data?.length ? (
            <EmptyState icon={<BellOff />} title="Không có thông báo" className="py-8" />
          ) : (
            <ul>
              {list.data.map((n) => (
                <li key={n.id} className={cn('flex items-start gap-2 border-b px-3 py-2 last:border-b-0', !n.isRead && 'bg-primary/5')}>
                  <button
                    type="button"
                    className="min-w-0 flex-1 cursor-pointer text-left"
                    onClick={() => handleClick(n)}
                  >
                    <div className={cn('text-sm', !n.isRead && 'font-semibold')}>{n.message}</div>
                    <div className="text-xs text-muted-foreground" title={formatDateTime(n.createdAt)}>
                      {fromNow(n.createdAt)}
                    </div>
                  </button>
                  {!n.isRead && (
                    <Button variant="link" size="xs" className="shrink-0" onClick={() => markRead.mutate(n.id)}>
                      Đã đọc
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
