/**
 * Tests for src/config/config.ts
 *
 * Production module path: src/config/config.ts
 * Exported functions / types:
 *   - DEFAULT_API_URL: string = 'https://plugins.claudeforge.dev'
 *   - ENV_KEY_API_URL: string = 'CLAUDE_PLUGINS_API_URL'  (deprecated, kept for compat)
 *   - ENV_KEY_HOME: string = 'CLAUDE_PLUGINS_HOME'
 *   - API_URL_ENV: string = 'CLAUDEFORGE_API_URL'
 *   - resolveHome(env?: NodeJS.ProcessEnv): string
 *       → returns env.CLAUDE_PLUGINS_HOME or os.homedir() + '/.claude-plugins'
 *   - resolveApiUrl(config: CliConfig, env?: NodeJS.ProcessEnv): string
 *       → precedence: env.CLAUDEFORGE_API_URL (trimmed) > config.apiUrl > DEFAULT_API_URL
 *       → env value is NEVER persisted; ephemeral only
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
  API_URL_ENV,
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

  it('ENV_KEY_API_URL is CLAUDE_PLUGINS_API_URL (deprecated compat)', () => {
    expect(ENV_KEY_API_URL).toBe('CLAUDE_PLUGINS_API_URL');
  });

  it('ENV_KEY_HOME is CLAUDE_PLUGINS_HOME', () => {
    expect(ENV_KEY_HOME).toBe('CLAUDE_PLUGINS_HOME');
  });

  it('API_URL_ENV is CLAUDEFORGE_API_URL', () => {
    expect(API_URL_ENV).toBe('CLAUDEFORGE_API_URL');
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
  // --- Tier 1: CLAUDEFORGE_API_URL env override (highest priority) ---

  it('returns env URL when CLAUDEFORGE_API_URL is set, regardless of config and default', () => {
    const config: CliConfig = { apiUrl: 'https://persisted.example.com' };
    const result = resolveApiUrl(config, { CLAUDEFORGE_API_URL: 'http://localhost:9999' });
    expect(result).toBe('http://localhost:9999');
  });

  it('trims whitespace from env value before using it', () => {
    const config: CliConfig = { apiUrl: DEFAULT_API_URL };
    const result = resolveApiUrl(config, { CLAUDEFORGE_API_URL: '  http://localhost:8080  ' });
    expect(result).toBe('http://localhost:8080');
  });

  it('ignores env value that is empty string and falls through to config', () => {
    const config: CliConfig = { apiUrl: 'https://persisted.example.com' };
    const result = resolveApiUrl(config, { CLAUDEFORGE_API_URL: '' });
    expect(result).toBe('https://persisted.example.com');
  });

  it('ignores env value that is whitespace-only and falls through to config', () => {
    const config: CliConfig = { apiUrl: 'https://persisted.example.com' };
    const result = resolveApiUrl(config, { CLAUDEFORGE_API_URL: '   ' });
    expect(result).toBe('https://persisted.example.com');
  });

  it('ignores env value that is whitespace-only and falls through to DEFAULT_API_URL when no config set', () => {
    const config: CliConfig = { apiUrl: DEFAULT_API_URL };
    const result = resolveApiUrl(config, { CLAUDEFORGE_API_URL: '   ' });
    // config.apiUrl === DEFAULT_API_URL so this should still return DEFAULT_API_URL
    expect(result).toBe(DEFAULT_API_URL);
  });

  // --- Tier 2: persisted config.apiUrl ---

  it('returns config.apiUrl when env is not set', () => {
    const config: CliConfig = { apiUrl: 'https://persisted.example.com' };
    const result = resolveApiUrl(config, {});
    expect(result).toBe('https://persisted.example.com');
  });

  // --- Tier 3: DEFAULT_API_URL (fallback) ---

  it('returns DEFAULT_API_URL when no env is set and config holds default', () => {
    const config: CliConfig = { apiUrl: DEFAULT_API_URL };
    const result = resolveApiUrl(config, {});
    expect(result).toBe(DEFAULT_API_URL);
  });

  // --- Ephemerality: env override does NOT leak across calls ---

  it('is ephemeral: resolving again with empty env returns persisted config value', () => {
    const config: CliConfig = { apiUrl: 'https://persisted.example.com' };
    // First call WITH env override
    const withOverride = resolveApiUrl(config, { CLAUDEFORGE_API_URL: 'http://localhost:9999' });
    expect(withOverride).toBe('http://localhost:9999');
    // Second call WITHOUT env override — must return persisted value, not the env one
    const withoutOverride = resolveApiUrl(config, {});
    expect(withoutOverride).toBe('https://persisted.example.com');
  });

  it('is ephemeral: resolving again with no env set returns DEFAULT_API_URL when config is default', () => {
    const config: CliConfig = { apiUrl: DEFAULT_API_URL };
    const withOverride = resolveApiUrl(config, { CLAUDEFORGE_API_URL: 'http://localhost:7777' });
    expect(withOverride).toBe('http://localhost:7777');
    const withoutOverride = resolveApiUrl(config, {});
    expect(withoutOverride).toBe(DEFAULT_API_URL);
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
