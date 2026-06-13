/**
 * Tests for src/addon/manifest.ts
 *
 * Production module path: src/addon/manifest.ts
 * Exported:
 *   - AddonType: 'hook' | 'plugin' | 'skill' | 'agent'
 *   - AddonScope: 'local' | 'global'
 *   - HookRegistration: { event: string; matcher: string; command: string; type?: string }
 *   - AddonManifest: { name, version, type, supportedScopes, files, hook? }
 *   - AddonValidationResult: { valid: boolean; errors: string[]; warnings: string[] }
 *   - normalizeSupportedScopes(input: unknown): AddonScope[]
 *   - validateAddonManifest(input: unknown): AddonValidationResult
 */

import { describe, it, expect } from 'vitest';

import {
  normalizeSupportedScopes,
  validateAddonManifest,
} from '../addon/manifest.js';
import type { AddonManifest, AddonScope, AddonValidationResult } from '../addon/manifest.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeValidHookManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'my-hook',
    version: '1.0.0',
    type: 'hook',
    supportedScopes: ['local', 'global'],
    files: ['hooks/auth.sh'],
    hook: {
      event: 'PreToolUse',
      matcher: 'Bash',
      command: 'hooks/auth.sh',
    },
    ...overrides,
  };
}

function makeValidSkillManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'my-skill',
    version: '2.1.0',
    type: 'skill',
    supportedScopes: ['local', 'global'],
    files: ['SKILL.md', 'scripts/run.sh'],
    ...overrides,
  };
}

function makeValidAgentManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'my-agent',
    version: '0.1.0',
    type: 'agent',
    supportedScopes: ['local', 'global'],
    files: ['my-agent.md'],
    ...overrides,
  };
}

function makeValidPluginManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'my-plugin',
    version: '1.0.0',
    type: 'plugin',
    supportedScopes: ['global'],
    files: ['bundle/index.js', '.claude-plugin/plugin.json'],
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// normalizeSupportedScopes
// ---------------------------------------------------------------------------

describe('normalizeSupportedScopes', () => {
  it('returns an explicit array unchanged', () => {
    expect(normalizeSupportedScopes(['local', 'global'])).toEqual(['local', 'global']);
  });

  it('expands "both" shorthand to ["local","global"]', () => {
    expect(normalizeSupportedScopes('both')).toEqual(['local', 'global']);
  });

  it('returns ["local"] when given only local', () => {
    expect(normalizeSupportedScopes(['local'])).toEqual(['local']);
  });

  it('returns ["global"] when given only global', () => {
    expect(normalizeSupportedScopes(['global'])).toEqual(['global']);
  });

  it('returns [] for an empty array (caller surfaces the error)', () => {
    expect(normalizeSupportedScopes([])).toEqual([]);
  });

  it('returns [] for an invalid scope value', () => {
    expect(normalizeSupportedScopes(['invalid'])).toEqual([]);
  });

  it('returns [] for a non-array, non-"both" string', () => {
    expect(normalizeSupportedScopes('local')).toEqual([]);
  });

  it('returns [] for null', () => {
    expect(normalizeSupportedScopes(null)).toEqual([]);
  });

  it('returns [] for undefined', () => {
    expect(normalizeSupportedScopes(undefined)).toEqual([]);
  });

  it('returns [] for a number', () => {
    expect(normalizeSupportedScopes(42)).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — not an object
// ---------------------------------------------------------------------------

describe('validateAddonManifest — non-object input', () => {
  it('returns valid=false for null', () => {
    const result: AddonValidationResult = validateAddonManifest(null);
    expect(result.valid).toBe(false);
    expect(result.errors.length).toBeGreaterThan(0);
  });

  it('returns valid=false for a string', () => {
    const result = validateAddonManifest('not-an-object');
    expect(result.valid).toBe(false);
  });

  it('returns valid=false for a number', () => {
    expect(validateAddonManifest(42).valid).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — valid manifests
// ---------------------------------------------------------------------------

describe('validateAddonManifest — valid manifests accepted', () => {
  it('accepts a fully valid hook manifest', () => {
    const result = validateAddonManifest(makeValidHookManifest());
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('accepts a fully valid skill manifest', () => {
    const result = validateAddonManifest(makeValidSkillManifest());
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('accepts a fully valid agent manifest', () => {
    const result = validateAddonManifest(makeValidAgentManifest());
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('accepts a fully valid plugin manifest', () => {
    const result = validateAddonManifest(makeValidPluginManifest());
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('accepts semver with pre-release: 1.0.0-beta.1', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ version: '1.0.0-beta.1' }));
    expect(result.valid).toBe(true);
  });

  it('accepts semver with build metadata: 1.0.0+build.1', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ version: '1.0.0+build.1' }));
    expect(result.valid).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — missing required fields
// ---------------------------------------------------------------------------

describe('validateAddonManifest — missing required fields', () => {
  it('rejects when name is missing', () => {
    const manifest = makeValidSkillManifest();
    const result = validateAddonManifest({ version: manifest.version, type: manifest.type, supportedScopes: manifest.supportedScopes, files: manifest.files });
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('name'))).toBe(true);
  });

  it('rejects when name is empty string', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ name: '' }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('name'))).toBe(true);
  });

  it('rejects when version is missing', () => {
    const manifest = makeValidSkillManifest();
    const result = validateAddonManifest({ name: manifest.name, type: manifest.type, supportedScopes: manifest.supportedScopes, files: manifest.files });
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('version'))).toBe(true);
  });

  it('rejects when type is missing', () => {
    const manifest = makeValidSkillManifest();
    const result = validateAddonManifest({ name: manifest.name, version: manifest.version, supportedScopes: manifest.supportedScopes, files: manifest.files });
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('type'))).toBe(true);
  });

  it('rejects when files is missing', () => {
    const manifest = makeValidSkillManifest();
    const result = validateAddonManifest({ name: manifest.name, version: manifest.version, type: manifest.type, supportedScopes: manifest.supportedScopes });
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('files'))).toBe(true);
  });

  it('rejects when supportedScopes is missing', () => {
    const manifest = makeValidSkillManifest();
    const result = validateAddonManifest({ name: manifest.name, version: manifest.version, type: manifest.type, files: manifest.files });
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('scope'))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — unknown type
// ---------------------------------------------------------------------------

describe('validateAddonManifest — unknown type value', () => {
  it('rejects an unknown type and names it in the error', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ type: 'unknown-type' as 'skill' }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.includes('unknown-type'))).toBe(true);
  });

  it('error message lists the valid types', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ type: 'command' as 'skill' }));
    expect(result.valid).toBe(false);
    const combined = result.errors.join(' ');
    expect(combined).toMatch(/hook/);
    expect(combined).toMatch(/plugin/);
    expect(combined).toMatch(/skill/);
    expect(combined).toMatch(/agent/);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — version semver
