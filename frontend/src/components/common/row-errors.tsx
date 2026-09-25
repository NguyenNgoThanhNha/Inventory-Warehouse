import { TriangleAlert } from 'lucide-react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { RowErrorDto } from '@/types';

/** Per-row errors of an Excel import: row number as seen in Excel, column, reason. */
export function RowErrors({ errors, title }: { errors: RowErrorDto[]; title?: string }) {
  if (!errors.length) return null;
  const rows = new Set(errors.map((e) => e.row)).size;
  return (
    <div className="space-y-2" role="region" aria-label="Lỗi từng dòng">
      <div className="flex items-center gap-2 text-sm font-medium text-destructive">
        <TriangleAlert className="size-4" />
        {title ?? `${rows} dòng lỗi sẽ bị bỏ qua`}
      </div>
      <div className="max-h-64 overflow-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-20">Dòng</TableHead>
              <TableHead className="w-40">Cột</TableHead>
              <TableHead>Lỗi</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {errors.map((e, i) => (
              <TableRow key={`${e.row}-${e.column}-${i}`}>
                <TableCell className="tabular-nums">{e.row}</TableCell>
                <TableCell>{e.column ?? '—'}</TableCell>
                <TableCell>{e.message}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
