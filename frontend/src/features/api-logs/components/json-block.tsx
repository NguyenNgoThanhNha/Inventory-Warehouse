import { CopyButton } from '@/components/common/copy-button';
import { prettyJson } from '../api-log-query';

/** Titled <pre> with pretty-printed JSON and a copy button. */
export function JsonBlock({ title, value }: { title: string; value: string | null | undefined }) {
  const text = prettyJson(value);
  return (
    <section className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-medium">{title}</h3>
        {text && <CopyButton value={text} label={`Copy ${title.toLowerCase()}`} />}
      </div>
      {text ? (
        <pre
          aria-label={title}
          className="max-h-80 overflow-auto rounded-lg border bg-muted/50 p-3 font-mono text-xs leading-relaxed break-all whitespace-pre-wrap"
        >
          {text}
        </pre>
      ) : (
        <p className="text-sm text-muted-foreground">(trống)</p>
      )}
    </section>
  );
}
