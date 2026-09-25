import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, Loader2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Form } from '@/components/ui/form';
import { TextFormField } from '@/components/common/form-fields';
import { applyFieldErrors, getErrorMessage, getStatus } from '@/lib/api-errors';
import { AuthCard } from '../components/auth-card';
import { useRegister } from '../hooks/use-auth';
import { registerSchema, type RegisterForm } from '../schemas';

const FIELDS = ['fullName', 'email', 'password'] as const;

export function RegisterPage() {
  const navigate = useNavigate();
  const register = useRegister();
  const form = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: '', email: '', password: '', confirmPassword: '' },
  });

  const onSubmit = form.handleSubmit(({ fullName, email, password }) =>
    register.mutate(
      { fullName, email, password },
      {
        onSuccess: () => navigate('/', { replace: true }),
        onError: (err) => {
          if (getStatus(err) === 409) {
            form.setError('email', { type: 'server', message: 'Email đã được sử dụng' });
            return;
          }
          applyFieldErrors(err, FIELDS, form.setError);
        },
      },
    ),
  );

  const hasFieldErrors = Object.keys(form.formState.errors).length > 0;
  const showAlert = !!register.error && getStatus(register.error) !== 409 && !hasFieldErrors;

  return (
    <AuthCard
      title="Đăng ký tài khoản"
      footer={
        <span>
          Đã có tài khoản?{' '}
          <Link to="/login" className="text-primary underline-offset-4 hover:underline">
            Đăng nhập
          </Link>
        </span>
      }
    >
      {showAlert && (
        <Alert variant="destructive" className="mb-4">
          <AlertCircle />
          <AlertDescription>{getErrorMessage(register.error)}</AlertDescription>
        </Alert>
      )}
      <Form {...form}>
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <TextFormField control={form.control} name="fullName" label="Họ tên" required autoComplete="name" />
          <TextFormField control={form.control} name="email" label="Email" required type="email" autoComplete="email" />
          <TextFormField
            control={form.control}
            name="password"
            label="Mật khẩu"
            required
            type="password"
            autoComplete="new-password"
            description="Tối thiểu 8 ký tự, có chữ hoa, chữ thường và số"
          />
          <TextFormField
            control={form.control}
            name="confirmPassword"
            label="Nhập lại mật khẩu"
            required
            type="password"
            autoComplete="new-password"
          />
          <Button type="submit" className="w-full" disabled={register.isPending}>
            {register.isPending && <Loader2 className="animate-spin" />}
            Đăng ký
          </Button>
        </form>
      </Form>
    </AuthCard>
  );
}
