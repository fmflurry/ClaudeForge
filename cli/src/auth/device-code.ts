/**
 * Device Code flow — headless OAuth fallback.
 * Requests a device code, displays verification instructions,
 * polls for approval, and stores tokens on success.
 */

import type { TokenExchangeResponse } from './pkce-login.js';

// Re-export so consumers of device-code.ts can get TokenExchangeResponse
export type { TokenExchangeResponse } from './pkce-login.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface DeviceCodeResponse {
  userCode: string;
  verificationUri: string;
  deviceCode: string;
  expiresIn: number; // seconds
  interval: number; // polling interval seconds
}

export type DeviceTokenPollResult =
  | { status: 'approved'; tokens: TokenExchangeResponse }
  | { status: 'pending' }
  | { status: 'slow_down' }
  | { status: 'expired' };

export interface DeviceCodeApiPort {
  requestDeviceCode(provider: string): Promise<DeviceCodeResponse>;
  pollDeviceToken(deviceCode: string, provider: string): Promise<DeviceTokenPollResult>;
}

export interface DeviceCodeDeps {
  api: DeviceCodeApiPort;
  storeCredentials(creds: TokenExchangeResponse): Promise<void>;
  displayInstructions(verificationUri: string, userCode: string): void;
  pollIntervalMs?: number; // default: interval * 1000 from server
  maxAttempts?: number; // default: 180
  clock?: { now(): number; sleep(ms: number): Promise<void> };
}

export interface DeviceCodeResult {
  exitCode: number;
  output: string;
}

// ---------------------------------------------------------------------------
// Default clock
// ---------------------------------------------------------------------------

const realClock = {
  now: () => Date.now(),
  sleep: (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)),
};

// ---------------------------------------------------------------------------
// runDeviceCodeLogin
// ---------------------------------------------------------------------------

export async function runDeviceCodeLogin(provider: string, deps: DeviceCodeDeps): Promise<DeviceCodeResult> {
  const { api, storeCredentials, displayInstructions } = deps;
  const clock = deps.clock ?? realClock;
  const maxAttempts = deps.maxAttempts ?? 180;

  // 1. Request device code
  let deviceCodeResponse: DeviceCodeResponse;
  try {
    deviceCodeResponse = await api.requestDeviceCode(provider);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return { exitCode: 1, output: `Device code request failed: ${message}` };
  }

  const { userCode, verificationUri, deviceCode, interval } = deviceCodeResponse;
  const baseIntervalMs = deps.pollIntervalMs ?? interval * 1000;

  // 2. Display instructions
  displayInstructions(verificationUri, userCode);

  const output = `Visit: ${verificationUri}\nEnter code: ${userCode}`;

  // 3. Poll
  let currentIntervalMs = baseIntervalMs;
  let attempts = 0;

  while (attempts < maxAttempts) {
    if (currentIntervalMs > 0) {
      await clock.sleep(currentIntervalMs);
    }

    let pollResult: DeviceTokenPollResult;
    try {
      pollResult = await api.pollDeviceToken(deviceCode, provider);
    } catch {
      attempts++;
      continue;
    }

    if (pollResult.status === 'approved') {
      await storeCredentials(pollResult.tokens);
      return {
        exitCode: 0,
        output: `${output}\nLogged in as ${pollResult.tokens.user}`,
      };
    }

    if (pollResult.status === 'expired') {
      return { exitCode: 1, output: `${output}\nDevice code expired. Please try again.` };
    }

    if (pollResult.status === 'slow_down') {
      // Increase interval by 5 seconds on slow_down
      currentIntervalMs = currentIntervalMs + 5000;
    }

    // 'pending' or 'slow_down' → continue polling
    attempts++;
  }

  return { exitCode: 1, output: `${output}\nAuthentication timed out after ${maxAttempts} attempts.` };
}
