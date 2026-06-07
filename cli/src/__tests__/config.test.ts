/**
 * Tests for src/config/config.ts
 *
 * Production module path: src/config/config.ts
 * Exported functions / types:
 *   - DEFAULT_API_URL: string = 'https://plugins.claudeforge.dev'
 *   - ENV_KEY_API_URL: string = 'CLAUDE_PLUGINS_API_URL'
 *   - ENV_KEY_HOME: string = 'CLAUDE_PLUGINS_HOME'
 *   - resolveHome(env?: NodeJS.ProcessEnv): string
 *       → returns env.CLAUDE_PLUGINS_HOME or os.homedir() + '/.claude-plugins'
 *   - resolveApiUrl(explicit: string | undefined, env?: NodeJS.ProcessEnv): string
 *       → precedence: explicit > env.CLAUDE_PLUGINS_API_URL > DEFAULT_API_URL
 *   - validateUrl(url: string): boolean
 *       → returns true only for well-formed http/https URLs
 *   - readConfig(homeDir: string): Promise<CliConfig>
 *       → reads homeDir/config.json; returns defaults when file absent or corrupt
 *   - writeConfig(homeDir: string, config: CliConfig): Promise<void>
 *       → writes homeDir/config.json immutably (never mutates input object)
 *   - CliConfig interface: { apiUrl: string }
 *
 * Exit-code model: commands return { exitCode: number; output: string }
 * Home / FS injection: all functions accept homeDir as a string param (tests use tmp dirs)
 * Network injection: commands accept an IMarketplaceClient param
 */

import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

// These imports WILL FAIL until src/config/config.ts is created (RED state).
import {
  DEFAULT_API_URL,
  ENV_KEY_API_URL,
  ENV_KEY_HOME,
  resolveHome,
  resolveApiUrl,
  validateUrl,
  readConfig,
  writeConfig,
} from '../config/config.js';
import type { CliConfig } from '../config/config.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-plugins-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

describe('config – constants', () => {
  it('DEFAULT_API_URL is the official marketplace URL', () => {
    expect(DEFAULT_API_URL).toBe('https://plugins.claudeforge.dev');
  });

  it('ENV_KEY_API_URL is CLAUDE_PLUGINS_API_URL', () => {
    expect(ENV_KEY_API_URL).toBe('CLAUDE_PLUGINS_API_URL');
  });

  it('ENV_KEY_HOME is CLAUDE_PLUGINS_HOME', () => {
    expect(ENV_KEY_HOME).toBe('CLAUDE_PLUGINS_HOME');
  });
});

// ---------------------------------------------------------------------------
// resolveHome
// ---------------------------------------------------------------------------

describe('resolveHome', () => {
  it('returns the value from CLAUDE_PLUGINS_HOME env when set', () => {
    const customHome = '/tmp/custom-plugins-home';
    const result = resolveHome({ CLAUDE_PLUGINS_HOME: customHome });
    expect(result).toBe(customHome);
  });

  it('falls back to os.homedir()/.claude-plugins when env is not set', () => {
    const result = resolveHome({});
    expect(result).toBe(path.join(os.homedir(), '.claude-plugins'));
  });

  it('falls back to os.homedir()/.claude-plugins when env is undefined', () => {
    const result = resolveHome(undefined);
    expect(result).toBe(path.join(os.homedir(), '.claude-plugins'));
  });
});

// ---------------------------------------------------------------------------
// resolveApiUrl
// ---------------------------------------------------------------------------

describe('resolveApiUrl', () => {
  it('returns explicit URL when provided, ignoring env and default', () => {
    const result = resolveApiUrl('https://explicit.example.com', { CLAUDE_PLUGINS_API_URL: 'https://env.example.com' });
    expect(result).toBe('https://explicit.example.com');
  });

  it('returns env URL when no explicit URL provided', () => {
    const result = resolveApiUrl(undefined, { CLAUDE_PLUGINS_API_URL: 'https://env.example.com' });
    expect(result).toBe('https://env.example.com');
  });

  it('returns DEFAULT_API_URL when neither explicit nor env is set', () => {
    const result = resolveApiUrl(undefined, {});
    expect(result).toBe(DEFAULT_API_URL);
  });

  it('returns DEFAULT_API_URL when explicit is empty string and env is not set', () => {
    const result = resolveApiUrl('', {});
    expect(result).toBe(DEFAULT_API_URL);
  });

  it('prefers explicit over env when both present', () => {
    const result = resolveApiUrl('https://explicit.test', {
      CLAUDE_PLUGINS_API_URL: 'https://env.test',
    });
    expect(result).toBe('https://explicit.test');
  });
});

