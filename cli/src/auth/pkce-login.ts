/**
 * PKCE-based loopback OAuth login flow.
 * Binds a loopback HTTP server to 127.0.0.1, opens the browser, waits for
 * the authorization code callback, exchanges it for tokens, and stores them.
 */

import * as http from 'node:http';
import * as crypto from 'node:crypto';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PkceVerifierChallengePair {
  verifier: string;
  challenge: string;
}

export interface LoopbackServerHandle {
  port: number;
  waitForCode(): Promise<string>;
}

export interface LoopbackServerPort {
  listen(port: number, host: string): Promise<LoopbackServerHandle>;
  close(): Promise<void>;
}

export interface TokenExchangeResponse {
  access: string;
  refresh: string;
  expiresAt: string;
  user: string;
  provider: string;
}

export interface TokenExchangePort {
  exchange(code: string, verifier: string, redirectUri: string): Promise<TokenExchangeResponse>;
}

export interface PkceLoginDeps {
  generatePkce(): PkceVerifierChallengePair;
  openBrowser(url: string): Promise<void>;
  loopbackServer: LoopbackServerPort;
  tokenExchange: TokenExchangePort;
  storeCredentials(creds: TokenExchangeResponse): Promise<void>;
  timeoutMs?: number; // default 300_000 (5 min)
  clock?: { now(): number };
}

export interface PkceLoginResult {
  exitCode: number;
  output: string;
}

// ---------------------------------------------------------------------------
// generatePkceVerifierChallenge
// ---------------------------------------------------------------------------

export function generatePkceVerifierChallenge(): PkceVerifierChallengePair {
  // Generate 32 random bytes → 43-char base64url (no padding)
  const verifier = crypto.randomBytes(32).toString('base64url');
  const challenge = crypto.createHash('sha256').update(verifier).digest('base64url');
  return { verifier, challenge };
}

// ---------------------------------------------------------------------------
// createLoopbackServer
// ---------------------------------------------------------------------------

export function createLoopbackServer(): LoopbackServerPort {
  let server: http.Server | null = null;

  return {
    listen(port, host): Promise<LoopbackServerHandle> {
      return new Promise((resolve, reject) => {
        // Set up pending code promise before creating the server
        let resolveCode: ((code: string) => void) | null = null;
        let rejectCode: ((err: Error) => void) | null = null;

        const codePromise = new Promise<string>((res, rej) => {
          resolveCode = res;
          rejectCode = rej;
        });

        const srv = http.createServer((req: http.IncomingMessage, res: http.ServerResponse) => {
          const rawUrl = req.url ?? '';
          const addr = srv.address();
          const actualPort = addr && typeof addr !== 'string' ? addr.port : port;
          const parsed = new URL(rawUrl, `http://${host}:${actualPort}`);
          const code = parsed.searchParams.get('code');

          res.writeHead(200, { 'Content-Type': 'text/plain' });
          res.end('Authentication complete. You may close this window.');

          if (code && resolveCode) {
            resolveCode(code);
          } else if (rejectCode) {
            rejectCode(new Error('No code in callback'));
          }
        });
        server = srv;

        srv.on('error', reject);

        srv.listen(port, host, () => {
          const addr = srv.address();
          if (!addr || typeof addr === 'string') {
            reject(new Error('Unexpected server address type'));
            return;
          }
          const actualPort = addr.port;

          const waitForCode = (): Promise<string> => codePromise;

          resolve({ port: actualPort, waitForCode });
        });
      });
    },

    close(): Promise<void> {
      return new Promise((resolve, reject) => {
        if (!server) {
          resolve();
          return;
        }
        server.close((err) => {
          if (err) {
            reject(err);
          } else {
            resolve();
          }
        });
      });
    },
  };
}

// ---------------------------------------------------------------------------
// runPkceLogin
// ---------------------------------------------------------------------------

export async function runPkceLogin(
  provider: string,
  apiUrl: string,
  deps: PkceLoginDeps,
): Promise<PkceLoginResult> {
  const { generatePkce, openBrowser, loopbackServer, tokenExchange, storeCredentials } = deps;

  // Generate PKCE pair and state
  const { verifier, challenge } = generatePkce();
  const state = crypto.randomBytes(16).toString('base64url');

  // Start loopback server on 127.0.0.1
  const handle = await loopbackServer.listen(0, '127.0.0.1');
  const redirectUri = `http://127.0.0.1:${handle.port}/callback`;

  // Build auth URL
  const base = apiUrl.replace(/\/$/, '');
  const authUrl = new URL(`${base}/auth/authorize`);
  authUrl.searchParams.set('provider', provider);
  authUrl.searchParams.set('code_challenge', challenge);
  authUrl.searchParams.set('code_challenge_method', 'S256');
  authUrl.searchParams.set('redirect_uri', redirectUri);
  authUrl.searchParams.set('state', state);

  await openBrowser(authUrl.toString());

  try {
    const code = await handle.waitForCode();
    const tokens = await tokenExchange.exchange(code, verifier, redirectUri);
    await storeCredentials(tokens);
    return { exitCode: 0, output: `Logged in as ${tokens.user}` };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    const isTimeout =
      message.includes('Authentication cancelled or timed out') ||
      message.includes('state_mismatch') ||
      (err instanceof Object && 'code' in err && (err as { code: string }).code === 'STATE_MISMATCH');
    if (isTimeout) {
      return { exitCode: 1, output: `Authentication cancelled or timed out` };
    }
    return { exitCode: 1, output: `Login failed: ${message}` };
  } finally {
    await loopbackServer.close();
  }
}
