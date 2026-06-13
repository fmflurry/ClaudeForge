/**
 * Tests for src/addon/settings.ts
 *
 * Production module path: src/addon/settings.ts
 * Exported:
 *   - SettingsJson: { hooks?: Record<string, HookMatcherGroup[]>; [key: string]: unknown }
 *   - HookMatcherGroup: { matcher: string; hooks: HookCommand[] }
 *   - HookCommand: { type: string; command: string }
 *   - SettingsFsPort: { readFile; writeFile; rename; exists; rm }
 *   - mergeHookEntry(settings, reg): SettingsJson  — pure, immutable, idempotent
 *   - removeHookEntry(settings, reg): SettingsJson  — pure, immutable
 *   - readSettings(path, port): Promise<SettingsJson>
 *   - writeSettingsAtomic(path, settings, port): Promise<void>
 */

import { describe, it, expect, vi } from 'vitest';
import type { Mock } from 'vitest';

import {
  mergeHookEntry,
  removeHookEntry,
  readSettings,
  writeSettingsAtomic,
} from '../addon/settings.js';
import type { SettingsJson, SettingsFsPort } from '../addon/settings.js';
import type { HookRegistration } from '../addon/manifest.js';

// ---------------------------------------------------------------------------
// Helpers / fixtures
// ---------------------------------------------------------------------------

function makeReg(overrides?: Partial<HookRegistration>): HookRegistration {
  return {
    event: 'PreToolUse',
    matcher: 'Bash',
    command: 'hooks/auth.sh',
    ...overrides,
  };
}