// ---------------------------------------------------------------------------
// validateUrl
// ---------------------------------------------------------------------------

describe('validateUrl', () => {
  it('accepts a valid https URL', () => {
    expect(validateUrl('https://plugins.claudeforge.dev')).toBe(true);
  });

  it('accepts a valid http URL', () => {
    expect(validateUrl('http://localhost:3000')).toBe(true);
  });

  it('accepts https URL with path', () => {
    expect(validateUrl('https://custom-marketplace.local/api')).toBe(true);
  });

  it('rejects an empty string', () => {
    expect(validateUrl('')).toBe(false);
  });

  it('rejects a plain string without protocol', () => {
    expect(validateUrl('not-a-url')).toBe(false);
  });

  it('rejects ftp:// URLs (not http/https)', () => {
    expect(validateUrl('ftp://example.com')).toBe(false);
  });

  it('rejects URLs without a hostname', () => {
    expect(validateUrl('https://')).toBe(false);
  });

  it('rejects null-like/undefined-coerced input gracefully', () => {
    // TypeScript will prevent truly invalid types; test boundary strings
    expect(validateUrl('https://?')).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// readConfig / writeConfig
// ---------------------------------------------------------------------------

describe('readConfig', () => {
  let tmpDir: string;

  beforeEach(async () => {
    tmpDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(tmpDir);
  });

  it('returns default CliConfig when config.json does not exist', async () => {
    const config = await readConfig(tmpDir);
    expect(config).toEqual<CliConfig>({ apiUrl: DEFAULT_API_URL });
  });

  it('returns written config when config.json exists with valid data', async () => {
    const expected: CliConfig = { apiUrl: 'https://custom.example.com' };
    await fs.writeFile(path.join(tmpDir, 'config.json'), JSON.stringify(expected), 'utf-8');
    const config = await readConfig(tmpDir);
    expect(config).toEqual(expected);
  });

  it('returns defaults when config.json is corrupt JSON', async () => {
    await fs.writeFile(path.join(tmpDir, 'config.json'), '{invalid json', 'utf-8');
    const config = await readConfig(tmpDir);
    expect(config).toEqual<CliConfig>({ apiUrl: DEFAULT_API_URL });
  });

  it('returns defaults when config.json is empty', async () => {
    await fs.writeFile(path.join(tmpDir, 'config.json'), '', 'utf-8');
    const config = await readConfig(tmpDir);
    expect(config).toEqual<CliConfig>({ apiUrl: DEFAULT_API_URL });
  });

  it('does not throw on missing directory (handles gracefully)', async () => {
    const nonExistent = path.join(tmpDir, 'nonexistent-subdir');
    await expect(readConfig(nonExistent)).resolves.toEqual<CliConfig>({
      apiUrl: DEFAULT_API_URL,
    });
  });
});

describe('writeConfig', () => {
  let tmpDir: string;

  beforeEach(async () => {
    tmpDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(tmpDir);
  });

  it('creates config.json with the supplied config', async () => {
    const config: CliConfig = { apiUrl: 'https://example.com' };
    await writeConfig(tmpDir, config);
    const raw = await fs.readFile(path.join(tmpDir, 'config.json'), 'utf-8');
    expect(JSON.parse(raw)).toEqual(config);
  });

  it('creates the home directory when it does not exist', async () => {
    const nested = path.join(tmpDir, 'auto-created');
    const config: CliConfig = { apiUrl: DEFAULT_API_URL };
    await writeConfig(nested, config);
    const raw = await fs.readFile(path.join(nested, 'config.json'), 'utf-8');
    expect(JSON.parse(raw)).toEqual(config);
  });

  it('overwrites an existing config.json immutably (does not mutate the input object)', async () => {
    const original: CliConfig = { apiUrl: 'https://original.example.com' };
    await writeConfig(tmpDir, original);
    const updated: CliConfig = { apiUrl: 'https://updated.example.com' };
    await writeConfig(tmpDir, updated);
    const raw = await fs.readFile(path.join(tmpDir, 'config.json'), 'utf-8');
    expect(JSON.parse(raw)).toEqual(updated);
    // Input object must not have been mutated
    expect(updated.apiUrl).toBe('https://updated.example.com');
  });

  it('round-trips: writeConfig then readConfig returns the same value', async () => {
    const config: CliConfig = { apiUrl: 'https://round-trip.example.com' };
    await writeConfig(tmpDir, config);
    const result = await readConfig(tmpDir);
    expect(result).toEqual(config);
  });
});
