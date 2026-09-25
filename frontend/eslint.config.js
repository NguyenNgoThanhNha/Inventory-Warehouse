import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

/** Features are consumed only through their public index.ts (`@/features/<name>`). */
const NO_DEEP_FEATURE_IMPORTS = {
  group: ['@/features/*/**'],
  message: 'Import other features only via their public index: "@/features/<name>".',
};

/** Shared layers must not depend on features. */
const NO_FEATURE_IMPORTS = {
  group: ['@/features/**', '**/features/**'],
  message: 'Shared code (components/ui, components/common, lib, stores, types) must not import from features.',
};

export default tseslint.config(
  { ignores: ['dist', 'node_modules', 'coverage'] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ['**/*.{ts,tsx}'],
    languageOptions: { ecmaVersion: 2022, globals: globals.browser },
    plugins: { 'react-hooks': reactHooks, 'react-refresh': reactRefresh },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // feature modules intentionally co-locate small helpers/schemas with their components;
      // shadcn/ui files export variants next to components
      'react-refresh/only-export-components': 'off',
      'no-restricted-imports': ['error', { patterns: [NO_DEEP_FEATURE_IMPORTS] }],
    },
  },
  {
    files: ['src/components/**/*.{ts,tsx}', 'src/lib/**/*.{ts,tsx}', 'src/stores/**/*.{ts,tsx}', 'src/types/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [NO_DEEP_FEATURE_IMPORTS, NO_FEATURE_IMPORTS] }],
    },
  },
);
