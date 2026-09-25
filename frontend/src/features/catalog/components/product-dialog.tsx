import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Form } from '@/components/ui/form';
import { SelectFormField, SwitchFormField, TextFormField } from '@/components/common/form-fields';
import { applyFieldErrors, showError } from '@/lib/api-errors';
import type { ProductDto } from '@/types';
import { useProductGroups, useSaveProduct } from '../hooks/use-catalog';
import { productSchema, type ProductForm, type ProductFormInput } from '../schemas';

const FIELDS = ['sku', 'name', 'unit', 'groupId', 'cost', 'price', 'imageUrl'] as const;

export function ProductDialog({
  product,
  open,
  onOpenChange,
}: {
  product: ProductDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const save = useSaveProduct(product?.id ?? null);
  const { data: groups = [] } = useProductGroups();
  const form = useForm<ProductFormInput, unknown, ProductForm>({ resolver: zodResolver(productSchema) });

  useEffect(() => {
    if (!open) return;
    form.reset({
      sku: product?.sku ?? '',
      name: product?.name ?? '',
      unit: product?.unit ?? 'cái',
      groupId: product?.groupId ?? ('' as unknown as number),
      cost: product?.cost ?? 0,
      price: product?.price ?? 0,
      imageUrl: product?.imageUrl ?? '',
      isActive: product?.isActive ?? true,
    });
  }, [open, product, form]);

  const onSubmit = form.handleSubmit((v) =>
    save.mutate(v, {
      onSuccess: (p) => {
        toast.success(product ? `Đã cập nhật ${p.sku}` : `Đã thêm ${p.sku}`);
        onOpenChange(false);
      },
      onError: (err) => {
        if (!applyFieldErrors(err, FIELDS, form.setError)) showError(err);
      },
    }),
  );

  return (
    <Dialog open={open} onOpenChange={(o) => !save.isPending && onOpenChange(o)}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{product ? `Sửa sản phẩm ${product.sku}` : 'Thêm sản phẩm'}</DialogTitle>
          <DialogDescription>SKU không đổi được sau khi tạo. Giá vốn dùng để tính giá trị tồn.</DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form id="product-form" onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
            <TextFormField control={form.control} name="sku" label="SKU" required disabled={!!product} autoFocus={!product} />
            <TextFormField control={form.control} name="unit" label="Đơn vị" required />
            <div className="sm:col-span-2">
              <TextFormField control={form.control} name="name" label="Tên sản phẩm" required />
            </div>
            <SelectFormField
              control={form.control}
              name="groupId"
              label="Nhóm hàng"
              required
              options={groups.map((g) => ({ value: String(g.id), label: g.name }))}
            />
            <div className="hidden sm:block" />
            <TextFormField control={form.control} name="cost" label="Giá vốn (đ)" type="number" min={0} inputMode="decimal" />
            <TextFormField control={form.control} name="price" label="Giá bán (đ)" type="number" min={0} inputMode="decimal" />
            <div className="sm:col-span-2">
              <TextFormField control={form.control} name="imageUrl" label="URL ảnh" placeholder="https://..." />
            </div>
            {product && (
              <div className="sm:col-span-2">
                <SwitchFormField
                  control={form.control}
                  name="isActive"
                  label="Đang kinh doanh"
                  description="Tắt để ẩn khỏi phiếu mới; lịch sử và tồn hiện có vẫn giữ nguyên."
                />
              </div>
            )}
          </form>
        </Form>
        <DialogFooter>
          <Button variant="outline" disabled={save.isPending} onClick={() => onOpenChange(false)}>
            Hủy
          </Button>
          <Button type="submit" form="product-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            Lưu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
