import { z } from 'zod';

const money = z.coerce
  .number({ invalid_type_error: 'Vui lòng nhập số' })
  .min(0, 'Không được âm')
  .max(1_000_000_000_000, 'Giá trị quá lớn');

const optionalText = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Tối đa ${max} ký tự`)
    .optional()
    .transform((v) => (v ? v : null));

export const productSchema = z.object({
  sku: z
    .string()
    .trim()
    .min(1, 'Vui lòng nhập SKU')
    .max(50, 'Tối đa 50 ký tự')
    .regex(/^[A-Za-z0-9._-]+$/, 'SKU chỉ gồm chữ, số và . _ -'),
  name: z.string().trim().min(1, 'Vui lòng nhập tên').max(200, 'Tối đa 200 ký tự'),
  unit: z.string().trim().min(1, 'Vui lòng nhập đơn vị').max(20, 'Tối đa 20 ký tự'),
  groupId: z.coerce.number({ invalid_type_error: 'Chọn nhóm hàng' }).int().positive('Chọn nhóm hàng'),
  cost: money,
  price: money,
  imageUrl: optionalText(500),
  isActive: z.boolean(),
});
export type ProductFormInput = z.input<typeof productSchema>;
export type ProductForm = z.output<typeof productSchema>;

export const warehouseSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, 'Vui lòng nhập mã kho')
    .max(20, 'Tối đa 20 ký tự')
    .regex(/^[A-Za-z0-9_-]+$/, 'Mã kho chỉ gồm chữ, số và _ -'),
  name: z.string().trim().min(1, 'Vui lòng nhập tên kho').max(100, 'Tối đa 100 ký tự'),
  address: optionalText(300),
  isActive: z.boolean(),
});
export type WarehouseFormInput = z.input<typeof warehouseSchema>;
export type WarehouseForm = z.output<typeof warehouseSchema>;

export const supplierSchema = z.object({
  name: z.string().trim().min(1, 'Vui lòng nhập tên nhà cung cấp').max(200, 'Tối đa 200 ký tự'),
  phone: optionalText(20).refine((v) => !v || /^[0-9+ ()-]*$/.test(v), 'Số điện thoại không hợp lệ'),
  email: optionalText(200).refine((v) => !v || z.string().email().safeParse(v).success, 'Email không hợp lệ'),
  address: optionalText(300),
});
export type SupplierFormInput = z.input<typeof supplierSchema>;
export type SupplierForm = z.output<typeof supplierSchema>;

export const groupSchema = z.object({
  name: z.string().trim().min(1, 'Vui lòng nhập tên nhóm').max(100, 'Tối đa 100 ký tự'),
});
export type GroupForm = z.infer<typeof groupSchema>;
