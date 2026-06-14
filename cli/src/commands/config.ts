/**
 * Config command — get/set CLI configuration.
 */

import { API_URL_ENV, readConfig, writeConfig, validateUrl, resolveApiUrl } from '../config/config.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export type ConnectivityCheck = (url: string) => Promise<boolean>;

export interface ConfigSetArgs {
  key: string;
  value: string;
}

export interface ConfigDeps {
  homeDir: string;
  env?: NodeJS.ProcessEnv;
  connectivityCheck?: ConnectivityCheck;
}

export interface ConfigShowDeps {
  homeDir: string;
  env?: NodeJS.ProcessEnv;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const VALID_KEYS = ['api-url'] as const;
type ValidKey = (typeof VALID_KEYS)[number];

function isValidKey(key: string): key is ValidKey {
  return VALID_KEYS.includes(key as ValidKey);
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

export async function runConfigSet(args: ConfigSetArgs, deps: ConfigDeps): Promise<CommandResult> {
  const { key, value } = args;
  const { homeDir, connectivityCheck } = deps;

  if (!isValidKey(key)) {
    return {
      exitCode: 1,
      output: `Unknown config key: ${key}. Valid keys: api-url`,
    };
  }

  if (!validateUrl(value)) {
    return {
      exitCode: 1,
      output: `Invalid URL format: ${value}`,
    };
  }

  if (connectivityCheck) {
    const reachable = await connectivityCheck(value);
    if (!reachable) {
      return {
        exitCode: 1,
        output: `Could not connect to API at ${value}`,
      };
    }
  }

  const current = await readConfig(homeDir);
  const updated = { ...current, apiUrl: value };
  await writeConfig(homeDir, updated);

  const lines: string[] = [`API URL set to ${value}`];

  // Informational warning: plain HTTP to a remote host sends credentials in clear text.
  try {
    const parsed = new URL(value);
    const isLocalhost = parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1' || parsed.hostname === '::1';
    if (parsed.protocol === 'http:' && !isLocalhost) {
      lines.push(
        'Warning: the configured URL uses plain HTTP with a non-localhost host. ' +
          'Consider using HTTPS to avoid sending credentials in clear text.',
      );
    }
  } catch {
    // validateUrl already accepted the URL; URL constructor should never throw here.
  }

  return {
    exitCode: 0,
    output: lines.join('\n'),
  };
}

export async function runConfigShow(deps: ConfigShowDeps): Promise<CommandResult> {
  const { homeDir, env } = deps;
  const config = await readConfig(homeDir);

  // resolveApiUrl applies precedence: CLAUDEFORGE_API_URL env > persisted config > default.
  const resolvedEnv = env ?? process.env;
  const effectiveUrl = resolveApiUrl(config, resolvedEnv);

  const lines: string[] = [`API URL: ${effectiveUrl}`];

  // Inform the user when an ephemeral env override is shadowing the persisted value.
  const envOverride = resolvedEnv[API_URL_ENV]?.trim();
  if (envOverride && envOverride.length > 0 && envOverride !== config.apiUrl) {
    lines.push(`(Note: ${API_URL_ENV} env override is active; persisted config value is: ${config.apiUrl})`);
  }

  return {
    exitCode: 0,
    output: lines.join('\n'),
  };
}
