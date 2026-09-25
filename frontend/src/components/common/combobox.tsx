import { useState } from 'react';
import { ChevronsUpDown, Loader2, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';

export interface ComboboxOption {
  value: string;
  label: string;
}

interface BaseProps {
  options: ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  'aria-label': string;
  disabled?: boolean;
  loading?: boolean;
  className?: string;
}

/** Searchable single-select (Popover + Command). `allowClear` adds a "clear" entry. */
export function Combobox({
  options,
  value,
  onChange,
  placeholder = 'Chọn...',
  searchPlaceholder = 'Tìm...',
  emptyText = 'Không có kết quả',
  allowClear,
  clearLabel = 'Bỏ chọn',
  disabled,
  loading,
  className,
  'aria-label': ariaLabel,
}: BaseProps & {
  value: string | undefined;
  onChange: (value: string | undefined) => void;
  allowClear?: boolean;
  clearLabel?: string;
}) {
  const [open, setOpen] = useState(false);
  const selected = options.find((o) => o.value === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={ariaLabel}
          disabled={disabled}
          className={cn('w-full justify-between font-normal', !selected && 'text-muted-foreground', className)}
        >
          <span className="truncate">{selected?.label ?? placeholder}</span>
          {loading ? <Loader2 className="animate-spin" /> : <ChevronsUpDown className="opacity-50" />}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-56 p-0" align="start">
        <Command>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            <CommandEmpty>{emptyText}</CommandEmpty>
            {allowClear && value !== undefined && (
              <>
                <CommandGroup>
                  <CommandItem
                    value="__clear__"
                    keywords={[clearLabel]}
                    onSelect={() => {
                      onChange(undefined);
                      setOpen(false);
                    }}
                  >
                    <X /> {clearLabel}
                  </CommandItem>
                </CommandGroup>
                <CommandSeparator />
              </>
            )}
            <CommandGroup>
              {options.map((o) => (
                <CommandItem
                  key={o.value}
                  value={o.value}
                  keywords={[o.label]}
                  data-checked={o.value === value}
                  onSelect={() => {
                    onChange(o.value);
                    setOpen(false);
                  }}
                >
                  {o.label}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

/** Searchable multi-select; selected items are shown as badges. */
export function MultiSelect({
  options,
  value,
  onChange,
  placeholder = 'Chọn...',
  searchPlaceholder = 'Tìm...',
  emptyText = 'Không có kết quả',
  disabled,
  loading,
  className,
  'aria-label': ariaLabel,
}: BaseProps & { value: string[]; onChange: (value: string[]) => void }) {
  const [open, setOpen] = useState(false);
  const selected = options.filter((o) => value.includes(o.value));
  const toggle = (v: string) => onChange(value.includes(v) ? value.filter((x) => x !== v) : [...value, v]);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={ariaLabel}
          disabled={disabled}
          className={cn('h-auto min-h-8 w-full justify-between py-1 font-normal', className)}
        >
          <span className="flex flex-wrap gap-1">
            {selected.length ? (
              selected.map((o) => (
                <Badge key={o.value} variant="secondary">
                  {o.label}
                </Badge>
              ))
            ) : (
              <span className="text-muted-foreground">{placeholder}</span>
            )}
          </span>
          {loading ? <Loader2 className="animate-spin" /> : <ChevronsUpDown className="opacity-50" />}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-56 p-0" align="start">
        <Command>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            <CommandEmpty>{emptyText}</CommandEmpty>
            <CommandGroup>
              {options.map((o) => (
                <CommandItem
                  key={o.value}
                  value={o.value}
                  keywords={[o.label]}
                  data-checked={value.includes(o.value)}
                  onSelect={() => toggle(o.value)}
                >
                  {o.label}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