/** Build a minimal SettingsFsPort fake using vi.fn(). */
function makeFakePort(overrides?: Partial<Record<keyof SettingsFsPort, Mock>>): SettingsFsPort {
  return {
    readFile: vi.fn(),
    writeFile: vi.fn().mockResolvedValue(undefined),
    rename: vi.fn().mockResolvedValue(undefined),
    exists: vi.fn().mockResolvedValue(true),
    rm: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// mergeHookEntry — pure transform
// ---------------------------------------------------------------------------

describe('mergeHookEntry', () => {
  it('creates the event key, matcher group, and hook command on empty settings', () => {
    const reg = makeReg();
    const result = mergeHookEntry({}, reg);

    expect(result.hooks).toBeDefined();
    const groups = result.hooks?.['PreToolUse'];
    expect(groups).toHaveLength(1);
    expect(groups?.[0]?.matcher).toBe('Bash');
    expect(groups?.[0]?.hooks).toHaveLength(1);
    expect(groups?.[0]?.hooks[0]).toEqual({ type: 'command', command: 'hooks/auth.sh' });
  });

  it('defaults type to "command" when reg.type is absent', () => {
    const reg: HookRegistration = { event: 'PreToolUse', matcher: 'Bash', command: 'hooks/auth.sh' };
    const result = mergeHookEntry({}, reg);
    expect(result.hooks?.['PreToolUse']?.[0]?.hooks?.[0]?.type).toBe('command');
  });

  it('respects an explicit reg.type', () => {
    const reg = makeReg({ type: 'script' });
    const result = mergeHookEntry({}, reg);
    expect(result.hooks?.['PreToolUse']?.[0]?.hooks?.[0]?.type).toBe('script');
  });

  it('is idempotent — re-adding the same hook does not create a duplicate entry', () => {
    const reg = makeReg();
    const once = mergeHookEntry({}, reg);
    const twice = mergeHookEntry(once, reg);

    const hooks = twice.hooks?.['PreToolUse']?.[0]?.hooks;
    expect(hooks).toHaveLength(1);
  });

  it('adds a second hook to the same matcher group when type+command differs', () => {
    const reg1 = makeReg({ command: 'hooks/auth.sh' });
    const reg2 = makeReg({ command: 'hooks/log.sh' });
    const after1 = mergeHookEntry({}, reg1);
    const after2 = mergeHookEntry(after1, reg2);

    const hooks = after2.hooks?.['PreToolUse']?.[0]?.hooks;
    expect(hooks).toHaveLength(2);
  });

  it('creates a second matcher group for a different matcher', () => {
    const reg1 = makeReg({ matcher: 'Bash' });
    const reg2 = makeReg({ matcher: 'Read' });
    const after1 = mergeHookEntry({}, reg1);
    const after2 = mergeHookEntry(after1, reg2);

    const groups = after2.hooks?.['PreToolUse'];
    expect(groups).toHaveLength(2);
  });

  it('creates a second event key for a different event', () => {
    const reg1 = makeReg({ event: 'PreToolUse' });
    const reg2 = makeReg({ event: 'PostToolUse' });
    const after1 = mergeHookEntry({}, reg1);
    const after2 = mergeHookEntry(after1, reg2);

    expect(Object.keys(after2.hooks ?? {})).toContain('PreToolUse');
    expect(Object.keys(after2.hooks ?? {})).toContain('PostToolUse');
  });

  it('preserves unrelated top-level keys', () => {
    const initial: SettingsJson = { theme: 'dark', notifications: true };
    const result = mergeHookEntry(initial, makeReg());

    expect(result['theme']).toBe('dark');
    expect(result['notifications']).toBe(true);
  });

  it('preserves unrelated hook events', () => {
    const existing: SettingsJson = {
      hooks: {
        PostToolUse: [
          { matcher: 'Write', hooks: [{ type: 'command', command: 'hooks/post.sh' }] },
        ],
      },
    };
    const result = mergeHookEntry(existing, makeReg({ event: 'PreToolUse' }));

    // The unrelated PostToolUse event must still be present and unchanged.
    const postGroups = result.hooks?.['PostToolUse'];
    expect(postGroups).toHaveLength(1);
    expect(postGroups?.[0]?.matcher).toBe('Write');
  });

  it('preserves unrelated matchers within the same event', () => {
    const existing: SettingsJson = {
      hooks: {
        PreToolUse: [
          { matcher: 'Read', hooks: [{ type: 'command', command: 'hooks/read.sh' }] },
        ],
      },
    };
    const result = mergeHookEntry(existing, makeReg({ matcher: 'Bash' }));

    const groups = result.hooks?.['PreToolUse'];
    expect(groups).toHaveLength(2);
    const readGroup = groups?.find((g) => g.matcher === 'Read');
    expect(readGroup).toBeDefined();
    expect(readGroup?.hooks[0]?.command).toBe('hooks/read.sh');
  });

  it('does not mutate the original settings object', () => {
    const original: SettingsJson = {};
    mergeHookEntry(original, makeReg());
    expect(original.hooks).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// removeHookEntry — pure transform
// ---------------------------------------------------------------------------

describe('removeHookEntry', () => {
  function settingsWithEntry(reg: HookRegistration): SettingsJson {
    return mergeHookEntry({}, reg);
  }

  it('removes the matching {type, command} entry', () => {
    const reg = makeReg();
    const settings = settingsWithEntry(reg);
    const result = removeHookEntry(settings, reg);

    const hooks = result.hooks?.['PreToolUse']?.[0]?.hooks;
    expect(hooks).toBeUndefined();
  });

  it('drops the matcher group when its hooks array becomes empty', () => {
    const reg = makeReg();
    const settings = settingsWithEntry(reg);
    const result = removeHookEntry(settings, reg);

    const groups = result.hooks?.['PreToolUse'];
    // The group should no longer exist (length 0 or array removed entirely).
    expect(groups ?? []).toHaveLength(0);
  });

  it('drops the event key when its array becomes empty', () => {
    const reg = makeReg();
    // Add two events so that removing one still leaves a hooks object.
    const settings = mergeHookEntry(
      settingsWithEntry(reg),
      makeReg({ event: 'PostToolUse', matcher: 'Write', command: 'hooks/post.sh' }),
    );
    const result = removeHookEntry(settings, reg);

    // The hooks object should still exist (PostToolUse is there).
    expect(result.hooks).toBeDefined();
    // But PreToolUse must be gone.
    expect('PreToolUse' in (result.hooks ?? {})).toBe(false);
  });

  it('drops the hooks key entirely when all events are removed', () => {
    const reg = makeReg();
    const settings = settingsWithEntry(reg);
    const result = removeHookEntry(settings, reg);

    // hooks should be absent or empty object — the key should not exist at all
    expect(result.hooks).toBeUndefined();
  });

  it('leaves unrelated hook entries untouched', () => {
    const reg = makeReg({ event: 'PreToolUse', matcher: 'Bash', command: 'hooks/auth.sh' });
    const regOther = makeReg({ event: 'PostToolUse', matcher: 'Write', command: 'hooks/post.sh' });
    const settings = mergeHookEntry(mergeHookEntry({}, reg), regOther);

    const result = removeHookEntry(settings, reg);

    // The unrelated PostToolUse entry must survive.
    const postGroups = result.hooks?.['PostToolUse'];
    expect(postGroups).toHaveLength(1);
    expect(postGroups?.[0]?.hooks[0]?.command).toBe('hooks/post.sh');
  });

  it('leaves unrelated matchers within the same event untouched', () => {
    const reg1 = makeReg({ matcher: 'Bash', command: 'hooks/auth.sh' });
    const reg2 = makeReg({ matcher: 'Read', command: 'hooks/read.sh' });
    const settings = mergeHookEntry(mergeHookEntry({}, reg1), reg2);

    const result = removeHookEntry(settings, reg1);

    const groups = result.hooks?.['PreToolUse'];
    expect(groups).toHaveLength(1);
    expect(groups?.[0]?.matcher).toBe('Read');
  });

  it('leaves a second hook in the same group untouched', () => {
    const reg1 = makeReg({ command: 'hooks/auth.sh' });
    const reg2 = makeReg({ command: 'hooks/log.sh' });
    const settings = mergeHookEntry(mergeHookEntry({}, reg1), reg2);

    const result = removeHookEntry(settings, reg1);

    const hooks = result.hooks?.['PreToolUse']?.[0]?.hooks;
    expect(hooks).toHaveLength(1);
    expect(hooks?.[0]?.command).toBe('hooks/log.sh');
  });

  it('is a no-op when the entry does not exist', () => {
    const reg = makeReg();
    const result = removeHookEntry({}, reg);
    // Should return a valid SettingsJson without throwing.
    expect(result).toBeDefined();
    expect(result.hooks).toBeUndefined();
  });

  it('preserves unrelated top-level keys', () => {
    const initial: SettingsJson = { theme: 'dark', ...mergeHookEntry({}, makeReg()) };
    const result = removeHookEntry(initial, makeReg());
    expect(result['theme']).toBe('dark');
  });

  it('does not mutate the original settings object', () => {
    const reg = makeReg();
    const settings = settingsWithEntry(reg);
    removeHookEntry(settings, reg);
    // The original should still have the entry.
    expect(settings.hooks?.['PreToolUse']?.[0]?.hooks).toHaveLength(1);
  });
});

// ---------------------------------------------------------------------------
// readSettings
// ---------------------------------------------------------------------------

describe('readSettings', () => {
  it('returns {} when the file does not exist (exists returns false)', async () => {
    const port = makeFakePort({
      exists: vi.fn().mockResolvedValue(false),
    });
    const result = await readSettings('/some/path/settings.json', port);
    expect(result).toEqual({});
  });

  it('returns {} when the file exists but content is empty string', async () => {
    const port = makeFakePort({
      exists: vi.fn().mockResolvedValue(true),
      readFile: vi.fn().mockResolvedValue(''),
    });
    const result = await readSettings('/some/path/settings.json', port);
    expect(result).toEqual({});
  });

  it('returns {} when the file exists but content is only whitespace', async () => {
    const port = makeFakePort({
      exists: vi.fn().mockResolvedValue(true),
      readFile: vi.fn().mockResolvedValue('   \n  '),
    });
    const result = await readSettings('/some/path/settings.json', port);
    expect(result).toEqual({});
  });

  it('parses valid JSON and returns the parsed object', async () => {
    const content = JSON.stringify({ theme: 'dark', hooks: {} });
    const port = makeFakePort({
      exists: vi.fn().mockResolvedValue(true),
      readFile: vi.fn().mockResolvedValue(content),
    });
    const result = await readSettings('/some/path/settings.json', port);
    expect(result).toEqual({ theme: 'dark', hooks: {} });
  });

  it('throws a clear Error when the file contains malformed JSON', async () => {
    const port = makeFakePort({
      exists: vi.fn().mockResolvedValue(true),
      readFile: vi.fn().mockResolvedValue('{ not valid json'),
    });
    await expect(readSettings('/some/path/settings.json', port)).rejects.toThrow(
      /malformed|invalid|JSON/i,
    );
  });

  it('does NOT return {} on malformed JSON — it throws', async () => {
    const port = makeFakePort({
      exists: vi.fn().mockResolvedValue(true),
      readFile: vi.fn().mockResolvedValue('{bad}'),
    });
    await expect(readSettings('/some/path/settings.json', port)).rejects.toThrow();
  });
});

// ---------------------------------------------------------------------------
// writeSettingsAtomic
// ---------------------------------------------------------------------------

describe('writeSettingsAtomic', () => {
  it('writes to a temp file and then renames it to the target path', async () => {
    const calls: string[] = [];
    const port = makeFakePort({
      writeFile: vi.fn().mockImplementation((p: string) => {
        calls.push(`write:${p}`);
        return Promise.resolve();
      }),
      rename: vi.fn().mockImplementation((src: string, dest: string) => {
        calls.push(`rename:${src}->${dest}`);
        return Promise.resolve();
      }),
    });

    const settings: SettingsJson = { theme: 'dark' };
    await writeSettingsAtomic('/scope/.claude/settings.json', settings, port);

    // writeFile must be called before rename.
    const writeIndex = calls.findIndex((c) => c.startsWith('write:'));
    const renameIndex = calls.findIndex((c) => c.startsWith('rename:'));
    expect(writeIndex).toBeGreaterThanOrEqual(0);
    expect(renameIndex).toBeGreaterThan(writeIndex);
  });

  it('writes to a path that is NOT the target (i.e. a temp path)', async () => {
    const targetPath = '/scope/.claude/settings.json';
    let writtenPath = '';
    const port = makeFakePort({
      writeFile: vi.fn().mockImplementation((p: string) => {
        writtenPath = p;
        return Promise.resolve();
      }),
    });

    await writeSettingsAtomic(targetPath, {}, port);

    expect(writtenPath).not.toBe(targetPath);
    expect(writtenPath).toContain(targetPath); // temp lives near the target
  });

  it('renames the temp file to the exact target path', async () => {
    const targetPath = '/scope/.claude/settings.json';
    let renameDest = '';
    const port = makeFakePort({
      rename: vi.fn().mockImplementation((_src: string, dest: string) => {
        renameDest = dest;
        return Promise.resolve();
      }),
    });

    await writeSettingsAtomic(targetPath, {}, port);

    expect(renameDest).toBe(targetPath);
  });

  it('serializes settings with 2-space indentation', async () => {
    let writtenContent = '';
    const port = makeFakePort({
      writeFile: vi.fn().mockImplementation((_p: string, content: string) => {
        writtenContent = content;
        return Promise.resolve();
      }),
    });

    const settings: SettingsJson = { theme: 'light' };
    await writeSettingsAtomic('/scope/.claude/settings.json', settings, port);

    expect(writtenContent).toBe(JSON.stringify(settings, null, 2));
  });

  it('calls writeFile exactly once and rename exactly once', async () => {
    const port = makeFakePort();
    await writeSettingsAtomic('/scope/.claude/settings.json', {}, port);

    expect(port.writeFile).toHaveBeenCalledTimes(1);
    expect(port.rename).toHaveBeenCalledTimes(1);
  });
});