// ---------------------------------------------------------------------------

describe('validateAddonManifest — semver validation', () => {
  it('rejects "1.0" (missing patch)', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ version: '1.0' }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('version'))).toBe(true);
  });

  it('rejects "not-semver"', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ version: 'not-semver' }));
    expect(result.valid).toBe(false);
  });

  it('rejects "v1.0.0" (leading v not allowed)', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ version: 'v1.0.0' }));
    expect(result.valid).toBe(false);
  });

  it('accepts "1.0.0"', () => {
    expect(validateAddonManifest(makeValidSkillManifest({ version: '1.0.0' })).valid).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — files array
// ---------------------------------------------------------------------------

describe('validateAddonManifest — files array', () => {
  it('rejects empty files array', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: [] }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('files'))).toBe(true);
  });

  it('rejects absolute paths in files', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['/absolute/path/auth.ts'] }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.includes('/absolute/path/auth.ts') || e.toLowerCase().includes('absolute'))).toBe(true);
  });

  it('rejects ".." segments in files', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['../escape/file.ts'] }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.includes('..'))).toBe(true);
  });

  it('rejects ".." deep in path', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['foo/../../bar.ts'] }));
    expect(result.valid).toBe(false);
  });

  it('rejects paths with null byte', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['foo\0bar.ts'] }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.includes('foo\0bar.ts') || e.toLowerCase().includes('null') || e.toLowerCase().includes('unsafe'))).toBe(true);
  });

  it('accepts relative paths', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['SKILL.md', 'scripts/run.sh'] }));
    expect(result.valid).toBe(true);
  });

  it('rejects non-array files', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: 'not-an-array' as unknown as string[] }));
    expect(result.valid).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — supportedScopes
// ---------------------------------------------------------------------------

