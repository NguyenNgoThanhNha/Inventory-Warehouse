import { useState, type ReactNode } from 'react';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { ThemeProvider, useTheme } from '@/components/common/theme-provider';
import { Toaster } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import { createQueryClient } from '@/lib/query-client';

/** The generated shadcn Toaster reads next-themes; we pass our resolved theme explicitly. */
function ThemedToaster() {
  const { resolvedTheme } = useTheme();
  return <Toaster theme={resolvedTheme} richColors closeButton position="top-right" />;
}

/** App-wide providers: theme (class strategy), React Query, tooltips and toasts. */
export function AppProviders({ children, queryClient }: { children: ReactNode; queryClient?: QueryClient }) {
  const [client] = useState(() => queryClient ?? createQueryClient());
  return (
    <ThemeProvider>
      <QueryClientProvider client={client}>
        <TooltipProvider delayDuration={200}>
          {children}
          <ThemedToaster />
        </TooltipProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
