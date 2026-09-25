import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { AlertCircle, Loader2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Form } from '@/components/ui/form';
import { TextFormField } from '@/components/common/form-fields';
import { getErrorMessage, getStatus } from '@/lib/api-errors';
import { AuthCard } from '../components/auth-card';
import { useLogin } from '../hooks/use-auth';
import { loginSchema, type LoginForm } from '../schemas';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;
  const login = useLogin();

  const form = useForm<LoginForm>({ resolver: zodResolver(loginSchema), defaultValues: { email: '', password: '' } });

  const onSubmit = form.handleSubmit((values) =>
    login.mutate(values, {
      onSuccess: () => navigate(from && from !== '/login' ? from : '/', { replace: true }),
    }),
  );

  const errorText = login.error
    ? getStatus(login.error) === 401
      ? 'Email hoặc mật khẩu không đúng'
      : getErrorMessage(login.error)
    : null;

  return (
    <AuthCard
      title="Đăng nhập Quản lý kho"
      description="Đăng nhập để quản lý tồn kho và chứng từ nhập – xuất"
      footer={
        <span>
          Chưa có tài khoản?{' '}
          <Link to="/register" className="text-primary underline-offset-4 hover:underline">
            Đăng ký
          </Link>
        </span>
      }
    >
      {errorText && (
        <Alert variant="destructive" className="mb-4">
          <AlertCircle />
          <AlertDescription>{errorText}</AlertDescription>
        </Alert>
      )}
      <Form {...form}>
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <TextFormField
            control={form.control}
            name="email"
            label="Email"
            required
            type="email"
            autoComplete="email"
            placeholder="you@company.com"
          />
          <TextFormField
            control={form.control}
            name="password"
            label="Mật khẩu"
            required
            type="password"
            autoComplete="current-password"
          />
          <div className="text-right text-sm">
            <Link to="/forgot-password" className="text-primary underline-offset-4 hover:underline">
              Quên mật khẩu?
            </Link>
          </div>
          <Button type="submit" className="w-full" disabled={login.isPending}>
            {login.isPending && <Loader2 className="animate-spin" />}
            Đăng nhập
          </Button>
        </form>
      </Form>
    </AuthCard>
  );
}
