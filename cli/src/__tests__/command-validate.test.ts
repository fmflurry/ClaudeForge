/**
 * Tests for src/commands/validate.ts
 *
 * Production module path: src/commands/validate.ts
 * Exported functions:
 *   - runValidate(args: ValidateArgs, deps: ValidateDeps): Promise<CommandResult>
 *       args: { pluginPath?: string }   — defaults to cwd
 *       deps: { fs?: ValidateFsPort; cwd?: string }
 *   - ValidateFsPort: { readFile(p: string): Promise<string>; exists(p: string): Promise<boolean> }
 *   - PluginManifest interface: {
 *       name: string;
 *       version: string;
 *       description: string;
 *       author: string;
 *       types: string[];
 *       languages: string[];
 *       entrypoints: string[];
 *       useCaseTags?: string[];
 *       dependencies?: Record<string, string>;
 *       license?: string;
 *       docsUrl?: string;
 *     }
 *   - validateManifest(manifest: unknown): ValidationResult
 *   - ValidationResult: { valid: boolean; errors: string[]; warnings: string[] }
 *   - CommandResult: { exitCode: number; output: string }
 *
 * Exit codes:
 *   0 — valid (may have warnings)
 *   1 — invalid manifest fields or semver error
 *   2 — manifest file not found
 *
 * VERBATIM spec strings (from spec.md + design.md):
 *   - missing manifest: "Missing required field: <field>"
 *   - no manifest file: mention "plugin.json"
 *   - valid types values: skill|hook|agent|command|plugin
 *   - dependency warning (not error): warns but exits 0
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/commands/validate.ts is created (RED state).
import { runValidate, validateManifest } from '../commands/validate.js';
import type { CommandResult, ValidateFsPort, PluginManifest, ValidationResult } from '../commands/validate.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeFakeFs(files: Record<string, string>): ValidateFsPort {
  return {
    readFile: vi.fn(async (p: string) => {
      if (p in files) return files[p];
      throw Object.assign(new Error('ENOENT'), { code: 'ENOENT' });
    }),
    exists: vi.fn(async (p: string) => p in files),
  };
}

function makeValidManifest(overrides?: Partial<PluginManifest>): PluginManifest {
  return {
    name: '@test/plugin',
    version: '1.0.0',
    description: 'A valid plugin for testing',
    author: 'Test Author',
    types: ['skill'],
    languages: ['typescript'],
    entrypoints: ['src/index.ts'],
    ...overrides,
  };
}

const VALID_MANIFEST_JSON = (overrides?: Partial<PluginManifest>): string =>
  JSON.stringify(makeValidManifest(overrides));

// ---------------------------------------------------------------------------
// validateManifest — pure function tests
// ---------------------------------------------------------------------------

describe('validateManifest – valid manifest', () => {
  it('returns valid=true for a fully valid manifest', () => {
    const result: ValidationResult = validateManifest(makeValidManifest());
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('includes no errors for optional missing fields', () => {
    const manifest = makeValidManifest({ license: undefined, docsUrl: undefined });
    const result = validateManifest(manifest);
    expect(result.valid).toBe(true);
  });
});

describe('validateManifest – missing required fields', () => {
  const requiredFields: (keyof PluginManifest)[] = [
    'name', 'version', 'description', 'author', 'types', 'languages', 'entrypoints',
  ];

  for (const field of requiredFields) {
    it(`returns valid=false and error message when "${field}" is missing`, () => {
      const manifest = makeValidManifest({ [field]: undefined } as Partial<PluginManifest>);
      const result = validateManifest(manifest);
      expect(result.valid).toBe(false);
      // Spec: "Missing required field: <field>"
      expect(result.errors.some((e) => e.includes(field))).toBe(true);
    });
  }
});

describe('validateManifest – invalid types array', () => {
  it('returns error for empty types array', () => {
    const result = validateManifest(makeValidManifest({ types: [] }));
    expect(result.valid).toBe(false);
  });

  it('returns error for invalid type value (not skill|hook|agent|command|plugin)', () => {
    const result = validateManifest(makeValidManifest({ types: ['invalid-type'] }));
    expect(result.valid).toBe(false);
  });

  it('accepts all valid type values', () => {
    const validTypes = ['skill', 'hook', 'agent', 'command', 'plugin'] as const;
    for (const t of validTypes) {
      const result = validateManifest(makeValidManifest({ types: [t] }));
      expect(result.valid).toBe(true);
    }
  });
});

describe('validateManifest – semver', () => {
  it('returns error for invalid semver version', () => {
    const result = validateManifest(makeValidManifest({ version: 'not-semver' }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('version'))).toBe(true);
  });

  it('accepts valid semver: 1.0.0', () => {
    expect(validateManifest(makeValidManifest({ version: '1.0.0' })).valid).toBe(true);
  });

  it('accepts valid semver with pre-release: 1.0.0-beta.1', () => {
    expect(validateManifest(makeValidManifest({ version: '1.0.0-beta.1' })).valid).toBe(true);
  });
});

describe('validateManifest – dependency warnings', () => {
  it('adds a warning (not error) when dependencies are declared (display only, no resolution)', () => {
    const manifest = makeValidManifest({
      dependencies: { 'framework-x': '>=3.0.0' },
    });
    const result = validateManifest(manifest);
    expect(result.valid).toBe(true);
    expect(result.warnings.length).toBeGreaterThan(0);
  });
});

describe('validateManifest – entrypoints', () => {
  it('returns error for empty entrypoints array', () => {
    const result = validateManifest(makeValidManifest({ entrypoints: [] }));
    expect(result.valid).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// runValidate – command integration
// ---------------------------------------------------------------------------

describe('runValidate – valid plugin directory', () => {
  it('returns exitCode 0 for a valid plugin.json', async () => {
    const fakeFs = makeFakeFs({
      '/my/plugin/plugin.json': VALID_MANIFEST_JSON(),
    });
    const result: CommandResult = await runValidate(
      { pluginPath: '/my/plugin' },
      { fs: fakeFs },
    );
    expect(result.exitCode).toBe(0);
  });

  it('output confirms validation passed', async () => {
    const fakeFs = makeFakeFs({
      '/my/plugin/plugin.json': VALID_MANIFEST_JSON(),
    });
    const result = await runValidate({ pluginPath: '/my/plugin' }, { fs: fakeFs });
    expect(result.output.toLowerCase()).toMatch(/valid|success|ok/);
  });
});

describe('runValidate – missing manifest', () => {
  it('returns exitCode 2 when plugin.json is not found', async () => {
    const fakeFs = makeFakeFs({});
    const result: CommandResult = await runValidate(
      { pluginPath: '/my/plugin' },
      { fs: fakeFs },
    );
    expect(result.exitCode).toBe(2);
  });

  it('output mentions plugin.json when manifest is missing', async () => {
    const fakeFs = makeFakeFs({});
    const result = await runValidate({ pluginPath: '/my/plugin' }, { fs: fakeFs });
    expect(result.output).toContain('plugin.json');
  });

  it('suggests using claude plugin scaffold when manifest is missing', async () => {
    const fakeFs = makeFakeFs({});
    const result = await runValidate({ pluginPath: '/my/plugin' }, { fs: fakeFs });
    expect(result.output).toContain('claude plugin scaffold');
  });
});

describe('runValidate – invalid manifest content', () => {
  it('returns exitCode 1 when manifest has missing required field', async () => {
    const manifest = makeValidManifest({ types: undefined as unknown as string[] });
    const fakeFs = makeFakeFs({
      '/my/plugin/plugin.json': JSON.stringify(manifest),
    });
    const result = await runValidate({ pluginPath: '/my/plugin' }, { fs: fakeFs });
    expect(result.exitCode).toBe(1);
  });

  it('output includes "Missing required field" for each missing field', async () => {
    const manifest = { name: '@test/plugin', version: '1.0.0' };
    const fakeFs = makeFakeFs({
      '/my/plugin/plugin.json': JSON.stringify(manifest),
    });
    const result = await runValidate({ pluginPath: '/my/plugin' }, { fs: fakeFs });
    expect(result.output).toContain('Missing required field');
  });

  it('returns exitCode 1 when manifest has corrupt JSON', async () => {
    const fakeFs = makeFakeFs({
      '/my/plugin/plugin.json': '{invalid json',
    });
    const result = await runValidate({ pluginPath: '/my/plugin' }, { fs: fakeFs });
    expect(result.exitCode).toBe(1);
  });
});

describe('runValidate – dependency conflict warnings', () => {
  it('returns exitCode 0 but output contains warning when deps are present', async () => {
    const manifest = makeValidManifest({ dependencies: { 'some-framework': '>=2.0.0' } });
    const fakeFs = makeFakeFs({
      '/my/plugin/plugin.json': JSON.stringify(manifest),
    });
    const result = await runValidate({ pluginPath: '/my/plugin' }, { fs: fakeFs });
    expect(result.exitCode).toBe(0);
    expect(result.output.toLowerCase()).toMatch(/warn|dependenc/);
  });
});
