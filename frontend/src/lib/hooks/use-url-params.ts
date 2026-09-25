import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';

export type ParamPatch = Record<string, string | number | undefined | null>;

/**
 * URL search params as state. `update(patch)` sets / removes keys (empty → removed) and,
 * unless `resetPage` is false, drops `page` so filter changes go back to page 1.
 */
export function useUrlParams() {
  const [searchParams, setSearchParams] = useSearchParams();

  const update = useCallback(
    (patch: ParamPatch, resetPage = true) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          for (const [key, value] of Object.entries(patch)) {
            if (value === undefined || value === null || value === '') next.delete(key);
            else next.set(key, String(value));
          }
          if (resetPage) next.delete('page');
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  return [searchParams, update] as const;
}

export function oneOf<T extends string>(values: readonly T[], v: string | null): T | undefined {
  return v && (values as readonly string[]).includes(v) ? (v as T) : undefined;
}

export function toPositiveInt(v: string | null): number | undefined {
  const n = v ? Number(v) : NaN;
  return Number.isInteger(n) && n > 0 ? n : undefined;
}
