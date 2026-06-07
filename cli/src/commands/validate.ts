/**
 * Validate command — validate a plugin manifest.
 */

import * as nodeFsPromises from 'node:fs/promises';
import * as path from 'node:path';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface PluginManifest {
  name: string;
  version: string;
  description: string;
  author: string;
  types: string[];
  languages: string[];
  entrypoints: string[];
  useCaseTags?: string[];
  dependencies?: Record<string, string>;
  license?: string;
  docsUrl?: string;
}

export interface ValidationResult {
  valid: boolean;
  errors: string[];
  warnings: string[];
}

export interface ValidateFsPort {
  readFile(p: string): Promise<string>;
  exists(p: string): Promise<boolean>;
}

export interface ValidateArgs {
  pluginPath?: string;
}

export interface ValidateDeps {
  fs?: ValidateFsPort;
  cwd?: string;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const VALID_TYPES = new Set(['skill', 'hook', 'agent', 'command', 'plugin']);
const SEMVER_RE = /^\d+\.\d+\.\d+([+-][a-zA-Z0-9._+-]*)?$/;

// ---------------------------------------------------------------------------
// Pure validation
// ---------------------------------------------------------------------------

export function validateManifest(manifest: unknown): ValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];

  if (manifest === null || typeof manifest !== 'object') {
    return { valid: false, errors: ['Manifest must be an object'], warnings };
  }

  const m = manifest as Record<string, unknown>;

  // Required string fields
  const requiredStrings: (keyof PluginManifest)[] = ['name', 'version', 'description', 'author'];
  for (const field of requiredStrings) {
    if (!m[field] || typeof m[field] !== 'string' || (m[field] as string).trim() === '') {
      errors.push(`Missing required field: ${field}`);
    }
  }

  // Required array fields
  if (!Array.isArray(m['types']) || (m['types'] as string[]).length === 0) {
    errors.push('Missing required field: types');
  } else {
    const invalid = (m['types'] as string[]).filter((t) => !VALID_TYPES.has(t));
    if (invalid.length > 0) {
      errors.push(`Invalid type value(s): ${invalid.join(', ')}. Valid: skill, hook, agent, command, plugin`);
    }
  }

  if (!Array.isArray(m['languages']) || (m['languages'] as string[]).length === 0) {
    errors.push('Missing required field: languages');
  }

  if (!Array.isArray(m['entrypoints']) || (m['entrypoints'] as string[]).length === 0) {
    errors.push('Missing required field: entrypoints');
  }

  // Semver validation
  if (typeof m['version'] === 'string' && m['version'].trim() !== '') {
    if (!SEMVER_RE.test(m['version'])) {
      errors.push(`Invalid version: "${m['version']}" is not valid semver`);
    }
  }

  // Dependency warnings (not errors)
  if (m['dependencies'] && typeof m['dependencies'] === 'object') {
    const deps = m['dependencies'] as Record<string, string>;
    const depCount = Object.keys(deps).length;
    if (depCount > 0) {
      warnings.push(
        `Plugin declares ${depCount} dependenc${depCount === 1 ? 'y' : 'ies'}. ` +
          `Ensure they are available in the target environment.`,
      );
    }
  }

  return { valid: errors.length === 0, errors, warnings };
}

// ---------------------------------------------------------------------------
// Default real FS implementation
// ---------------------------------------------------------------------------

const realValidateFsPort: ValidateFsPort = {
  async readFile(p) {
    return nodeFsPromises.readFile(p, 'utf-8');
  },
  async exists(p) {
    try {
      await nodeFsPromises.stat(p);
      return true;
    } catch {
      return false;
    }
  },
};

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runValidate(args: ValidateArgs, deps: ValidateDeps): Promise<CommandResult> {
  const { pluginPath } = args;
  const { fs: fsPort = realValidateFsPort, cwd = process.cwd() } = deps;

  const dir = pluginPath ?? cwd;
  const manifestPath = path.join(dir, 'plugin.json');

  // ── 1. Check manifest exists ─────────────────────────────────────────────
  const exists = await fsPort.exists(manifestPath);
  if (!exists) {
    return {
      exitCode: 2,
      output: [`plugin.json not found in ${dir}`, `Run \`claude plugin scaffold\` to generate a plugin template.`].join(
        '\n',
      ),
    };
  }

  // ── 2. Read and parse manifest ───────────────────────────────────────────
  let parsed: unknown;
  try {
    const raw = await fsPort.readFile(manifestPath);
    parsed = JSON.parse(raw);
  } catch {
    return {
      exitCode: 1,
      output: `Invalid plugin.json: file contains malformed JSON.`,
    };
  }

  // ── 3. Validate ──────────────────────────────────────────────────────────
  const result = validateManifest(parsed);

  if (!result.valid) {
    const lines: string[] = ['Validation failed:', ...result.errors.map((e) => `  - ${e}`)];
    if (result.warnings.length > 0) {
      lines.push('Warnings:', ...result.warnings.map((w) => `  - ${w}`));
    }
    return { exitCode: 1, output: lines.join('\n') };
  }

  const lines: string[] = ['Manifest is valid.'];
  if (result.warnings.length > 0) {
    lines.push('Warnings:', ...result.warnings.map((w) => `  - warn: ${w}`));
  }

  return { exitCode: 0, output: lines.join('\n') };
}
