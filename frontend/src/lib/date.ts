import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import relativeTime from 'dayjs/plugin/relativeTime';

dayjs.extend(relativeTime);
dayjs.locale('vi');

export { dayjs };

/** API dates are ISO UTC ("...Z"); dayjs() parses them and formats in local time. */
export const formatDateTime = (value: string | null | undefined) =>
  value ? dayjs(value).format('DD/MM/YYYY HH:mm') : '—';

export const formatDateTimeSeconds = (value: string | null | undefined) =>
  value ? dayjs(value).format('DD/MM/YYYY HH:mm:ss') : '—';

/** Compact table format: "18/06 09:50" within the current year, "18/06/2025" otherwise (full value belongs in a tooltip). */
export const formatDateTimeShort = (value: string | null | undefined) => {
  if (!value) return '—';
  const d = dayjs(value);
  return d.year() === dayjs().year() ? d.format('DD/MM HH:mm') : d.format('DD/MM/YYYY');
};

export const formatDate =(value: string | null | undefined) => (value ? dayjs(value).format('DD/MM/YYYY') : '—');

export const fromNow = (value: string) => dayjs(value).fromNow();

/** yyyy-MM-dd (local date), used by document list `from` / `to` filters. */
export const toApiDate = (d: Date) => dayjs(d).format('YYYY-MM-DD');

function humanizeMinutes(totalMinutes: number): string {
  const minutes = Math.abs(Math.round(totalMinutes));
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    const rest = minutes % 60;
    return rest && hours < 10 ? `${hours}h${rest}m` : `${hours}h`;
  }
  const days = Math.floor(hours / 24);
  const restHours = hours % 24;
  return restHours ? `${days}d ${restHours}h` : `${days}d`;
}

/** "còn 2h" / "quá hạn 3h" relative to now. */
export function slaCountdown(dueAt: string | null | undefined, now: dayjs.Dayjs = dayjs()): string | null {
  if (!dueAt) return null;
  const diff = dayjs(dueAt).diff(now, 'minute', true);
  return diff >= 0 ? `còn ${humanizeMinutes(diff)}` : `quá hạn ${humanizeMinutes(diff)}`;
}

export interface DateRangeValue {
  from: Date;
  to: Date;
}

/** Last `days` days including today (local time). */
export function lastDays(days: number): DateRangeValue {
  return {
    from: dayjs().subtract(days - 1, 'day').startOf('day').toDate(),
    to: dayjs().endOf('day').toDate(),
  };
}
