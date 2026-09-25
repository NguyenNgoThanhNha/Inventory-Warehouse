import { z } from 'zod';

export const passwordSchema = z
  .string()
  .min(8, 'Mật khẩu tối thiểu 8 ký tự')
  .regex(/[A-Z]/, 'Mật khẩu phải có chữ hoa')
  .regex(/[a-z]/, 'Mật khẩu phải có chữ thường')
  .regex(/[0-9]/, 'Mật khẩu phải có chữ số');

const email = z.string().trim().min(1, 'Vui lòng nhập email').email('Email không hợp lệ');

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Vui lòng nhập mật khẩu'),
});
export type LoginForm = z.infer<typeof loginSchema>;

export const registerSchema = z
  .object({
    fullName: z.string().trim().min(1, 'Vui lòng nhập họ tên').max(100, 'Tối đa 100 ký tự'),
    email,
    password: passwordSchema,
    confirmPassword: z.string().min(1, 'Vui lòng nhập lại mật khẩu'),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Mật khẩu nhập lại không khớp',
  });
export type RegisterForm = z.infer<typeof registerSchema>;

export const forgotPasswordSchema = z.object({ email });
export type ForgotPasswordForm = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z
  .object({
    email,
    token: z.string().min(1, 'Thiếu token đặt lại mật khẩu'),
    newPassword: passwordSchema,
    confirmPassword: z.string().min(1, 'Vui lòng nhập lại mật khẩu'),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Mật khẩu nhập lại không khớp',
  });
export type ResetPasswordForm = z.infer<typeof resetPasswordSchema>;
