/**
 * Tests for src/addon/placement.ts
 *
 * Production module path: src/addon/placement.ts
 * Exported:
 *   - PlacedFile: { readonly fromRel: string; readonly toAbs: string }
 *   - Placement: { readonly type, readonly liveTargets, readonly ownerPath, readonly sidecarPath, readonly settingsEntry? }
 *   - resolvePlacement(manifest: AddonManifest, deps: { scopeRoot: string; homeDir: string }): Placement
 *
 * Per-type mapping (from design.md Decision 2):
 *   agent  → ownerPath = <scopeRoot>/agents/<name>.md (FILE, NOT dir)
 *            liveTargets[0] = { fromRel: files[0], toAbs: <scopeRoot>/agents/<name>.md }
 *   skill  → ownerPath = <scopeRoot>/skills/<name>/ (DIR)
 *            liveTargets[i] = { fromRel: files[i], toAbs: <scopeRoot>/skills/<name>/<files[i]> }
 *   hook   → ownerPath = <scopeRoot>/hooks/ (shared dir, keyed by add-on name via sidecar)
 *            liveTargets[i] = { fromRel: files[i], toAbs: <scopeRoot>/hooks/<files[i]> }
 *            settingsEntry = manifest.hook (required)
 *   plugin → ownerPath = <homeDir>/.claude/plugins/<name>/ (GLOBAL DIR, never scopeRoot)
 *            liveTargets[i] = { fromRel: files[i], toAbs: <homeDir>/.claude/plugins/<name>/<files[i]> }
 *
 *   sidecarPath (ALL types) = <scopeRoot>/.addons/<type>/<name>.json
 */

import { describe, it, expect } from 'vitest';
import * as path from 'node:path';

import { resolvePlacement } from '../addon/placement.js';
import type { Placement, PlacedFile } from '../addon/placement.js';
import type { AddonManifest } from '../addon/manifest.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

// Use a LOCAL scope root that is clearly distinct from the global home .claude dir.
// This lets us verify plugin paths resolve to homeDir (not scopeRoot) without ambiguity.
const SCOPE_ROOT = '/projects/my-project/.claude';
const HOME_DIR = '/home/alice';

function makeAgentManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'reviewer',
    version: '1.0.0',
    type: 'agent',
    supportedScopes: ['local', 'global'],
    files: ['reviewer.md'],
    ...overrides,
  };
}

function makeSkillManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'lint',
    version: '1.0.0',
    type: 'skill',
    supportedScopes: ['local', 'global'],
    files: ['SKILL.md', 'scripts/run.sh', 'utils/helpers.ts'],
    ...overrides,
  };
}

function makeHookManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'auth-check',
    version: '1.0.0',
    type: 'hook',
    supportedScopes: ['local', 'global'],
    files: ['hooks/auth-check.sh'],
    hook: {
      event: 'PreToolUse',
      matcher: 'Bash',
      command: 'hooks/auth-check.sh',
    },
    ...overrides,
  };
}

function makePluginManifest(overrides?: Partial<AddonManifest>): AddonManifest {
  return {
    name: 'pack',
    version: '1.0.0',
    type: 'plugin',
    supportedScopes: ['global'],
    files: ['bundle/index.js', '.claude-plugin/plugin.json'],
    ...overrides,
  };
}

function callResolvePlacement(manifest: AddonManifest): Placement {
  return resolvePlacement(manifest, { scopeRoot: SCOPE_ROOT, homeDir: HOME_DIR });
}

// ---------------------------------------------------------------------------
// Agent type
// ---------------------------------------------------------------------------

describe('resolvePlacement — agent', () => {
  it('ownerPath is a .md file path (not a directory)', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.ownerPath).toBe(`${SCOPE_ROOT}/agents/reviewer.md`);
  });

  it('ownerPath ends in <name>.md', () => {
    const p = callResolvePlacement(makeAgentManifest({ name: 'code-helper' }));
    expect(p.ownerPath).toMatch(/agents\/code-helper\.md$/);
  });

  it('ownerPath does NOT end with a slash (not a directory)', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.ownerPath).not.toMatch(/\/$/);
  });

  it('liveTargets has exactly one entry', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.liveTargets).toHaveLength(1);
  });

  it('liveTargets[0].fromRel is the original files[0]', () => {
    const p = callResolvePlacement(makeAgentManifest({ files: ['reviewer.md'] }));
    expect(p.liveTargets[0].fromRel).toBe('reviewer.md');
  });

  it('liveTargets[0].toAbs equals ownerPath', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.liveTargets[0].toAbs).toBe(p.ownerPath);
  });

  it('toAbs is an absolute path', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(path.isAbsolute(p.liveTargets[0].toAbs)).toBe(true);
  });

  it('no settingsEntry for agent', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.settingsEntry).toBeUndefined();
  });

  it('sidecarPath = <scopeRoot>/.addons/agent/<name>.json', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.sidecarPath).toBe(`${SCOPE_ROOT}/.addons/agent/reviewer.json`);
  });

  it('type field is "agent"', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.type).toBe('agent');
  });
});

