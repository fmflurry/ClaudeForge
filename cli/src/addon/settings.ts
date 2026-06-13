/**
 * settings.ts — Hook registration in Claude Code's settings.json.
 *
 * Decision 6 (design.md): idempotent merge / unmerge of hook entries,
 * atomic read-modify-write, preservation of unrelated settings.
 *
 * All transforms are pure and immutable (spread, no mutation).
 * No `any` — open index typed as `unknown` and narrowed where needed.
 */

import * as nodeFsPromises from 'node:fs/promises';
import type { HookRegistration } from './manifest.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface HookCommand {
  type: string;
  command: string;
}

export interface HookMatcherGroup {
  matcher: string;
  hooks: HookCommand[];
}

/**
 * Structural model for Claude Code's settings.json.
 *
 * The index signature uses `unknown` (not `any`) for the open set of
 * unrecognised top-level keys.  Callers must narrow before using those values.
 */
export type SettingsJson = {
  hooks?: Record<string, HookMatcherGroup[]>;
} & Record<string, unknown>;

/**
 * Port abstraction for settings.json file I/O.
 * Real implementation at the bottom of this file; tests inject vi.fn() fakes.
 */
export interface SettingsFsPort {
  readFile(path: string): Promise<string>;
  writeFile(path: string, content: string): Promise<void>;
  rename(src: string, dest: string): Promise<void>;
  exists(path: string): Promise<boolean>;
  rm(path: string, opts?: { force: boolean }): Promise<void>;
}

// ---------------------------------------------------------------------------
// Real FS implementation
// ---------------------------------------------------------------------------

export const realSettingsFsPort: SettingsFsPort = {
  async readFile(filePath) {
    return nodeFsPromises.readFile(filePath, 'utf-8');
  },

  async writeFile(filePath, content) {
    await nodeFsPromises.writeFile(filePath, content, 'utf-8');
  },

  async rename(src, dest) {
    await nodeFsPromises.rename(src, dest);
  },

  async exists(filePath) {
    try {
      await nodeFsPromises.stat(filePath);
      return true;
    } catch {
      return false;
    }
  },

  async rm(filePath, opts) {
    await nodeFsPromises.rm(filePath, { recursive: false, force: opts?.force ?? false });
  },
};

// ---------------------------------------------------------------------------
// Pure transforms
// ---------------------------------------------------------------------------

/**
 * Merge a hook registration entry into settings.json in a purely immutable way.
 *
 * - Creates the `hooks[event]` array if absent.
 * - Finds the matcher group whose `matcher === reg.matcher` (creates if absent).
 * - Adds `{ type, command }` to the group's `hooks` array ONLY when no identical
 *   entry already exists (idempotent dedupe by type+command).
 * - All unrelated top-level keys, events, and matchers are carried through unchanged.
 */
export function mergeHookEntry(settings: SettingsJson, reg: HookRegistration): SettingsJson {
  const resolvedType = reg.type ?? 'command';
  const newCommand: HookCommand = { type: resolvedType, command: reg.command };

  // Shallow-clone the existing hooks object (or start fresh).
  const existingHooks: Record<string, HookMatcherGroup[]> = { ...(settings.hooks ?? {}) };

  // Clone the array for the target event (or start fresh).
  const eventGroups: HookMatcherGroup[] = [...(existingHooks[reg.event] ?? [])];

  // Find the index of the existing matcher group, if any.
  const matcherIdx = eventGroups.findIndex((g) => g.matcher === reg.matcher);

  let updatedGroups: HookMatcherGroup[];
  if (matcherIdx === -1) {
    // No group for this matcher yet — create a new one.
    updatedGroups = [...eventGroups, { matcher: reg.matcher, hooks: [newCommand] }];
  } else {
    const existingGroup = eventGroups[matcherIdx];

    // Check whether an identical {type, command} is already present.
    const alreadyPresent = existingGroup.hooks.some(
      (h) => h.type === newCommand.type && h.command === newCommand.command,
    );

    if (alreadyPresent) {
      // Idempotent — nothing to add.
      return settings;
    }

    // Clone the group with the new command appended.
    const updatedGroup: HookMatcherGroup = {
      ...existingGroup,
      hooks: [...existingGroup.hooks, newCommand],
    };

    updatedGroups = [
      ...eventGroups.slice(0, matcherIdx),
      updatedGroup,
      ...eventGroups.slice(matcherIdx + 1),
    ];
  }

  const updatedHooks: Record<string, HookMatcherGroup[]> = {
    ...existingHooks,
    [reg.event]: updatedGroups,
  };

  // Spread the full settings to preserve every unrelated top-level key.
  return { ...settings, hooks: updatedHooks };
}

