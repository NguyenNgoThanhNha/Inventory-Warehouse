import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-client';
import type { NotificationDto } from '@/types';
import { notificationsApi } from '../api/notifications-api';

export const UNREAD_POLL_INTERVAL = 30_000;

/**
 * Unread badge count, polled every 30s while the tab is visible. Polling pauses while the tab is hidden
 * (TanStack's focusManager follows `visibilitychange`), and the count is refreshed as soon as the user comes back.
 */
export function useUnreadCount() {
  return useQuery({
    queryKey: queryKeys.unreadCount,
    queryFn: notificationsApi.unreadCount,
    refetchInterval: UNREAD_POLL_INTERVAL,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: 'always',
    meta: { suppressGlobalError: true },
  });
}

export function useNotificationList(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.notificationList,
    queryFn: () => notificationsApi.list({ take: 20 }),
    enabled,
    staleTime: 0,
  });
}

interface Snapshot {
  list: NotificationDto[] | undefined;
  count: number | undefined;
}

/** Cancels in-flight notification queries (so they can't overwrite the optimistic state) and snapshots the cache. */
async function snapshot(queryClient: QueryClient): Promise<Snapshot> {
  await queryClient.cancelQueries({ queryKey: queryKeys.notifications });
  return {
    list: queryClient.getQueryData<NotificationDto[]>(queryKeys.notificationList),
    count: queryClient.getQueryData<number>(queryKeys.unreadCount),
  };
}

function restore(queryClient: QueryClient, prev: Snapshot | undefined) {
  if (!prev) return;
  queryClient.setQueryData(queryKeys.notificationList, prev.list);
  queryClient.setQueryData(queryKeys.unreadCount, prev.count);
}

/** Marks one notification read. Optimistic: the item and the badge update immediately and roll back on error. */
export function useMarkRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: notificationsApi.markRead,
    onMutate: async (id: number) => {
      const prev = await snapshot(queryClient);
      const wasUnread = prev.list ? prev.list.some((n) => n.id === id && !n.isRead) : true;
      queryClient.setQueryData<NotificationDto[]>(queryKeys.notificationList, (list) =>
        list?.map((n) => (n.id === id ? { ...n, isRead: true } : n)),
      );
      if (wasUnread) {
        queryClient.setQueryData<number>(queryKeys.unreadCount, (c) => (c === undefined ? c : Math.max(0, c - 1)));
      }
      return prev;
    },
    onError: (_err, _id, prev) => restore(queryClient, prev),
    // the list is exact after the optimistic update; only re-sync the (cheap) count
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.unreadCount }),
  });
}

/** Marks everything read. Optimistic, like useMarkRead. */
export function useMarkAllRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: notificationsApi.markAllRead,
    onMutate: async () => {
      const prev = await snapshot(queryClient);
      queryClient.setQueryData<NotificationDto[]>(queryKeys.notificationList, (list) =>
        list?.map((n) => (n.isRead ? n : { ...n, isRead: true })),
      );
      queryClient.setQueryData<number>(queryKeys.unreadCount, (c) => (c === undefined ? c : 0));
      return prev;
    },
    onError: (_err, _vars, prev) => restore(queryClient, prev),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.unreadCount }),
  });
}
