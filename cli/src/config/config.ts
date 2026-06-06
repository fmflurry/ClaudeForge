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

export const DEFAULT_API_URL = 'https://plugins.claudeforge.dev';
export const ENV_KEY_API_URL = 'CLAUDE_PLUGINS_API_URL';
export const ENV_KEY_HOME = 'CLAUDE_PLUGINS_HOME';

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

export function resolveApiUrl(
  explicit: string | undefined,
  env?: NodeJS.ProcessEnv,
): string {
  if (explicit && explicit.length > 0) {
    return explicit;
  }
  const fromEnv = env?.[ENV_KEY_API_URL];
  if (fromEnv && fromEnv.length > 0) {
    return fromEnv;
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
  await fs.writeFile(
    path.join(homeDir, CONFIG_FILENAME),
    JSON.stringify(snapshot, null, 2),
    'utf-8',
  );
}