describe('validateAddonManifest — supportedScopes', () => {
  it('honors an explicit ["local","global"] array', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ supportedScopes: ['local', 'global'] }));
    expect(result.valid).toBe(true);
  });

  it('honors a ["local"] array', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ supportedScopes: ['local'] }));
    expect(result.valid).toBe(true);
  });

  it('accepts "both" shorthand (raw input) and expands it', () => {
    const result = validateAddonManifest({
      ...makeValidSkillManifest(),
      supportedScopes: 'both',
    });
    expect(result.valid).toBe(true);
  });

  it('rejects an empty supportedScopes array', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ supportedScopes: [] }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('scope'))).toBe(true);
  });

  it('rejects an invalid scope value', () => {
    const result = validateAddonManifest(
      makeValidSkillManifest({ supportedScopes: ['invalid'] as unknown as AddonScope[] }),
    );
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('scope'))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — plugin-global-only constraint
// ---------------------------------------------------------------------------

describe('validateAddonManifest — plugin is global-only', () => {
  it('rejects plugin with supportedScopes=["local"]', () => {
    const result = validateAddonManifest(makeValidPluginManifest({ supportedScopes: ['local'] }));
    expect(result.valid).toBe(false);
    const combined = result.errors.join(' ');
    expect(combined.toLowerCase()).toMatch(/plugin.*global|global.*plugin/);
  });

  it('rejects plugin with supportedScopes=["local","global"] (both forms)', () => {
    const result = validateAddonManifest(makeValidPluginManifest({ supportedScopes: ['local', 'global'] }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('global'))).toBe(true);
  });

  it('accepts plugin with supportedScopes=["global"]', () => {
    const result = validateAddonManifest(makeValidPluginManifest({ supportedScopes: ['global'] }));
    expect(result.valid).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — hook type requires hook object
// ---------------------------------------------------------------------------

describe('validateAddonManifest — hook type requires hook object', () => {
  it('rejects a hook manifest with no hook object', () => {
    const manifest = makeValidHookManifest();
    const result = validateAddonManifest({ name: manifest.name, version: manifest.version, type: manifest.type, supportedScopes: manifest.supportedScopes, files: manifest.files });
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('hook'))).toBe(true);
  });

  it('rejects when hook.command does not match any files entry', () => {
    const result = validateAddonManifest(
      makeValidHookManifest({
        files: ['hooks/auth.sh'],
        hook: {
          event: 'PreToolUse',
          matcher: 'Bash',
          command: 'hooks/missing.sh',
        },
      }),
    );
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('command'))).toBe(true);
  });

  it('accepts hook manifest where hook.command matches a files entry', () => {
    const result = validateAddonManifest(makeValidHookManifest());
    expect(result.valid).toBe(true);
  });

  it('hook.event is required in hook object', () => {
    const result = validateAddonManifest(
      makeValidHookManifest({
        hook: { event: '', matcher: 'Bash', command: 'hooks/auth.sh' },
      }),
    );
    expect(result.valid).toBe(false);
  });

  it('hook.matcher is required in hook object', () => {
    const result = validateAddonManifest(
      makeValidHookManifest({
        hook: { event: 'PreToolUse', matcher: '', command: 'hooks/auth.sh' },
      }),
    );
    expect(result.valid).toBe(false);
  });

  it('hook.command is required in hook object', () => {
    const result = validateAddonManifest(
      makeValidHookManifest({
        hook: { event: 'PreToolUse', matcher: 'Bash', command: '' },
      }),
    );
    expect(result.valid).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — non-hook types must not declare hook object
// ---------------------------------------------------------------------------

describe('validateAddonManifest — non-hook types must not declare hook', () => {
  it('rejects a skill manifest with a hook object', () => {
    const result = validateAddonManifest({
      ...makeValidSkillManifest(),
      hook: { event: 'PreToolUse', matcher: 'Bash', command: 'SKILL.md' },
    });
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('hook'))).toBe(true);
  });

  it('rejects an agent manifest with a hook object', () => {
    const result = validateAddonManifest({
      ...makeValidAgentManifest(),
      hook: { event: 'PreToolUse', matcher: 'Bash', command: 'my-agent.md' },
    });
    expect(result.valid).toBe(false);
  });

  it('rejects a plugin manifest with a hook object', () => {
    const result = validateAddonManifest({
      ...makeValidPluginManifest(),
      hook: { event: 'PreToolUse', matcher: 'Bash', command: 'bundle/index.js' },
    });
    expect(result.valid).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — agent: exactly one file
// ---------------------------------------------------------------------------

describe('validateAddonManifest — agent must have exactly one file', () => {
  it('rejects agent with two files', () => {
    const result = validateAddonManifest(makeValidAgentManifest({ files: ['a.md', 'b.md'] }));
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.toLowerCase().includes('one'))).toBe(true);
  });

  it('accepts agent with exactly one file', () => {
    const result = validateAddonManifest(makeValidAgentManifest({ files: ['my-agent.md'] }));
    expect(result.valid).toBe(true);
  });

  it('warns (not errors) when the single agent file does not end in .md', () => {
    const result = validateAddonManifest(makeValidAgentManifest({ files: ['my-agent.ts'] }));
    expect(result.valid).toBe(true); // warning, not error
    expect(result.warnings.some((w) => w.toLowerCase().includes('.md') || w.toLowerCase().includes('markdown'))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// validateAddonManifest — skill: warns if no SKILL.md
// ---------------------------------------------------------------------------

describe('validateAddonManifest — skill warns if no SKILL.md', () => {
  it('warns when no SKILL.md is present among files', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['scripts/run.sh'] }));
    expect(result.valid).toBe(true); // warning, not error
    expect(result.warnings.some((w) => w.toUpperCase().includes('SKILL.MD'))).toBe(true);
  });

  it('does not warn when SKILL.md is present', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['SKILL.md', 'scripts/run.sh'] }));
    expect(result.valid).toBe(true);
    expect(result.warnings.some((w) => w.toUpperCase().includes('SKILL.MD'))).toBe(false);
  });

  it('is case-sensitive for SKILL.md (skill.md does not satisfy)', () => {
    const result = validateAddonManifest(makeValidSkillManifest({ files: ['skill.md'] }));
    expect(result.warnings.some((w) => w.toUpperCase().includes('SKILL.MD'))).toBe(true);
  });
});
