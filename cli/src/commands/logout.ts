/**
 * Logout command — revokes OAuth tokens (best-effort) and deletes credentials.json.
 * Idempotent: exits 0 with "Already logged out" if no credentials file exists.
 */

import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import { CREDENTIALS_FILENAME, readCredentials } from '../auth/credentials-store.js';
import { writeActiveOrg } from '../auth/active-org-store.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface LogoutArgs {
  clearOrg?: boolean;
}

export interface LogoutApiPort {
  revokeToken?(token: string): Promise<void>;
}

export interface LogoutDeps {
  homeDir: string;
  api?: LogoutApiPort;
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runLogout(_args: LogoutArgs, deps: LogoutDeps): Promise<CommandResult> {
  const { homeDir, api } = deps;

  const creds = await readCredentials(homeDir);

  if (!creds) {
    return { exitCode: 0, output: 'Already logged out' };
  }

  // Best-effort token revocation
  if (api?.revokeToken) {
    try {
      await api.revokeToken(creds.refresh);
    } catch {
      // Best-effort — ignore revocation errors
    }
  }

  // Delete credentials.json
  const credentialsPath = path.join(homeDir, CREDENTIALS_FILENAME);
  try {
    await fs.rm(credentialsPath, { force: true });
  } catch {
    // Ignore errors on deletion
  }

  // Clear active org
  try {
    await writeActiveOrg(homeDir, null);
  } catch {
    // Non-fatal
  }

  return { exitCode: 0, output: `Logged out (${creds.user})` };
}
