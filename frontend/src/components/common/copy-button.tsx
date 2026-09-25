import { useState } from 'react';
import { Check, Copy } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { copyToClipboard } from '@/lib/api-errors';

export function CopyButton({ value, label = 'Copy', className }: { value: string; label?: string; className?: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      className={className}
      aria-label={label}
      onClick={() =>
        void copyToClipboard(value).then((ok) => {
          if (!ok) {
            toast.error('Không copy được');
            return;
          }
          setCopied(true);
          setTimeout(() => setCopied(false), 1500);
        })
      }
    >
      {copied ? <Check /> : <Copy />}
      {copied ? 'Đã copy' : label}
    </Button>
  );
}
