import { useEffect, useState } from 'react';
import { Search, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Input } from '@/components/ui/input';
import { useDebouncedCallback } from '@/lib/hooks/use-debounced-callback';

/**
 * Debounced search box with a clear button. Keeps its own text while typing (never remounts, so focus
 * and caret stay put) and follows `value` when it changes from outside — e.g. the global search in the header.
 */
export function SearchInput({
  value,
  onChange,
  placeholder = 'Tìm...',
  className,
  'aria-label': ariaLabel,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  'aria-label': string;
}) {
  const [text, setText] = useState(value);
  const debounced = useDebouncedCallback((v: string) => onChange(v.trim()));

  // external change (URL, reset button) → show it; our own debounced echo is a no-op
  useEffect(() => {
    setText((current) => (current.trim() === value ? current : value));
  }, [value]);

  return (
    <div className={cn('relative w-full sm:max-w-xs', className)}>
      <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        type="search"
        aria-label={ariaLabel}
        placeholder={placeholder}
        className="pr-8 pl-8 [&::-webkit-search-cancel-button]:hidden"
        value={text}
        onChange={(e) => {
          setText(e.target.value);
          debounced.run(e.target.value);
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') debounced.flush(text); // no need to wait
          if (e.key === 'Escape' && text) {
            setText('');
            debounced.flush('');
          }
        }}
      />
      {text && (
        <button
          type="button"
          aria-label="Xóa từ khóa"
          className="absolute top-1/2 right-2 -translate-y-1/2 rounded p-0.5 text-muted-foreground hover:text-foreground"
          onClick={() => {
            setText('');
            debounced.flush('');
          }}
        >
          <X className="size-4" />
        </button>
      )}
    </div>
  );
}
