import { useRef } from 'react';
import { Paperclip, X } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { formatFileSize, MAX_FILE_SIZE } from '@/lib/file';

/** Multi-file picker with a per-file size limit (default 10MB); selected files are listed with a remove button. */
export function FilePicker({
  files,
  onChange,
  disabled,
  label = 'Chọn file...',
  maxSize = MAX_FILE_SIZE,
  inputLabel = 'Đính kèm',
}: {
  files: File[];
  onChange: (files: File[]) => void;
  disabled?: boolean;
  label?: string;
  maxSize?: number;
  inputLabel?: string;
}) {
  const inputRef = useRef<HTMLInputElement>(null);

  const add = (list: FileList | null) => {
    if (!list) return;
    const accepted: File[] = [];
    for (const file of Array.from(list)) {
      if (file.size > maxSize) toast.error(`${file.name} vượt quá ${formatFileSize(maxSize)}`);
      else accepted.push(file);
    }
    if (accepted.length) onChange([...files, ...accepted]);
    if (inputRef.current) inputRef.current.value = '';
  };

  return (
    <div className="space-y-2">
      <input
        ref={inputRef}
        type="file"
        multiple
        hidden
        aria-label={inputLabel}
        disabled={disabled}
        onChange={(e) => add(e.target.files)}
      />
      <Button type="button" variant="outline" size="sm" disabled={disabled} onClick={() => inputRef.current?.click()}>
        <Paperclip /> {label}
      </Button>
      {files.length > 0 && (
        <ul className="space-y-1 text-sm">
          {files.map((f, i) => (
            <li key={`${f.name}-${i}`} className="flex items-center gap-2">
              <Paperclip className="size-3.5 text-muted-foreground" />
              <span className="truncate">{f.name}</span>
              <span className="text-muted-foreground">({formatFileSize(f.size)})</span>
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                aria-label={`Bỏ ${f.name}`}
                disabled={disabled}
                onClick={() => onChange(files.filter((_, j) => j !== i))}
              >
                <X />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
