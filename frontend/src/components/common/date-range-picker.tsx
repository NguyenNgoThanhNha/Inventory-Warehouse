import { useState } from 'react';
import type { DateRange } from 'react-day-picker';
import { vi } from 'react-day-picker/locale/vi';
import { CalendarIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { dayjs, lastDays, type DateRangeValue } from '@/lib/date';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Separator } from '@/components/ui/separator';

const PRESETS = [
  { label: '7 ngày', days: 7 },
  { label: '30 ngày', days: 30 },
  { label: '90 ngày', days: 90 },
];

const fmt = (d: Date) => dayjs(d).format('DD/MM/YYYY');

/**
 * Date range picker (Popover + Calendar range mode) with quick presets.
 * `value === undefined` means "no range" (only possible when `allowClear`).
 */
export function DateRangePicker({
  value,
  onChange,
  allowClear,
  placeholder = 'Chọn khoảng ngày',
  className,
  'aria-label': ariaLabel = 'Khoảng ngày',
}: {
  value: DateRangeValue | undefined;
  onChange: (value: DateRangeValue | undefined) => void;
  allowClear?: boolean;
  placeholder?: string;
  className?: string;
  'aria-label'?: string;
}) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<DateRange | undefined>(value);

  const handleOpenChange = (next: boolean) => {
    if (next) setDraft(value);
    setOpen(next);
  };

  const apply = (range: DateRangeValue | undefined) => {
    onChange(range);
    setOpen(false);
  };

  const today = dayjs().endOf('day').toDate();

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          aria-label={ariaLabel}
          className={cn('justify-start font-normal', !value && 'text-muted-foreground', className)}
        >
          <CalendarIcon />
          {value ? `${fmt(value.from)} – ${fmt(value.to)}` : placeholder}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-0" align="end">
        <div className="flex flex-col sm:flex-row">
          <div className="flex gap-1 p-2 sm:flex-col">
            {PRESETS.map((p) => (
              <Button key={p.days} variant="ghost" size="sm" className="justify-start" onClick={() => apply(lastDays(p.days))}>
                {p.label}
              </Button>
            ))}
            {allowClear && (
              <Button variant="ghost" size="sm" className="justify-start" onClick={() => apply(undefined)}>
                Bỏ chọn
              </Button>
            )}
          </div>
          <Separator orientation="vertical" className="hidden sm:block" />
          <div className="p-1">
            <Calendar
              mode="range"
              locale={vi}
              numberOfMonths={2}
              defaultMonth={draft?.from ?? dayjs().subtract(1, 'month').toDate()}
              selected={draft}
              onSelect={setDraft}
              disabled={{ after: today }}
            />
            <div className="flex justify-end gap-2 p-2">
              <Button variant="outline" size="sm" onClick={() => setOpen(false)}>
                Hủy
              </Button>
              <Button
                size="sm"
                disabled={!draft?.from}
                onClick={() =>
                  draft?.from &&
                  apply({
                    from: dayjs(draft.from).startOf('day').toDate(),
                    to: dayjs(draft.to ?? draft.from).endOf('day').toDate(),
                  })
                }
              >
                Áp dụng
              </Button>
            </div>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  );
}