/**
 * Remove a hook registration entry from settings.json immutably.
 *
 * - Removes the matching `{ type, command }` entry from its matcher group.
 * - If the matcher group's `hooks` array becomes empty, drops the group.
 * - If the event array becomes empty, drops the event key from `hooks`.
 * - If `hooks` itself becomes empty, drops the `hooks` key entirely.
 * - Never touches unrelated events, matchers, or top-level keys.
 */
export function removeHookEntry(settings: SettingsJson, reg: HookRegistration): SettingsJson {
  const resolvedType = reg.type ?? 'command';

  const existingHooks = settings.hooks;
  if (!existingHooks) {
    return settings;
  }

  const eventGroups = existingHooks[reg.event];
  if (!eventGroups || eventGroups.length === 0) {
    return settings;
  }

  const matcherIdx = eventGroups.findIndex((g) => g.matcher === reg.matcher);
  if (matcherIdx === -1) {
    return settings;
  }

  const existingGroup = eventGroups[matcherIdx];
  const filteredHooks = existingGroup.hooks.filter(
    (h) => !(h.type === resolvedType && h.command === reg.command),
  );

  // Build the updated groups array, dropping the group if now empty.
  let updatedGroups: HookMatcherGroup[];
  if (filteredHooks.length === 0) {
    updatedGroups = [
      ...eventGroups.slice(0, matcherIdx),
      ...eventGroups.slice(matcherIdx + 1),
    ];
  } else {
    const updatedGroup: HookMatcherGroup = { ...existingGroup, hooks: filteredHooks };
    updatedGroups = [
      ...eventGroups.slice(0, matcherIdx),
      updatedGroup,
      ...eventGroups.slice(matcherIdx + 1),
    ];
  }

  // Build the updated hooks record, dropping the event key if now empty.
  let updatedHooks: Record<string, HookMatcherGroup[]> | undefined;
  if (updatedGroups.length === 0) {
    // Drop this event key; keep everything else.
    const { [reg.event]: _dropped, ...rest } = existingHooks;
    void _dropped; // silence noUnusedLocals
    updatedHooks = Object.keys(rest).length > 0 ? rest : undefined;
  } else {
    updatedHooks = { ...existingHooks, [reg.event]: updatedGroups };
  }

  // Spread everything else; include/exclude `hooks` as computed.
  if (updatedHooks === undefined) {
    // Drop the hooks key entirely.
    const { hooks: _hooks, ...rest } = settings;
    void _hooks;
    return rest as SettingsJson;
  }

  return { ...settings, hooks: updatedHooks };
}

// ---------------------------------------------------------------------------
// I/O helpers (use the port — real FS or injected fake)
// ---------------------------------------------------------------------------

/**
 * Read and parse settings.json.
 *
 * - Missing file or empty content → returns `{}`.
 * - Malformed JSON → throws a clear `Error` so callers can abort rather than
 *   clobber an existing file with bad content.
 */
export async function readSettings(
  filePath: string,
  port: SettingsFsPort,
): Promise<SettingsJson> {
  const fileExists = await port.exists(filePath);
  if (!fileExists) {
    return {};
  }

  const raw = await port.readFile(filePath);
  if (!raw || raw.trim().length === 0) {
    return {};
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw) as unknown;
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    throw new Error(
      `settings.json at "${filePath}" contains malformed JSON and cannot be safely modified: ${msg}`,
    );
  }

  // The file must be a plain object — arrays, strings, nulls are invalid.
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error(
      `settings.json at "${filePath}" does not contain a JSON object. ` +
        'Refusing to overwrite.',
    );
  }

  return parsed as SettingsJson;
}

/**
 * Atomically write settings.json:
 * serialize with 2-space indent → write to `<path>.tmp-<suffix>` → rename over target.
 *
 * The temp file and target live in the same directory (same filesystem),
 * so the rename is atomic on POSIX.
 */
export async function writeSettingsAtomic(
  filePath: string,
  settings: SettingsJson,
  port: SettingsFsPort,
): Promise<void> {
  const content = JSON.stringify(settings, null, 2);
  // Use a monotonic counter suffix via Date.now() for uniqueness.
  // This matches the convention used by the rest of the CLI (no Math.random needed).
  const tmpPath = `${filePath}.tmp-${Date.now().toString(36)}`;

  await port.writeFile(tmpPath, content);
  await port.rename(tmpPath, filePath);
}
