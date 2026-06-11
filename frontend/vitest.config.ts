/**
 * Standalone vitest configuration for running specs via `npx vitest run` outside
 * the Angular build pipeline.
 *
 * The Angular unit-test runner (`ng test` / `@angular/build:unit-test`) resolves the
 * `@/*` path alias from tsconfig.json via the Angular compiler. When vitest is invoked
 * directly (e.g. `npx vitest run <spec>`), that compiler layer is absent, so the alias
 * must be declared here explicitly.
 *
 * Alias: `@/*` → `<root>/src/app/*` (matches `tsconfig.json` paths entry).
 *
 * Environment: jsdom — Angular TestBed requires a DOM.
 * Globals: true — exposes `describe`, `it`, `expect`, `vi`, etc. globally (matches tsconfig.spec.json).
 */

import { resolve } from 'path';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  resolve: {
    alias: {
      '@/': resolve(__dirname, 'src/app') + '/',
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: [
      './src/jest-zone-shim.ts',
      './src/vitest-angular-setup.ts',
    ],
  },
});
