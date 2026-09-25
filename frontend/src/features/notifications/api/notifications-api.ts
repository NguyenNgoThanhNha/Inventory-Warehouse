import { api, cleanParams } from '@/lib/api-client';
import type { NotificationDto } from '@/types';

export const notificationsApi = {
  list: (params: { unreadOnly?: boolean; take?: number } = {}) =>
    api.get<NotificationDto[]>('/notifications', { params: cleanParams(params) }).then((r) => r.data),
  unreadCount: () => api.get<{ count: number }>('/notifications/unread-count').then((r) => r.data.count),
  markRead: (id: number) => api.post<void>(`/notifications/${id}/read`).then(() => undefined),
  markAllRead: () => api.post<void>('/notifications/read-all').then(() => undefined),
};
