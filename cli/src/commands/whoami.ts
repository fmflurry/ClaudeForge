/**
 * Whoami command — displays current authenticated user info.
 * Calls GET /auth/me and prints email + orgs + active org.
 * Unauthenticated: message + non-zero exit.
 */

import { readCredentials } from '../auth/credentials-store.js';
import { readActiveOrg } from '../auth/active-org-store.js';
import { isTokenExpired } from '../auth/token-attachment.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface MeResponse {
  email: string;
  orgs: { id: string; name: string }[];
}

export interface WhoamiApiPort {
  getMe(token: string): Promise<MeResponse>;
}

export interface WhoamiArgs {
  org?: string;
}

export interface WhoamiDeps {
  homeDir: string;
  api?: WhoamiApiPort;
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runWhoami(_args: WhoamiArgs, deps: WhoamiDeps): Promise<CommandResult> {
  const { homeDir, api } = deps;

  const creds = await readCredentials(homeDir);

  if (!creds) {
    return {
      exitCode: 1,
      output: "Not logged in. Run 'claude-plugin login' to authenticate.",
    };
  }

  if (isTokenExpired(creds.expiresAt)) {
    return {
      exitCode: 1,
      output: "Session expired. Run 'claude-plugin login' to re-authenticate.",
    };
  }

  if (!api) {
    // Offline mode — show what we have locally
    const activeOrg = await readActiveOrg(homeDir);
    const lines = [`User: ${creds.user}`];
    if (activeOrg) {
      lines.push(`Active org: ${activeOrg}`);
    }
    return { exitCode: 0, output: lines.join('\n') };
  }

  try {
    const me = await api.getMe(creds.access);
    const activeOrg = await readActiveOrg(homeDir);

    const lines: string[] = [`User: ${me.email}`];

    if (me.orgs.length > 0) {
      lines.push(`Orgs:`);
      for (const org of me.orgs) {
        const marker = org.id === activeOrg ? ' (active)' : '';
        lines.push(`  - ${org.name} [${org.id}]${marker}`);
      }
    }

    if (activeOrg && !me.orgs.some((o) => o.id === activeOrg)) {
      lines.push(`Active org: ${activeOrg} (not in org list)`);
    }

    return { exitCode: 0, output: lines.join('\n') };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return { exitCode: 1, output: `Failed to get user info: ${message}` };
  }
}
