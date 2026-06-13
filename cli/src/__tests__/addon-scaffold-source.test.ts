/**
 * Tests for src/addon/scaffold-source.ts
 *
 * Each of the four AddonType values ('agent', 'skill', 'hook', 'plugin') must:
 *   - produce a synthesized addon.json that passes validateAddonManifest
 *   - include files matching the canonical per-type shape
 *   - delegate to generatePluginTemplate (no duplicated template bodies)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { validateAddonManifest } from '../addon/manifest.js';
import { buildAddonScaffold, mapAddonTypeToPluginType } from '../addon/scaffold-source.js';
import type { AddonManifest } from '../addon/manifest.js';
import * as generatorModule from '@claudeforge/plugin-template';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function parseAddonJson(addonJson: string): AddonManifest {
  return JSON.parse(addonJson) as AddonManifest;
}

// ---------------------------------------------------------------------------
// Spy setup: assert generatePluginTemplate is the source (no template duplication)
// ---------------------------------------------------------------------------

const generateSpy = vi.spyOn(generatorModule, 'generatePluginTemplate');

beforeEach(() => {
  generateSpy.mockClear();
});

// ---------------------------------------------------------------------------
// Type: agent
// ---------------------------------------------------------------------------

describe('buildAddonScaffold — agent', () => {
  it('calls generatePluginTemplate so template bodies are not duplicated', () => {
    generateSpy.mockReturnValueOnce({
      'plugin.json': '{}',
      'README.md': '# my-agent\n',
      'src/index.ts': '// ts\n',
    });

    buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });

    expect(generateSpy).toHaveBeenCalledOnce();
    expect(generateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'my-agent', language: 'typescript' }),
    );
  });

  it('produces a valid addon.json (passes validateAddonManifest)', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    const validation = validateAddonManifest(parsed);
    expect(validation.errors).toEqual([]);
    expect(validation.valid).toBe(true);
  });

  it('addon.json type is agent', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.type).toBe('agent');
  });

  it('addon.json supportedScopes includes both local and global', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.supportedScopes).toContain('local');
    expect(parsed.supportedScopes).toContain('global');
  });

  it('files map contains the canonical <name>.md file', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    expect(result.files).toHaveProperty('my-agent.md');
  });

  it('addon.json files array references only the single .md file', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.files).toHaveLength(1);
    expect(parsed.files[0]).toBe('my-agent.md');
    expect(parsed.files[0]).toMatch(/\.md$/);
  });

  it('files map does NOT contain marketplace plugin.json (replaced by addon.json)', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    expect(result.files).not.toHaveProperty('plugin.json');
  });

  it('files map includes addon.json itself', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    expect(result.files).toHaveProperty('addon.json');
    expect(result.files['addon.json']).toBe(result.addonJson);
  });

  it('addon.json does not include a hook field', () => {
    const result = buildAddonScaffold({ name: 'my-agent', type: 'agent', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.hook).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Type: skill
// ---------------------------------------------------------------------------

describe('buildAddonScaffold — skill', () => {
  it('calls generatePluginTemplate so template bodies are not duplicated', () => {
    generateSpy.mockReturnValueOnce({
      'plugin.json': '{}',
      'README.md': '# skill\n',
      'src/index.ts': '// ts\n',
    });

    buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });

    expect(generateSpy).toHaveBeenCalledOnce();
  });

  it('produces a valid addon.json (passes validateAddonManifest)', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    const validation = validateAddonManifest(parseAddonJson(result.addonJson));
    expect(validation.errors).toEqual([]);
    expect(validation.valid).toBe(true);
  });

  it('addon.json type is skill', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    expect(parseAddonJson(result.addonJson).type).toBe('skill');
  });

  it('addon.json supportedScopes includes both local and global', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.supportedScopes).toContain('local');
    expect(parsed.supportedScopes).toContain('global');
  });

  it('files map contains SKILL.md', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    expect(result.files).toHaveProperty('SKILL.md');
  });

  it('addon.json files array contains SKILL.md', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.files).toContain('SKILL.md');
  });

  it('files map does NOT contain marketplace plugin.json', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    expect(result.files).not.toHaveProperty('plugin.json');
  });

  it('files map includes addon.json', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    expect(result.files).toHaveProperty('addon.json');
  });

  it('addon.json does not include a hook field', () => {
    const result = buildAddonScaffold({ name: 'my-skill', type: 'skill', language: 'typescript' });
    expect(parseAddonJson(result.addonJson).hook).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Type: hook
// ---------------------------------------------------------------------------

describe('buildAddonScaffold — hook', () => {
  it('calls generatePluginTemplate so template bodies are not duplicated', () => {
    generateSpy.mockReturnValueOnce({
      'plugin.json': '{}',
      'src/index.ts': '// ts\n',
    });

    buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });

    expect(generateSpy).toHaveBeenCalledOnce();
  });

  it('produces a valid addon.json (passes validateAddonManifest)', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    const validation = validateAddonManifest(parseAddonJson(result.addonJson));
    expect(validation.errors).toEqual([]);
    expect(validation.valid).toBe(true);
  });

  it('addon.json type is hook', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    expect(parseAddonJson(result.addonJson).type).toBe('hook');
  });

  it('addon.json supportedScopes includes both local and global', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.supportedScopes).toContain('local');
    expect(parsed.supportedScopes).toContain('global');
  });

  it('files map contains the canonical hooks/<name>.ts script', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    expect(result.files).toHaveProperty('hooks/my-hook.ts');
  });

  it('addon.json files array references hooks/<name>.ts', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.files).toContain('hooks/my-hook.ts');
  });

  it('addon.json includes a hook stub with event, matcher, command', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.hook).toBeDefined();
    expect(parsed.hook?.event).toBeTruthy();
    expect(parsed.hook?.matcher).toBeTruthy();
    expect(parsed.hook?.command).toBeTruthy();
  });

  it('hook.command references one of the declared files entries', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.files).toContain(parsed.hook?.command);
  });

  it('files map does NOT contain marketplace plugin.json', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    expect(result.files).not.toHaveProperty('plugin.json');
  });

  it('files map includes addon.json', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'typescript' });
    expect(result.files).toHaveProperty('addon.json');
  });

  it('hook script path uses .sh extension for non-typescript languages', () => {
    const result = buildAddonScaffold({ name: 'my-hook', type: 'hook', language: 'python' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.files.some((f) => f.startsWith('hooks/'))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Type: plugin
// ---------------------------------------------------------------------------

describe('buildAddonScaffold — plugin', () => {
  it('calls generatePluginTemplate so template bodies are not duplicated', () => {
    generateSpy.mockReturnValueOnce({
      'plugin.json': '{"name":"my-plugin"}',
      'src/index.ts': '// ts\n',
    });

    buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });

    expect(generateSpy).toHaveBeenCalledOnce();
  });

  it('produces a valid addon.json (passes validateAddonManifest)', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    const validation = validateAddonManifest(parseAddonJson(result.addonJson));
    expect(validation.errors).toEqual([]);
    expect(validation.valid).toBe(true);
  });

  it('addon.json type is plugin', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    expect(parseAddonJson(result.addonJson).type).toBe('plugin');
  });

  it('plugin defaults supportedScopes to ["global"] only (not local)', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.supportedScopes).toEqual(['global']);
    expect(parsed.supportedScopes).not.toContain('local');
  });

  it('files map contains .claude-plugin/plugin.json', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    expect(result.files).toHaveProperty('.claude-plugin/plugin.json');
  });

  it('addon.json files array contains .claude-plugin/plugin.json', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    const parsed = parseAddonJson(result.addonJson);
    expect(parsed.files).toContain('.claude-plugin/plugin.json');
  });

  it('files map does NOT contain top-level plugin.json (marketplace key removed)', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    expect(result.files).not.toHaveProperty('plugin.json');
  });

  it('files map includes addon.json', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    expect(result.files).toHaveProperty('addon.json');
  });

  it('addon.json does not include a hook field', () => {
    const result = buildAddonScaffold({ name: 'my-plugin', type: 'plugin', language: 'typescript' });
    expect(parseAddonJson(result.addonJson).hook).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// mapAddonTypeToPluginType helper (exported for reuse)
// ---------------------------------------------------------------------------

describe('mapAddonTypeToPluginType', () => {
  it('maps agent to agent', () => {
    expect(mapAddonTypeToPluginType('agent')).toBe('agent');
  });

  it('maps skill to skill', () => {
    expect(mapAddonTypeToPluginType('skill')).toBe('skill');
  });

  it('maps hook to hook', () => {
    expect(mapAddonTypeToPluginType('hook')).toBe('hook');
  });

  it('maps plugin to plugin', () => {
    expect(mapAddonTypeToPluginType('plugin')).toBe('plugin');
  });
});
