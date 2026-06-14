/**
 * Tests for src/commands/config.ts
 *
 * Production module path: src/commands/config.ts
 * Exported functions:
 *   - runConfigSet(args: ConfigSetArgs, deps: ConfigDeps): Promise<CommandResult>
 *       args: { key: string; value: string }
 *       deps: { homeDir: string; env?: NodeJS.ProcessEnv; connectivityCheck?: ConnectivityCheck }
 *   - runConfigShow(deps: ConfigShowDeps): Promise<CommandResult>
 *       deps: { homeDir: string; env?: NodeJS.ProcessEnv }
 *   - CommandResult: { exitCode: number; output: string }
 *   - ConnectivityCheck: (url: string) => Promise<boolean>
 *
 * VERBATIM spec strings (from spec.md):
 *   - success: "API URL set to <url>"  (connectivity OK)
 *   - connectivity fail: "Could not connect to API at <url>"
 *   - show output:  "API URL: <url>"  (current configured URL)
 *   - invalid URL: "Invalid URL format: <url>"
 *   - unknown key: "Unknown config key: <key>. Valid keys: api-url"
 */

import { describe, it, expect, vi } from 'vitest';
import * as os from 'node:os';
import * as path from 'node:path';
import * as fs from 'node:fs/promises';

// These imports WILL FAIL until src/commands/config.ts is created (RED state).
import { runConfigSet, runConfigShow } from '../commands/config.js';
import type { ConnectivityCheck } from '../commands/config.js';
import { DEFAULT_API_URL } from '../config/config.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-cmd-config-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

const alwaysConnects: ConnectivityCheck = vi.fn().mockResolvedValue(true);
const neverConnects: ConnectivityCheck = vi.fn().mockResolvedValue(false);

// ---------------------------------------------------------------------------
// runConfigSet — api-url
// ---------------------------------------------------------------------------

describe('runConfigSet – api-url', () => {
  it('stores the URL and returns exitCode 0 on connectivity success', async () => {
    const homeDir = await makeTmpDir();
    try {
      const result = await runConfigSet(
        { key: 'api-url', value: 'https://custom-marketplace.local' },
        { homeDir, connectivityCheck: alwaysConnects },
      );
      expect(result.exitCode).toBe(0);
      expect(result.output).toContain('https://custom-marketplace.local');
      // Verbatim: "API URL set to <url>"
      expect(result.output).toMatch(/API URL set to/i);
    } finally {
      await removeTmpDir(homeDir);
    }
  });

  it('persists the URL to config.json after a successful set', async () => {
    const homeDir = await makeTmpDir();
    try {
      await runConfigSet(
        { key: 'api-url', value: 'https://persisted.example.com' },
        { homeDir, connectivityCheck: alwaysConnects },
      );
      const raw = await fs.readFile(path.join(homeDir, 'config.json'), 'utf-8');
      const parsed = JSON.parse(raw) as { apiUrl: string };
      expect(parsed.apiUrl).toBe('https://persisted.example.com');
    } finally {
      await removeTmpDir(homeDir);
    }
  });

  it('returns a message and exitCode > 0 when connectivity check fails', async () => {
    const homeDir = await makeTmpDir();
    try {
      const result = await runConfigSet(
        { key: 'api-url', value: 'https://custom-marketplace.local' },
        { homeDir, connectivityCheck: neverConnects },
      );
      // Verbatim spec: "Could not connect to API at https://custom-marketplace.local"
      expect(result.output).toContain('Could not connect to API at');
      expect(result.output).toContain('https://custom-marketplace.local');
      expect(result.exitCode).toBeGreaterThan(0);
    } finally {
      await removeTmpDir(homeDir);
    }
  });

  it('rejects an invalid URL format without calling connectivity check', async () => {
    const homeDir = await makeTmpDir();
    const connectCheck: ConnectivityCheck = vi.fn();
    try {
      const result = await runConfigSet(
        { key: 'api-url', value: 'not-a-url' },
        { homeDir, connectivityCheck: connectCheck },
      );
      expect(result.output).toContain('Invalid URL format: not-a-url');
      expect(result.exitCode).toBeGreaterThan(0);
      expect(connectCheck).not.toHaveBeenCalled();
    } finally {
      await removeTmpDir(homeDir);
    }
  });

  it('reports an error for unknown config keys', async () => {
    const homeDir = await makeTmpDir();
    try {
      const result = await runConfigSet(
        { key: 'unknown-key', value: 'something' },
        { homeDir, connectivityCheck: alwaysConnects },
      );
      expect(result.output).toContain('Unknown config key: unknown-key');
      expect(result.output).toContain('api-url');
      expect(result.exitCode).toBeGreaterThan(0);
    } finally {
      await removeTmpDir(homeDir);
    }
  });
});

// ---------------------------------------------------------------------------
// runConfigShow
// ---------------------------------------------------------------------------

describe('runConfigShow', () => {
  it('displays the default API URL when no config file exists', async () => {
    const homeDir = await makeTmpDir();
    try {
      const result = await runConfigShow({ homeDir });
      // Verbatim spec: "API URL: <url>"
      expect(result.output).toContain('API URL:');
      expect(result.output).toContain(DEFAULT_API_URL);
      expect(result.exitCode).toBe(0);
    } finally {
      await removeTmpDir(homeDir);
    }
  });

  it('displays a custom URL when one has been configured', async () => {
    const homeDir = await makeTmpDir();
    try {
      await fs.writeFile(
        path.join(homeDir, 'config.json'),
        JSON.stringify({ apiUrl: 'https://my-server.example.com' }),
        'utf-8',
      );
      const result = await runConfigShow({ homeDir });
      expect(result.output).toContain('https://my-server.example.com');
      expect(result.exitCode).toBe(0);
    } finally {
      await removeTmpDir(homeDir);
    }
  });

  it('uses CLAUDEFORGE_API_URL env override when no config file is set', async () => {
    const homeDir = await makeTmpDir();
    try {
      const result = await runConfigShow({
        homeDir,
        env: { CLAUDEFORGE_API_URL: 'https://env-override.example.com' },
      });
      expect(result.output).toContain('https://env-override.example.com');
      expect(result.exitCode).toBe(0);
    } finally {
      await removeTmpDir(homeDir);
    }
  });

  it('returns exitCode 0 and output even when config.json is corrupt', async () => {
    const homeDir = await makeTmpDir();
    try {
      await fs.writeFile(path.join(homeDir, 'config.json'), '{broken', 'utf-8');
      const result = await runConfigShow({ homeDir });
      expect(result.exitCode).toBe(0);
      expect(result.output).toContain('API URL:');
    } finally {
      await removeTmpDir(homeDir);
    }
  });
});