// ---------------------------------------------------------------------------
// Skill type
// ---------------------------------------------------------------------------

describe('resolvePlacement — skill', () => {
  it('ownerPath is skills/<name>/ (a directory, ends with slash)', () => {
    const p = callResolvePlacement(makeSkillManifest());
    expect(p.ownerPath).toBe(`${SCOPE_ROOT}/skills/lint/`);
  });

  it('ownerPath contains the skill name directory', () => {
    const p = callResolvePlacement(makeSkillManifest({ name: 'my-skill' }));
    expect(p.ownerPath).toContain('/skills/my-skill/');
  });

  it('liveTargets count matches files count', () => {
    const manifest = makeSkillManifest();
    const p = callResolvePlacement(manifest);
    expect(p.liveTargets).toHaveLength(manifest.files.length);
  });

  it('each file preserves its relative path under skills/<name>/', () => {
    const p = callResolvePlacement(makeSkillManifest());
    const targets = p.liveTargets as PlacedFile[];
    expect(targets[0].toAbs).toBe(`${SCOPE_ROOT}/skills/lint/SKILL.md`);
    expect(targets[1].toAbs).toBe(`${SCOPE_ROOT}/skills/lint/scripts/run.sh`);
    expect(targets[2].toAbs).toBe(`${SCOPE_ROOT}/skills/lint/utils/helpers.ts`);
  });

  it('fromRel matches the original files[] entry', () => {
    const manifest = makeSkillManifest();
    const p = callResolvePlacement(manifest);
    const targets = p.liveTargets as PlacedFile[];
    for (let i = 0; i < manifest.files.length; i++) {
      expect(targets[i].fromRel).toBe(manifest.files[i]);
    }
  });

  it('all toAbs paths are absolute', () => {
    const p = callResolvePlacement(makeSkillManifest());
    for (const t of p.liveTargets) {
      expect(path.isAbsolute(t.toAbs)).toBe(true);
    }
  });

  it('no settingsEntry for skill', () => {
    const p = callResolvePlacement(makeSkillManifest());
    expect(p.settingsEntry).toBeUndefined();
  });

  it('sidecarPath = <scopeRoot>/.addons/skill/<name>.json', () => {
    const p = callResolvePlacement(makeSkillManifest());
    expect(p.sidecarPath).toBe(`${SCOPE_ROOT}/.addons/skill/lint.json`);
  });

  it('type field is "skill"', () => {
    const p = callResolvePlacement(makeSkillManifest());
    expect(p.type).toBe('skill');
  });
});

// ---------------------------------------------------------------------------
// Hook type
// ---------------------------------------------------------------------------

describe('resolvePlacement — hook', () => {
  it('each file maps to <scopeRoot>/hooks/<file>', () => {
    const p = callResolvePlacement(makeHookManifest());
    expect(p.liveTargets[0].toAbs).toBe(`${SCOPE_ROOT}/hooks/hooks/auth-check.sh`);
  });

  it('fromRel preserves the original files entry', () => {
    const p = callResolvePlacement(makeHookManifest());
    expect(p.liveTargets[0].fromRel).toBe('hooks/auth-check.sh');
  });

  it('liveTargets count matches files count', () => {
    const manifest = makeHookManifest({ files: ['hooks/a.sh', 'hooks/b.sh'],
      hook: { event: 'PreToolUse', matcher: 'Bash', command: 'hooks/a.sh' } });
    const p = callResolvePlacement(manifest);
    expect(p.liveTargets).toHaveLength(2);
  });

  it('settingsEntry equals manifest.hook', () => {
    const manifest = makeHookManifest();
    const p = callResolvePlacement(manifest);
    expect(p.settingsEntry).toEqual(manifest.hook);
  });

  it('settingsEntry has event, matcher, command', () => {
    const p = callResolvePlacement(makeHookManifest());
    expect(p.settingsEntry?.event).toBe('PreToolUse');
    expect(p.settingsEntry?.matcher).toBe('Bash');
    expect(p.settingsEntry?.command).toBe('hooks/auth-check.sh');
  });

  it('ownerPath references hooks directory within scopeRoot', () => {
    const p = callResolvePlacement(makeHookManifest());
    expect(p.ownerPath).toContain(SCOPE_ROOT);
    expect(p.ownerPath).toContain('hooks');
  });

  it('all toAbs paths are absolute', () => {
    const p = callResolvePlacement(makeHookManifest());
    for (const t of p.liveTargets) {
      expect(path.isAbsolute(t.toAbs)).toBe(true);
    }
  });

  it('sidecarPath = <scopeRoot>/.addons/hook/<name>.json', () => {
    const p = callResolvePlacement(makeHookManifest());
    expect(p.sidecarPath).toBe(`${SCOPE_ROOT}/.addons/hook/auth-check.json`);
  });

  it('type field is "hook"', () => {
    const p = callResolvePlacement(makeHookManifest());
    expect(p.type).toBe('hook');
  });
});

