import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { formatQty } from '@/lib/format';
import { useSetThreshold } from '../hooks/use-stock';

export interface ThresholdTarget {
  productId: number;
  sku: string;
  productName: string;
  warehouseId: number;
  warehouseName: string;
  quantity: number;
  minThreshold: number;
}

export function ThresholdDialog({ target, onClose }: { target: ThresholdTarget | null; onClose: () => void }) {
  const save = useSetThreshold();
  const [value, setValue] = useState('');
  const parsed = Number(value);
  const invalid = value.trim() === '' || !Number.isFinite(parsed) || parsed < 0;

  useEffect(() => {
    if (target) setValue(String(target.minThreshold));
  }, [target]);

  const submit = () => {
    if (!target || invalid) return;
    save.mutate(
      { productId: target.productId, warehouseId: target.warehouseId, minThreshold: parsed },
      {
        onSuccess: () => {
          toast.success(`Đã đặt ngưỡng ${formatQty(parsed)} cho ${target.sku} tại ${target.warehouseName}`);
          onClose();
        },
      },
    );
  };

  return (
    <Dialog open={!!target} onOpenChange={(o) => !o && !save.isPending && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Ngưỡng tồn tối thiểu</DialogTitle>
          <DialogDescription>
            {target?.sku} — {target?.productName} tại {target?.warehouseName}. Tồn hiện tại: {formatQty(target?.quantity)}.
          </DialogDescription>
        </DialogHeader>
        <form
          id="threshold-form"
          className="space-y-2"
          onSubmit={(e) => {
            e.preventDefault();
            submit();
          }}
        >
          <Label htmlFor="min-threshold">Ngưỡng (0 = không cảnh báo)</Label>
          <Input
            id="min-threshold"
            type="number"
            min={0}
            step="any"
            inputMode="decimal"
            autoFocus
            value={value}
            aria-invalid={invalid || undefined}
            onChange={(e) => setValue(e.target.value)}
          />
          {invalid && <p className="text-sm text-destructive">Nhập một số từ 0 trở lên</p>}
        </form>
        <DialogFooter>
          <Button variant="outline" disabled={save.isPending} onClick={onClose}>
            Hủy
          </Button>
          <Button type="submit" form="threshold-form" disabled={invalid || save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            Lưu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
