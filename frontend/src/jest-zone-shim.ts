/**
 * Jest shim for zone.js/testing compatibility in vitest.
 *
 * zone.js/testing patches test lifecycle to wrap tests in a ProxyZone,
 * which is required for Angular's fakeAsync(). It detects the framework by globals:
 *   - jest → patches Jest-compatible runners
 *   - jasmine → patches Jasmine
 *
 * Vitest provides `vi` (not `jest`) as its testing utility global.
 * This shim provides a minimal `jest` global so zone.js/testing fires its Jest patch,
 * wrapping vitest's globally available `it`/`test`/etc. in a ProxyZone.
 *
 * Required global stubs:
 *   - jest.__zone_patch__: false (prevents double-patching)
 *   - xit, fit, xtest: vitest doesn't provide these; we stub them to prevent
 *     zone.js from throwing when accessing .each on undefined
 *
 * This file must be the FIRST polyfill, before zone.js, so that when zone.js/testing
 * loads and runs patchJest(), the jest global is already defined.
 */

if (typeof globalThis !== 'undefined') {
  const g = globalThis as Record<string, unknown>;

  // Define jest global shim so zone.js/testing detects this as a Jest-like runner.
  if (typeof g['jest'] === 'undefined') {
    g['jest'] = { __zone_patch__: false };
  }

  // Vitest with globals:true provides `it` and `test` but not:
  //   - xit (skip a test), fit (focus a test), xtest (alias for xit)
  // zone.js/testing tries to access these and wrap their .each method.
  // We provide noop stubs with a .each property to prevent TypeError.
  function noopTest(name: string, fn?: () => void): void {
    // Intentional noop stub — vitest handles test skipping via it.skip/test.skip
    void name;
    void fn;
  }
  function noopEachFactory(): () => void {
    return () => void 0;
  }
  noopTest.each = noopEachFactory;
  noopTest.todo = (_name: string): void => {
    void _name;
  };
  noopTest.failing = noopTest;
  noopTest.skip = noopTest;
  noopTest.only = noopTest;

  if (typeof g['xit'] === 'undefined') g['xit'] = noopTest;
  if (typeof g['fit'] === 'undefined') g['fit'] = noopTest;
  if (typeof g['xtest'] === 'undefined') g['xtest'] = noopTest;

  if (typeof g['xdescribe'] === 'undefined') {
    function noopDescribe(name: string, fn?: () => void): void {
      void name;
      void fn;
    }
    (noopDescribe as unknown as Record<string, unknown>)['each'] = noopEachFactory;
    g['xdescribe'] = noopDescribe;
  }

  if (typeof g['fdescribe'] === 'undefined') {
    function noopDescribeFocused(name: string, fn?: () => void): void {
      void name;
      void fn;
    }
    (noopDescribeFocused as unknown as Record<string, unknown>)['each'] = noopEachFactory;
    g['fdescribe'] = noopDescribeFocused;
  }
}
