import { useState } from 'react';
import { ChevronsUpDown, Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { useDebouncedCallback } from '@/lib/hooks/use-debounced-callback';
import type { ProductDto } from '@/types';
import { useProducts } from '../hooks/use-catalog';

export type PickedProduct = Pick<ProductDto, 'id' | 'sku' | 'name' | 'unit' | 'cost'>;

/**
 * Searches active products on the server (SKU prefix / name contains) — the catalog can have tens of thousands
 * of products, so options are never loaded all at once. Already-picked products are disabled.
 */
export function ProductPicker({
  value,
  onChange,
  excludeIds = [],
  invalid,
  'aria-label': ariaLabel,
}: {
  value: PickedProduct | null;
  onChange: (product: PickedProduct) => void;
  excludeIds?: number[];
  invalid?: boolean;
  'aria-label': string;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const debounced = useDebouncedCallback(setSearch, 250);
  const { data, isFetching } = useProducts({ search, isActive: true, page: 1, pageSize: 20 }, open);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={ariaLabel}
          aria-invalid={invalid || undefined}
          className={cn('w-full justify-between font-normal', !value && 'text-muted-foreground')}
        >
          <span className="truncate">{value ? `${value.sku} — ${value.name}` : 'Chọn sản phẩm...'}</span>
          {isFetching && open ? <Loader2 className="animate-spin" /> : <ChevronsUpDown className="opacity-50" />}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-80 p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput placeholder="Gõ SKU hoặc tên..." onValueChange={(v) => debounced.run(v)} />
          <CommandList>
            <CommandEmpty>{isFetching ? 'Đang tìm...' : 'Không có sản phẩm phù hợp'}</CommandEmpty>
            <CommandGroup>
              {data?.items.map((p) => (
                <CommandItem
                  key={p.id}
                  value={String(p.id)}
                  disabled={excludeIds.includes(p.id) && p.id !== value?.id}
                  data-checked={p.id === value?.id}
                  onSelect={() => {
                    onChange({ id: p.id, sku: p.sku, name: p.name, unit: p.unit, cost: p.cost });
                    setOpen(false);
                  }}
                >
                  <span className="font-mono text-xs text-muted-foreground">{p.sku}</span>
                  <span className="truncate">{p.name}</span>
                  <span className="ml-auto text-xs text-muted-foreground">{p.unit}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
