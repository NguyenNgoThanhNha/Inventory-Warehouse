import { dayjs, formatDateTimeShort, slaCountdown } from './date';
import { formatFileSize } from './file';

describe('formatDateTimeShort', () => {
  it('drops the year for dates in the current year and the time for older ones', () => {
    const thisYear = dayjs().year();
    expect(formatDateTimeShort(dayjs(`${thisYear}-06-18T09:50:00`).toISOString())).toBe('18/06 09:50');
    expect(formatDateTimeShort(dayjs(`${thisYear - 1}-06-18T09:50:00`).toISOString())).toBe(`18/06/${thisYear - 1}`);
    expect(formatDateTimeShort(null)).toBe('—');
  });
});

describe('slaCountdown', () => {
  const now = dayjs('2026-09-23T10:00:00Z');

  it('returns remaining time before the due date', () => {
    expect(slaCountdown('2026-09-23T12:00:00Z', now)).toBe('còn 2h');
    expect(slaCountdown('2026-09-23T10:45:00Z', now)).toBe('còn 45m');
    expect(slaCountdown('2026-09-25T13:00:00Z', now)).toBe('còn 2d 3h');
  });

  it('returns overdue time after the due date', () => {
    expect(slaCountdown('2026-09-23T07:00:00Z', now)).toBe('quá hạn 3h');
  });

  it('returns null when there is no due date', () => {
    expect(slaCountdown(null, now)).toBeNull();
  });
});

describe('formatFileSize', () => {
  it('formats bytes, KB and MB', () => {
    expect(formatFileSize(512)).toBe('512 B');
    expect(formatFileSize(2048)).toBe('2.0 KB');
    expect(formatFileSize(5 * 1024 * 1024)).toBe('5.0 MB');
  });
});
