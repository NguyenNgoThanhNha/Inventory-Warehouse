import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

export type Theme = 'light' | 'dark' | 'system';

interface ThemeContextValue {
  theme: Theme;
  resolvedTheme: 'light' | 'dark';
  setTheme: (theme: Theme) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

const STORAGE_KEY = 'inventory-theme';

function systemTheme(): 'light' | 'dark' {
  return typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function readStored(): Theme {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    return v === 'light' || v === 'dark' || v === 'system' ? v : 'system';
  } catch {
    return 'system';
  }
}

/**
 * Class-strategy dark mode for Vite (the shadcn/ui "Vite" theme provider pattern):
 * toggles `.dark` on <html>, persists the choice and follows the OS when set to "system".
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(readStored);
  const [system, setSystem] = useState<'light' | 'dark'>(systemTheme);

  useEffect(() => {
    const mql = window.matchMedia?.('(prefers-color-scheme: dark)');
    if (!mql) return;
    const onChange = () => setSystem(mql.matches ? 'dark' : 'light');
    mql.addEventListener?.('change', onChange);
    return () => mql.removeEventListener?.('change', onChange);
  }, []);

  const resolvedTheme = theme === 'system' ? system : theme;

  useEffect(() => {
    const root = document.documentElement;
    root.classList.remove('light', 'dark');
    root.classList.add(resolvedTheme);
    root.style.colorScheme = resolvedTheme;
  }, [resolvedTheme]);

  const value = useMemo<ThemeContextValue>(
    () => ({
      theme,
      resolvedTheme,
      setTheme: (t) => {
        try {
          localStorage.setItem(STORAGE_KEY, t);
        } catch {
          // ignore (private mode)
        }
        setThemeState(t);
      },
    }),
    [theme, resolvedTheme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme must be used within <ThemeProvider>');
  return ctx;
}
