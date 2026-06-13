/**
 * Add-on manifest module.
 *
 * Defines the canonical add-on manifest format (addon.json) and provides
 * pure, hand-rolled validation mirroring the style of validate.ts.
 *
 * No Zod, no `any` types — all narrowing via `unknown` + type guards.
 */

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type AddonType = 'hook' | 'plugin' | 'skill' | 'agent';

export type AddonScope = 'local' | 'global';

/** The hook registration entry merged into settings.json for hook add-ons. */
export interface HookRegistration {
  event: string;
  matcher: string;
  command: string;
  /** Defaults to 'command' when absent. */
  type?: string;
}

export interface AddonManifest {
  name: string;
  version: string;
  type: AddonType;
  supportedScopes: AddonScope[];
  files: string[];
  hook?: HookRegistration;
}

export interface AddonValidationResult {
  valid: boolean;
  errors: string[];
  warnings: string[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/** Strict semver: MAJOR.MINOR.PATCH with optional pre-release or build metadata. */
const SEMVER_RE = /^\d+\.\d+\.\d+([+-][a-zA-Z0-9._+-]*)?$/;

const VALID_ADDON_TYPES: ReadonlySet<string> = new Set<AddonType>([
  'hook',
  'plugin',
  'skill',
  'agent',
]);

const VALID_SCOPES: ReadonlySet<string> = new Set<AddonScope>(['local', 'global']);

// ---------------------------------------------------------------------------
// Path-traversal guard (mirrors install.ts lines ~144-145)
// ---------------------------------------------------------------------------

/**
 * Returns true when a relative-path file entry is safe — no absolute paths,
 * no `..` segments, no null bytes.  Mirrors the traversal guard in install.ts.
 */
function isSafePath(filePath: string): boolean {
  if (filePath.includes('\0')) return false;
  if (isAbsolutePath(filePath)) return false;
  const hasDotDot = filePath.split(/[\\/]/).some((seg) => seg === '..');
  return !hasDotDot;
}

/** Cross-platform absolute-path check (handles POSIX `/` and Windows `C:\` / `\\`). */
function isAbsolutePath(p: string): boolean {
  if (p.startsWith('/') || p.startsWith('\\')) return true;
  // Windows drive letter: C:\ or C:/
  if (/^[a-zA-Z]:[/\\]/.test(p)) return true;
  // UNC path: \\server\share
  if (p.startsWith('\\\\')) return true;
  return false;
}

/**
 * Returns an error message for an unsafe path, or null if the path is safe.
 */
function unsafePathError(filePath: string): string | null {
  if (filePath.includes('\0')) {
    return `File path "${filePath}" contains unsafe null byte character.`;
  }
  if (isAbsolutePath(filePath)) {
    return `File path "${filePath}" must be relative (absolute paths are not allowed).`;
  }
  const hasDotDot = filePath.split(/[\\/]/).some((seg) => seg === '..');
  if (hasDotDot) {
    return `File path "${filePath}" must not contain ".." segments.`;
  }
  return null;
}

// ---------------------------------------------------------------------------
// normalizeSupportedScopes
// ---------------------------------------------------------------------------

/**
 * Normalizes the raw `supportedScopes` field from a manifest input.
 *
 * Accepts:
 *   - `'both'` — expands to `['local', 'global']`
 *   - `AddonScope[]` — returned as-is when all entries are valid scope values
 *
 * Returns `[]` for:
 *   - empty array (caller surfaces the error)
 *   - any invalid value (non-"both" string, array with invalid entries, null, etc.)
 */
export function normalizeSupportedScopes(input: unknown): AddonScope[] {
  if (input === 'both') {
    return ['local', 'global'];
  }

  if (!Array.isArray(input)) {
    return [];
  }

  if (input.length === 0) {
    return [];
  }

  // Validate every entry is a known scope value.
  const allValid = input.every(
    (entry): entry is AddonScope => typeof entry === 'string' && VALID_SCOPES.has(entry),
  );

  if (!allValid) {
    return [];
  }

  return input as AddonScope[];
}

// ---------------------------------------------------------------------------
// Internal validation helpers
// ---------------------------------------------------------------------------

function validateIdentityFields(
  m: Record<string, unknown>,
  errors: string[],
): void {
  // name — required, non-empty string
  if (typeof m['name'] !== 'string' || m['name'].trim() === '') {
    errors.push('Missing required field: name (must be a non-empty string).');
  }

  // version — required, must match SEMVER_RE
  if (typeof m['version'] !== 'string' || m['version'].trim() === '') {
    errors.push('Missing required field: version (must be a semver string).');
  } else if (!SEMVER_RE.test(m['version'])) {
    errors.push(
      `Invalid version: "${m['version']}" does not follow semver format (expected MAJOR.MINOR.PATCH).`,
    );
  }

  // type — required, one of the four known values
  if (m['type'] === undefined || m['type'] === null) {
    errors.push(
      `Missing required field: type (must be one of: hook, plugin, skill, agent).`,
    );
  } else if (typeof m['type'] !== 'string' || !VALID_ADDON_TYPES.has(m['type'])) {
    errors.push(
      `Unknown type: "${String(m['type'])}". Valid types are: hook, plugin, skill, agent.`,
    );
  }
}

function validateFilesField(
  m: Record<string, unknown>,
  errors: string[],
): void {
  if (!Array.isArray(m['files'])) {
    errors.push('Missing required field: files (must be a non-empty array of relative path strings).');
    return;
  }

  if ((m['files'] as unknown[]).length === 0) {
    errors.push('files array must not be empty — at least one file is required.');
    return;
  }

  for (const entry of m['files'] as unknown[]) {
    if (typeof entry !== 'string') {
      errors.push(`Each files entry must be a string; got: ${JSON.stringify(entry)}.`);
      continue;
    }
    const pathError = unsafePathError(entry);
    if (pathError !== null) {
      errors.push(pathError);
    }
  }
}

function validateSupportedScopes(
  rawScopes: unknown,
  addonType: string | undefined,
  errors: string[],
): AddonScope[] {
  if (rawScopes === undefined || rawScopes === null) {
    errors.push('Missing required field: supportedScopes (must be an array of "local"|"global", or "both").');
    return [];
  }

  const normalized = normalizeSupportedScopes(rawScopes);

  if (normalized.length === 0) {
    // Distinguish empty-array from invalid value for a clearer message.
    if (Array.isArray(rawScopes) && (rawScopes as unknown[]).length === 0) {
      errors.push('supportedScopes must not be empty — at least one supported scope is required.');
    } else {
      errors.push(
        `Invalid supportedScopes value: ${JSON.stringify(rawScopes)}. ` +
          `Must be an array of "local"|"global", or the shorthand "both".`,
      );
    }
    return [];
  }

  // Plugin is global-only.
  if (addonType === 'plugin' && normalized.includes('local')) {
    errors.push(
      'plugin add-ons are global-only and must not include "local" in supportedScopes. ' +
        'Use supportedScopes: ["global"].',
    );
  }

  return normalized;
}

function validateHookField(
  m: Record<string, unknown>,
  addonType: string | undefined,
  files: string[],
  errors: string[],
): void {
  const hookPresent = 'hook' in m && m['hook'] !== undefined && m['hook'] !== null;

  if (addonType === 'hook') {
    if (!hookPresent) {
      errors.push('hook add-ons must include a "hook" object (event, matcher, command).');
      return;
    }

    if (typeof m['hook'] !== 'object' || Array.isArray(m['hook'])) {
      errors.push('hook must be an object with fields: event, matcher, command.');
      return;
    }

    const h = m['hook'] as Record<string, unknown>;

    if (typeof h['event'] !== 'string' || h['event'].trim() === '') {
      errors.push('hook.event must be a non-empty string.');
    }

    if (typeof h['matcher'] !== 'string' || h['matcher'].trim() === '') {
      errors.push('hook.matcher must be a non-empty string.');
    }

    if (typeof h['command'] !== 'string' || h['command'].trim() === '') {
      errors.push('hook.command must be a non-empty string.');
    } else if (!files.includes(h['command'] as string)) {
      errors.push(
        `hook.command "${h['command']}" must reference one of the declared files entries: ` +
          `[${files.map((f) => `"${f}"`).join(', ')}].`,
      );
    }
  } else {
    // Non-hook type — hook field must not be present.
    if (hookPresent) {
      errors.push(
        'hook entry is only valid for hook add-ons. ' +
          `Remove the "hook" field from this "${addonType ?? 'unknown'}" manifest.`,
      );
    }
  }
}

function validatePerTypeFilesConstraints(
  addonType: string | undefined,
  files: string[],
  errors: string[],
  warnings: string[],
): void {
  if (addonType === 'agent') {
    if (files.length !== 1) {
      errors.push(
        `agent add-ons must declare exactly one file (got ${files.length}). ` +
          'An agent is a single markdown file.',
      );
    } else {
      // Warn if the single file is not a .md file.
      if (!files[0].endsWith('.md')) {
        warnings.push(
          `agent file "${files[0]}" does not end in ".md". ` +
            'Agent add-ons are expected to be markdown files.',
        );
      }
    }
  }

  if (addonType === 'skill') {
    const hasSkillMd = files.some((f) => f === 'SKILL.md' || f.endsWith('/SKILL.md'));
    if (!hasSkillMd) {
      warnings.push(
        'skill add-on does not include a "SKILL.md" file. ' +
          'Consider adding SKILL.md as the skill entry point.',
      );
    }
  }
}

// ---------------------------------------------------------------------------
// validateAddonManifest — public API
// ---------------------------------------------------------------------------

/**
 * Pure validation function for addon.json manifests.
 *
 * Narrows from `unknown` via type guards — no `any`.
 * Returns an immutable-style result `{ valid, errors, warnings }`.
 */
export function validateAddonManifest(input: unknown): AddonValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];

  if (input === null || typeof input !== 'object' || Array.isArray(input)) {
    return {
      valid: false,
      errors: ['Manifest must be a JSON object.'],
      warnings,
    };
  }

  const m = input as Record<string, unknown>;

  // 1. Identity fields: name, version, type
  validateIdentityFields(m, errors);

  // 2. files array
  validateFilesField(m, errors);

  // Collect valid files (those that passed path guards) for cross-field checks.
  const safeFiles: string[] =
    Array.isArray(m['files'])
      ? (m['files'] as unknown[]).filter(
          (f): f is string => typeof f === 'string' && isSafePath(f),
        )
      : [];

  // Derive the type string for cross-field checks (may be undefined/invalid).
  const addonType: string | undefined =
    typeof m['type'] === 'string' ? m['type'] : undefined;

  // 3. supportedScopes
  validateSupportedScopes(m['supportedScopes'], addonType, errors);

  // 4. Cross-field: hook object
  validateHookField(m, addonType, safeFiles, errors);

  // 5. Per-type files constraints (errors + warnings)
  validatePerTypeFilesConstraints(addonType, safeFiles, errors, warnings);

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  };
}
