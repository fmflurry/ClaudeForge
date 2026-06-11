/**
 * Angular TestBed initialization for standalone `npx vitest run` invocations.
 *
 * When tests run via `ng test` (`@angular/build:unit-test`), Angular injects a virtual
 * `angular:test-bed-init` entry point that calls `getTestBed().initTestEnvironment()`.
 * When vitest is invoked directly, that virtual file is absent, so we replicate the
 * minimal setup here.
 *
 * This file is referenced in vitest.config.ts `setupFiles` and must run before any spec.
 */

import { getTestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { afterEach, beforeEach } from 'vitest';

// eslint-disable-next-line @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-explicit-any
const getCleanupHook = (getTestBed as unknown as any)['ɵgetCleanupHook'] as ((arg: boolean) => () => void) | undefined;

// Register cleanup hooks if available (Angular 17+)
if (getCleanupHook) {
  beforeEach(getCleanupHook(false));
  afterEach(getCleanupHook(true));
}

const ANGULAR_TESTBED_SETUP = Symbol.for('@angular/cli/testbed-setup');
const g = globalThis as Record<symbol, boolean>;

if (!g[ANGULAR_TESTBED_SETUP]) {
  g[ANGULAR_TESTBED_SETUP] = true;

  getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting(), {
    errorOnUnknownElements: false,
    errorOnUnknownProperties: false,
    teardown: { destroyAfterEach: true },
  });
}