// ---------------------------------------------------------------------------
// Plugin type — GLOBAL ONLY
// ---------------------------------------------------------------------------

describe('resolvePlacement — plugin (global only)', () => {
  it('ownerPath is under homeDir/.claude/plugins/<name>/, NOT scopeRoot', () => {
    const p = callResolvePlacement(makePluginManifest());
    expect(p.ownerPath).toBe(`${HOME_DIR}/.claude/plugins/pack/`);
    expect(p.ownerPath).not.toContain(SCOPE_ROOT);
  });

  it('each file maps under homeDir/.claude/plugins/<name>/', () => {
    const p = callResolvePlacement(makePluginManifest());
    expect(p.liveTargets[0].toAbs).toBe(`${HOME_DIR}/.claude/plugins/pack/bundle/index.js`);
    expect(p.liveTargets[1].toAbs).toBe(`${HOME_DIR}/.claude/plugins/pack/.claude-plugin/plugin.json`);
  });

  it('liveTargets do NOT reference scopeRoot', () => {
    const p = callResolvePlacement(makePluginManifest());
    for (const t of p.liveTargets) {
      expect(t.toAbs).not.toContain(SCOPE_ROOT);
    }
  });

  it('plugin resolves under homeDir even when scopeRoot is local scope', () => {
    const localScopeRoot = '/projects/my-project/.claude';
    const p = resolvePlacement(makePluginManifest(), {
      scopeRoot: localScopeRoot,
      homeDir: HOME_DIR,
    });
    expect(p.ownerPath).toBe(`${HOME_DIR}/.claude/plugins/pack/`);
    expect(p.liveTargets[0].toAbs).toContain(HOME_DIR);
    expect(p.liveTargets[0].toAbs).not.toContain(localScopeRoot);
  });

  it('fromRel preserves original files entries', () => {
    const manifest = makePluginManifest();
    const p = callResolvePlacement(manifest);
    const targets = p.liveTargets as PlacedFile[];
    for (let i = 0; i < manifest.files.length; i++) {
      expect(targets[i].fromRel).toBe(manifest.files[i]);
    }
  });

  it('all toAbs paths are absolute', () => {
    const p = callResolvePlacement(makePluginManifest());
    for (const t of p.liveTargets) {
      expect(path.isAbsolute(t.toAbs)).toBe(true);
    }
  });

  it('no settingsEntry for plugin', () => {
    const p = callResolvePlacement(makePluginManifest());
    expect(p.settingsEntry).toBeUndefined();
  });

  it('sidecarPath = <scopeRoot>/.addons/plugin/<name>.json (sidecar always in scopeRoot)', () => {
    const p = callResolvePlacement(makePluginManifest());
    expect(p.sidecarPath).toBe(`${SCOPE_ROOT}/.addons/plugin/pack.json`);
  });

  it('type field is "plugin"', () => {
    const p = callResolvePlacement(makePluginManifest());
    expect(p.type).toBe('plugin');
  });
});

// ---------------------------------------------------------------------------
// Path containment / traversal safety
// ---------------------------------------------------------------------------

describe('resolvePlacement — path containment safety', () => {
  it('agent toAbs is inside agents/ under scopeRoot', () => {
    const p = callResolvePlacement(makeAgentManifest());
    expect(p.liveTargets[0].toAbs.startsWith(SCOPE_ROOT)).toBe(true);
  });

  it('skill toAbs paths are inside skills/<name>/ under scopeRoot', () => {
    const p = callResolvePlacement(makeSkillManifest());
    for (const t of p.liveTargets) {
      expect(t.toAbs.startsWith(`${SCOPE_ROOT}/skills/lint/`)).toBe(true);
    }
  });

  it('hook toAbs paths are inside hooks/ under scopeRoot', () => {
    const p = callResolvePlacement(makeHookManifest());
    for (const t of p.liveTargets) {
      expect(t.toAbs.startsWith(`${SCOPE_ROOT}/hooks/`)).toBe(true);
    }
  });

  it('plugin toAbs paths are inside homeDir/.claude/plugins/<name>/', () => {
    const p = callResolvePlacement(makePluginManifest());
    for (const t of p.liveTargets) {
      expect(t.toAbs.startsWith(`${HOME_DIR}/.claude/plugins/pack/`)).toBe(true);
    }
  });

  it('sidecar is always within scopeRoot/.addons/', () => {
    for (const manifest of [
      makeAgentManifest(),
      makeSkillManifest(),
      makeHookManifest(),
      makePluginManifest(),
    ]) {
      const p = callResolvePlacement(manifest);
      expect(p.sidecarPath.startsWith(`${SCOPE_ROOT}/.addons/`)).toBe(true);
    }
  });
});
