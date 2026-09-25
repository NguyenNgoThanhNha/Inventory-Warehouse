import type { ReactNode } from 'react';
import { Checkbox } from '@/components/ui/checkbox';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import {
  ACTIVITY_ACTIONS,
  type ActivityAction,
  type ActivityDto,
  type ActivityPermissionInput,
  type CrudFlags,
} from '@/types';

const FLAG: Record<ActivityAction, keyof CrudFlags> = { C: 'c', R: 'r', U: 'u', D: 'd' };
const ACTION_LABEL: Record<ActivityAction, string> = { C: 'Create', R: 'Read', U: 'Update', D: 'Delete' };

/**
 * Flags that are meaningful for the known activity codes (API contract table).
 * Unknown codes: all flags enabled.
 */
const APPLICABLE: Record<string, readonly ActivityAction[]> = {
  PRODUCT: ['C', 'R', 'U', 'D'],
  WAREHOUSE: ['C', 'R', 'U'],
  SUPPLIER: ['C', 'R', 'U'],
  GOODS_RECEIPT: ['C', 'R', 'U', 'D'],
  GOODS_ISSUE: ['C', 'R', 'U', 'D'],
  TRANSFER: ['C', 'R', 'U', 'D'],
  STOCK_TAKE: ['C', 'R', 'U', 'D'],
  STOCK_REPORT: ['R'],
  IMPORT_EXPORT: ['C', 'R'],
  USER: ['R', 'U'],
  ROLE: ['C', 'R', 'U', 'D'],
  API_LOG: ['R'],
};

export const isApplicable = (code: string, action: ActivityAction) =>
  (APPLICABLE[code] ?? ACTIVITY_ACTIONS).includes(action);

const emptyFlags: CrudFlags = { c: false, r: false, u: false, d: false };

/** Removes rows where every flag is false (the API drops them anyway). */
export function normalizePermissions(value: ActivityPermissionInput[]): ActivityPermissionInput[] {
  return value.filter((p) => p.c || p.r || p.u || p.d);
}

/** Maps ActivityPermissionDto-like rows to API inputs. */
export function toPermissionInputs(rows: (CrudFlags & { activityId: string })[]): ActivityPermissionInput[] {
  return rows.map(({ activityId, c, r, u, d }) => ({ activityId, c, r, u, d }));
}

/** Returns a new value with one flag toggled. */
export function togglePermission(
  value: ActivityPermissionInput[],
  activityId: string,
  action: ActivityAction,
  checked: boolean,
): ActivityPermissionInput[] {
  const key = FLAG[action];
  const exists = value.some((p) => p.activityId === activityId);
  if (!exists) return [...value, { activityId, ...emptyFlags, [key]: checked }];
  return value.map((p) => (p.activityId === activityId ? { ...p, [key]: checked } : p));
}

export interface PermissionMatrixProps {
  activities: ActivityDto[];
  value: ActivityPermissionInput[];
  onChange?: (next: ActivityPermissionInput[]) => void;
  readOnly?: boolean;
  loading?: boolean;
  /** Optional extra column (e.g. effective permissions). */
  extraColumn?: { title: ReactNode; render: (activity: ActivityDto) => ReactNode };
}

/** Reusable activity × C/R/U/D checkbox matrix. */
export function PermissionMatrix({ activities, value, onChange, readOnly, loading, extraColumn }: PermissionMatrixProps) {
  const flagsOf = (activityId: string): CrudFlags => value.find((p) => p.activityId === activityId) ?? emptyFlags;

  const setRow = (activity: ActivityDto, checked: boolean) => {
    let next = value;
    for (const action of ACTIVITY_ACTIONS) {
      if (isApplicable(activity.code, action)) next = togglePermission(next, activity.id, action, checked);
    }
    onChange?.(next);
  };

  const colCount = 1 + ACTIVITY_ACTIONS.length + (readOnly ? 0 : 1) + (extraColumn ? 1 : 0);

  return (
    <div className="overflow-x-auto rounded-lg border" data-testid="permission-matrix">
      <Table>
        <TableHeader className="bg-muted/50">
          <TableRow>
            <TableHead>Chức năng</TableHead>
            {ACTIVITY_ACTIONS.map((action) => (
              <TableHead key={action} className="w-14 text-center">
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span className="cursor-help">{action}</span>
                  </TooltipTrigger>
                  <TooltipContent>{ACTION_LABEL[action]}</TooltipContent>
                </Tooltip>
              </TableHead>
            ))}
            {!readOnly && <TableHead className="w-16 text-center">Tất cả</TableHead>}
            {extraColumn && <TableHead className="w-32">{extraColumn.title}</TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {loading ? (
            Array.from({ length: 4 }, (_, i) => (
              <TableRow key={i}>
                <TableCell colSpan={colCount}>
                  <Skeleton className="h-5 w-full" />
                </TableCell>
              </TableRow>
            ))
          ) : activities.length === 0 ? (
            <TableRow>
              <TableCell colSpan={colCount} className="h-16 text-center text-muted-foreground">
                Không có chức năng
              </TableCell>
            </TableRow>
          ) : (
            activities.map((a) => {
              const flags = flagsOf(a.id);
              const applicable = ACTIVITY_ACTIONS.filter((x) => isApplicable(a.code, x));
              const on = applicable.filter((x) => flags[FLAG[x]]).length;
              return (
                <TableRow key={a.id}>
                  <TableCell className="whitespace-normal">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{a.name}</span>
                      <code className="rounded bg-muted px-1 py-0.5 text-[11px] text-muted-foreground">{a.code}</code>
                    </div>
                    {a.description && <div className="text-xs text-muted-foreground">{a.description}</div>}
                  </TableCell>
                  {ACTIVITY_ACTIONS.map((action) => {
                    const checked = flags[FLAG[action]];
                    return (
                      <TableCell key={action} className="text-center">
                        <Checkbox
                          aria-label={`${a.code} ${action}`}
                          checked={checked}
                          disabled={readOnly || (!isApplicable(a.code, action) && !checked)}
                          onCheckedChange={(v) => onChange?.(togglePermission(value, a.id, action, v === true))}
                        />
                      </TableCell>
                    );
                  })}
                  {!readOnly && (
                    <TableCell className="text-center">
                      <Checkbox
                        aria-label={`${a.code} all`}
                        checked={on > 0 && on === applicable.length ? true : on > 0 ? 'indeterminate' : false}
                        onCheckedChange={() => setRow(a, on !== applicable.length)}
                      />
                    </TableCell>
                  )}
                  {extraColumn && <TableCell>{extraColumn.render(a)}</TableCell>}
                </TableRow>
              );
            })
          )}
        </TableBody>
      </Table>
    </div>
  );
}

/** Compact C/R/U/D text for read-only display. */
export function FlagsText({ flags }: { flags: CrudFlags | undefined }) {
  const on = ACTIVITY_ACTIONS.filter((a) => flags?.[FLAG[a]]);
  return on.length ? (
    <code className="rounded bg-muted px-1.5 py-0.5 text-xs">{on.join('')}</code>
  ) : (
    <span className="text-muted-foreground">—</span>
  );
}
