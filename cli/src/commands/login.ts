/**
 * Login command — authenticates the user via PKCE loopback (primary)
 * or device-code flow (headless fallback).
 */

import * as childProcess from 'node:child_process';
import * as os from 'node:os';
import { generatePkceVerifierChallenge, runPkceLogin, createLoopbackServer } from '../auth/pkce-login.js';
import { runDeviceCodeLogin } from '../auth/device-code.js';
import { writeCredentials, readCredentials } from '../auth/credentials-store.js';
import { readActiveOrg } from '../auth/active-org-store.js';
import type { TokenExchangeResponse } from '../auth/pkce-login.js';
import type { Credentials } from '../auth/credentials-store.js';
import type { DeviceCodeDeps, DeviceCodeApiPort } from '../auth/device-code.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface LoginArgs {
  provider?: 'google' | 'microsoft';
  deviceCode?: boolean;
}

export interface LoginApiPort {
  exchangeToken(code: string, verifier: string, redirectUri: string): Promise<TokenExchangeResponse>;
  requestDeviceCode: DeviceCodeApiPort['requestDeviceCode'];
  pollDeviceToken: DeviceCodeApiPort['pollDeviceToken'];
  getActiveOrg?(token: string): Promise<string | null>;
}

export interface LoginDeps {
  homeDir: string;
  apiUrl: string;
  api?: LoginApiPort;
  openBrowser?: (url: string) => Promise<void>;
  deviceCodeMode?: boolean;
}

// ---------------------------------------------------------------------------
// Default browser opener
// ---------------------------------------------------------------------------

async function defaultOpenBrowser(url: string): Promise<void> {
  const platform = os.platform();
  const cmd = platform === 'darwin' ? 'open' : platform === 'win32' ? 'start' : 'xdg-open';
  return new Promise<void>((resolve) => {
    childProcess.exec(`${cmd} "${url}"`, () => {
      // Fire and forget — errors opening the browser are non-fatal
    });
    // Resolve immediately; browser opening is async
    resolve();
  });
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runLogin(args: LoginArgs, deps: LoginDeps): Promise<CommandResult> {
  const { homeDir, apiUrl } = deps;
  const provider = args.provider ?? 'google';
  const openBrowser = deps.openBrowser ?? defaultOpenBrowser;
  const useDeviceCode = args.deviceCode ?? deps.deviceCodeMode ?? false;

  const storeCredentials = async (tokens: TokenExchangeResponse): Promise<void> => {
    const creds: Credentials = {
      access: tokens.access,
      refresh: tokens.refresh,
      expiresAt: tokens.expiresAt,
      user: tokens.user,
      provider: tokens.provider as 'google' | 'microsoft',
    };
    await writeCredentials(homeDir, creds);
  };

  if (useDeviceCode) {
    // Device-code fallback
    const deviceDeps: DeviceCodeDeps = {
      api: deps.api ?? buildNoopDeviceCodeApi(),
      storeCredentials,
      displayInstructions: (verificationUri: string, userCode: string) => {
        process.stdout.write(`\nOpen: ${verificationUri}\nEnter code: ${userCode}\n`);
      },
    };
    return runDeviceCodeLogin(provider, deviceDeps);
  }

  // PKCE loopback primary
  const result = await runPkceLogin(provider, apiUrl, {
    generatePkce: generatePkceVerifierChallenge,
    openBrowser,
    loopbackServer: createLoopbackServer(),
    tokenExchange: {
      exchange: deps.api?.exchangeToken
        ? (code, verifier, redirectUri) => deps.api!.exchangeToken(code, verifier, redirectUri)
        : buildNoopTokenExchange(),
    },
    storeCredentials,
  });

  if (result.exitCode !== 0) {
    return result;
  }

  // Read back stored creds to get user email for output
  const stored = await readCredentials(homeDir);
  const activeOrg = await readActiveOrg(homeDir);
  const userLine = stored ? `Logged in as ${stored.user}` : result.output;
  const orgLine = activeOrg ? `Active org: ${activeOrg}` : '';

  return {
    exitCode: 0,
    output: [userLine, orgLine].filter(Boolean).join('\n'),
  };
}

// ---------------------------------------------------------------------------
// Noop fallbacks (used when no API port is injected in tests / non-wired contexts)
// ---------------------------------------------------------------------------

function buildNoopTokenExchange(): (
  code: string,
  verifier: string,
  redirectUri: string,
) => Promise<TokenExchangeResponse> {
  return async () => {
    throw new Error('Token exchange API not configured');
  };
}

function buildNoopDeviceCodeApi(): DeviceCodeApiPort {
  return {
    requestDeviceCode: async () => {
      throw new Error('Device code API not configured');
    },
    pollDeviceToken: async () => ({ status: 'expired' as const }),
  };
}
