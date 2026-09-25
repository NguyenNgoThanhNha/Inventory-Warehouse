import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { CircleCheck, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Form } from '@/components/ui/form';
import { EmptyState } from '@/components/common/empty-state';
import { TextFormField } from '@/components/common/form-fields';
import { AuthCard } from '../components/auth-card';
import { useForgotPassword } from '../hooks/use-auth';
import { forgotPasswordSchema, type ForgotPasswordForm } from '../schemas';

const backToLogin = (
  <Link to="/login" className="text-primary underline-offset-4 hover:underline">
    Quay lại đăng nhập
  </Link>
);

export function ForgotPasswordPage() {
  const forgot = useForgotPassword();
  const form = useForm<ForgotPasswordForm>({ resolver: zodResolver(forgotPasswordSchema), defaultValues: { email: '' } });

  if (forgot.isSuccess) {
    return (
      <AuthCard title="Quên mật khẩu" footer={backToLogin}>
        <EmptyState
          icon={<CircleCheck className="text-emerald-600" />}
          title="Đã gửi yêu cầu"
          description="Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu."
        />
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Quên mật khẩu" description="Nhập email để nhận link đặt lại mật khẩu" footer={backToLogin}>
      <Form {...form}>
        <form onSubmit={form.handleSubmit((v) => forgot.mutate(v.email))} noValidate className="space-y-4">
          <TextFormField control={form.control} name="email" label="Email" required type="email" autoComplete="email" />
          <Button type="submit" className="w-full" disabled={forgot.isPending}>
            {forgot.isPending && <Loader2 className="animate-spin" />}
            Gửi link đặt lại mật khẩu
          </Button>
        </form>
      </Form>
    </AuthCard>
  );
}
