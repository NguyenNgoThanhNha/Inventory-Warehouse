import { useCallback, useEffect, useMemo, useRef } from 'react';

export const SEARCH_DEBOUNCE_MS = 400;

/**
 * Debounced version of `fn`: `run(...args)` (re)starts the timer, `cancel()` drops a pending call,
 * `flush(...args)` cancels the timer and calls `fn` immediately. Always calls the latest `fn`;
 * a pending call is dropped on unmount.
 */
export function useDebouncedCallback<A extends unknown[]>(fn: (...args: A) => void, delay = SEARCH_DEBOUNCE_MS) {
  const fnRef = useRef(fn);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    fnRef.current = fn;
  }, [fn]);

  const cancel = useCallback(() => {
    if (timer.current !== undefined) clearTimeout(timer.current);
    timer.current = undefined;
  }, []);

  useEffect(() => cancel, [cancel]);

  return useMemo(
    () => ({
      run: (...args: A) => {
        cancel();
        timer.current = setTimeout(() => {
          timer.current = undefined;
          fnRef.current(...args);
        }, delay);
      },
      flush: (...args: A) => {
        cancel();
        fnRef.current(...args);
      },
      cancel,
    }),
    [cancel, delay],
  );
}
