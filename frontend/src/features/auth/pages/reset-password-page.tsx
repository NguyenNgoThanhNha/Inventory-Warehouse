import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useSearchParams } from 'react-router-dom';
import { AlertCircle, CircleCheck, Loader2, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Form } from '@/components/ui/form';
import { EmptyState } from '@/components/common/empty-state';
import { TextFormField } from '@/components/common/form-fields';
import { applyFieldErrors, getErrorMessage } from '@/lib/api-errors';
import { AuthCard } from '../components/auth-card';
import { useResetPassword } from '../hooks/use-auth';
import { resetPasswordSchema, type ResetPasswordForm } from '../schemas';

const FIELDS = ['newPassword', 'email', 'token'] as const;

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';
  const reset = useResetPassword();

  const form = useForm<ResetPasswordForm>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { email, token, newPassword: '', confirmPassword: '' },
  });
  const { errors } = form.formState;

  const onSubmit = form.handleSubmit((v) =>
    reset.mutate(
      { email: v.email, token: v.token, newPassword: v.newPassword },
      { onError: (err) => void applyFieldErrors(err, FIELDS, form.setError) },
    ),
  );

  if (reset.isSuccess) {
    return (
      <AuthCard title="Đặt lại mật khẩu">
        <EmptyState
          icon={<CircleCheck className="text-emerald-600" />}
          title="Đổi mật khẩu thành công"
          action={
            <Button asChild>
              <Link to="/login">Đăng nhập</Link>
            </Button>
          }
        />
      </AuthCard>
    );
  }

  const serverError = reset.error && !Object.keys(errors).length ? getErrorMessage(reset.error) : null;

  return (
    <AuthCard
      title="Đặt lại mật khẩu"
      footer={
        <Link to="/login" className="text-primary underline-offset-4 hover:underline">
          Quay lại đăng nhập
        </Link>
      }
    >
      <div className="mb-4 space-y-2 empty:hidden">
        {!token && (
          <Alert>
            <TriangleAlert />
            <AlertDescription>Link đặt lại mật khẩu không hợp lệ (thiếu token).</AlertDescription>
          </Alert>
        )}
        {(errors.token || serverError) && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errors.token?.message ?? serverError}</AlertDescription>
          </Alert>
        )}
      </div>
      <Form {...form}>
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <TextFormField control={form.control} name="email" label="Email" required readOnly={!!email} />
          <TextFormField
            control={form.control}
            name="newPassword"
            label="Mật khẩu mới"
            required
            type="password"
            autoComplete="new-password"
          />
          <TextFormField
            control={form.control}
            name="confirmPassword"
            label="Nhập lại mật khẩu"
            required
            type="password"
            autoComplete="new-password"
          />
          <Button type="submit" className="w-full" disabled={reset.isPending || !token}>
            {reset.isPending && <Loader2 className="animate-spin" />}
            Đặt lại mật khẩu
          </Button>
        </form>
      </Form>
    </AuthCard>
  );
}
