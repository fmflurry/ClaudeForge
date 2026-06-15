/**
 * CLI configuration module.
 * Manages the config.json file in the home directory.
 * No mutations — all writes produce new objects.
 */

import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

// PROVISIONAL: The production API endpoint is not yet finalised.
// This matches the claudeforge.fr website domain and the infra `api.` subdomain
// convention used by JWT issuers and OIDC callbacks in infra/docker-compose*.yml.
// Override at runtime without touching this file: CLAUDEFORGE_API_URL=<url> (see API_URL_ENV below).
export const DEFAULT_API_URL = 'https://api.claudeforge.fr';
/** @deprecated Use API_URL_ENV. Kept for backward compatibility with CLAUDE_PLUGINS_API_URL consumers. */
export const ENV_KEY_API_URL = 'CLAUDE_PLUGINS_API_URL';
export const ENV_KEY_HOME = 'CLAUDE_PLUGINS_HOME';

/**
 * Environment variable name for the ephemeral API URL override.
 * Set this to point the CLI at a local backend (e.g. a Docker test container)
 * without touching the persisted config.json.
 * Example: CLAUDEFORGE_API_URL=http://localhost:9999 claude-plugin search foo
 */
export const API_URL_ENV = 'CLAUDEFORGE_API_URL';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CliConfig {
  apiUrl: string;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

export function resolveHome(env?: NodeJS.ProcessEnv): string {
  return env?.[ENV_KEY_HOME] ?? path.join(os.homedir(), '.claude-plugins');
}

/**
 * Resolve the effective API URL applying the following precedence (highest first):
 *   1. process.env.CLAUDEFORGE_API_URL — ephemeral override for local/docker/staging.
 *      Trimmed; empty or whitespace-only values are ignored (fall through to next tier).
 *      This value is NEVER persisted — a subsequent call without the env var reverts to
 *      the persisted/default value.
 *   2. config.apiUrl — persisted value from ~/.claude-plugins/config.json.
 *   3. DEFAULT_API_URL — production default; never mutated.
 *
 * @param config  Loaded CliConfig (contains the persisted apiUrl).
 * @param env     Environment map to read; defaults to process.env. Injected in tests.
 */
export function resolveApiUrl(config: CliConfig, env: NodeJS.ProcessEnv = process.env): string {
  const fromEnv = env[API_URL_ENV]?.trim();
  if (fromEnv && fromEnv.length > 0) {
    return fromEnv;
  }
  if (config.apiUrl && config.apiUrl.length > 0) {
    return config.apiUrl;
  }
  return DEFAULT_API_URL;
}

export function validateUrl(url: string): boolean {
  if (!url || url.length === 0) {
    return false;
  }
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
      return false;
    }
    if (!parsed.hostname || parsed.hostname.length === 0) {
      return false;
    }
    return true;
  } catch {
    return false;
  }
}

const CONFIG_FILENAME = 'config.json';
const DEFAULT_CONFIG: CliConfig = { apiUrl: DEFAULT_API_URL };

export async function readConfig(homeDir: string): Promise<CliConfig> {
  try {
    const raw = await fs.readFile(path.join(homeDir, CONFIG_FILENAME), 'utf-8');
    if (!raw || raw.trim().length === 0) {
      return { ...DEFAULT_CONFIG };
    }
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'apiUrl' in parsed &&
      typeof (parsed as { apiUrl: unknown }).apiUrl === 'string'
    ) {
      return { apiUrl: (parsed as { apiUrl: string }).apiUrl };
    }
    return { ...DEFAULT_CONFIG };
  } catch {
    return { ...DEFAULT_CONFIG };
  }
}

export async function writeConfig(homeDir: string, config: CliConfig): Promise<void> {
  await fs.mkdir(homeDir, { recursive: true });
  const snapshot: CliConfig = { ...config };
  await fs.writeFile(path.join(homeDir, CONFIG_FILENAME), JSON.stringify(snapshot, null, 2), 'utf-8');
}
