/**
 * Tests for src/addon/scope.ts
 *
 * Production module path: src/addon/scope.ts
 * Exported:
 *   - resolveScopeRoot(scope: AddonScope, deps: { cwd: string; homeDir: string }): string
 *     - 'local' → path.resolve(cwd, '.claude')
 *     - 'global' → path.resolve(homeDir, '.claude')
 *
 * Key invariants:
 *   - Does NOT use resolveHome (marketplace ~/.claude-plugins)
 *   - Global scope is always homeDir/.claude, never cwd/.claude
 *   - Local scope is always cwd/.claude, never homeDir/.claude
 *   - Local and global paths never collide when cwd !== homeDir
 */

import { describe, it, expect } from 'vitest';
import * as path from 'node:path';

import { resolveScopeRoot } from '../addon/scope.js';
import type { AddonScope } from '../addon/manifest.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const FAKE_CWD = '/projects/my-project';
const FAKE_HOME = '/home/alice';

function deps(overrides?: { cwd?: string; homeDir?: string }): { cwd: string; homeDir: string } {
  return {
    cwd: overrides?.cwd ?? FAKE_CWD,
    homeDir: overrides?.homeDir ?? FAKE_HOME,
  };
}

// ---------------------------------------------------------------------------
// Local scope
// ---------------------------------------------------------------------------

describe('resolveScopeRoot — local scope', () => {
  it('resolves local to <cwd>/.claude', () => {
    const result = resolveScopeRoot('local', deps());
    expect(result).toBe(path.resolve(FAKE_CWD, '.claude'));
  });

  it('resolves local with a different cwd', () => {
    const cwd = '/workspace/other-project';
    const result = resolveScopeRoot('local', deps({ cwd }));
    expect(result).toBe(path.resolve(cwd, '.claude'));
  });

  it('result ends with /.claude', () => {
    const result = resolveScopeRoot('local', deps());
    expect(result.endsWith('/.claude') || result.endsWith('\\.claude')).toBe(true);
  });

  it('result contains the cwd base', () => {
    const result = resolveScopeRoot('local', deps());
    expect(result).toContain('my-project');
  });

  it('does NOT reference homeDir for local scope', () => {
    const result = resolveScopeRoot('local', deps());
    expect(result).not.toContain(FAKE_HOME);
  });

  it('does NOT produce the marketplace claude-plugins path', () => {
    const result = resolveScopeRoot('local', deps());
    expect(result).not.toContain('.claude-plugins');
  });
});

// ---------------------------------------------------------------------------
// Global scope
// ---------------------------------------------------------------------------

describe('resolveScopeRoot — global scope', () => {
  it('resolves global to <homeDir>/.claude', () => {
    const result = resolveScopeRoot('global', deps());
    expect(result).toBe(path.resolve(FAKE_HOME, '.claude'));
  });

  it('resolves global with a different homeDir', () => {
    const homeDir = '/home/bob';
    const result = resolveScopeRoot('global', deps({ homeDir }));
    expect(result).toBe(path.resolve(homeDir, '.claude'));
  });

  it('result ends with /.claude', () => {
    const result = resolveScopeRoot('global', deps());
    expect(result.endsWith('/.claude') || result.endsWith('\\.claude')).toBe(true);
  });

  it('result contains the homeDir base', () => {
    const result = resolveScopeRoot('global', deps());
    expect(result).toContain('alice');
  });

  it('does NOT reference cwd for global scope', () => {
    const result = resolveScopeRoot('global', deps());
    expect(result).not.toContain(FAKE_CWD);
  });

  it('does NOT produce the marketplace claude-plugins path', () => {
    const result = resolveScopeRoot('global', deps());
    expect(result).not.toContain('.claude-plugins');
  });
});

// ---------------------------------------------------------------------------
// Scope isolation
// ---------------------------------------------------------------------------

describe('resolveScopeRoot — scope isolation', () => {
  it('local path is never equal to global path when cwd !== homeDir', () => {
    const localRoot = resolveScopeRoot('local', deps());
    const globalRoot = resolveScopeRoot('global', deps());
    expect(localRoot).not.toBe(globalRoot);
  });

  it('local and global paths are independent even if both resolve under /.claude', () => {
    const localRoot = resolveScopeRoot('local', deps());
    const globalRoot = resolveScopeRoot('global', deps());
    // Different parents
    expect(path.dirname(localRoot)).not.toBe(path.dirname(globalRoot));
  });

  it('changing homeDir does not affect local scope resolution', () => {
    const rootA = resolveScopeRoot('local', deps({ homeDir: '/home/alice' }));
    const rootB = resolveScopeRoot('local', deps({ homeDir: '/home/bob' }));
    expect(rootA).toBe(rootB);
  });

  it('changing cwd does not affect global scope resolution', () => {
    const rootA = resolveScopeRoot('global', deps({ cwd: '/projects/a' }));
    const rootB = resolveScopeRoot('global', deps({ cwd: '/projects/b' }));
    expect(rootA).toBe(rootB);
  });

  it('local scope is deterministic for the same cwd', () => {
    const d = deps();
    expect(resolveScopeRoot('local', d)).toBe(resolveScopeRoot('local', d));
  });

  it('global scope is deterministic for the same homeDir', () => {
    const d = deps();
    expect(resolveScopeRoot('global', d)).toBe(resolveScopeRoot('global', d));
  });
});

// ---------------------------------------------------------------------------
// Return type is an absolute path
// ---------------------------------------------------------------------------

describe('resolveScopeRoot — return type', () => {
  const scopes: AddonScope[] = ['local', 'global'];

  for (const scope of scopes) {
    it(`returns an absolute path for scope=${scope}`, () => {
      const result = resolveScopeRoot(scope, deps());
      expect(path.isAbsolute(result)).toBe(true);
    });
  }
});
