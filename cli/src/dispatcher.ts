/**
 * CLI dispatcher — wires subcommands to command functions via commander.
 * Does NOT call process.exit. The caller (index.ts) is responsible for that.
 */

import { Command } from 'commander';
import type { ParseOptions } from 'commander';
import { resolveHome, resolveApiUrl, readConfig } from './config/config.js';
import { createMarketplaceClient } from './api/client.js';
import { readCredentials } from './auth/credentials-store.js';
import { readActiveOrg } from './auth/active-org-store.js';
import { createAuthenticatedClient } from './auth/token-attachment.js';

import type { CommandResult } from './commands/config.js';
import { runConfigSet as defaultRunConfigSet, runConfigShow as defaultRunConfigShow } from './commands/config.js';
import { runSearch as defaultRunSearch } from './commands/search.js';
import { runInstall as defaultRunInstall } from './commands/install.js';
import { runList as defaultRunList } from './commands/list.js';
import { runUpdate as defaultRunUpdate } from './commands/update.js';
import { runRemove as defaultRunRemove } from './commands/remove.js';
import { runValidate as defaultRunValidate } from './commands/validate.js';
import { runPublish as defaultRunPublish } from './commands/publish.js';
import { runScaffold as defaultRunScaffold } from './commands/scaffold.js';
import { runLogin as defaultRunLogin } from './commands/login.js';
import { runLogout as defaultRunLogout } from './commands/logout.js';
import { runWhoami as defaultRunWhoami } from './commands/whoami.js';

import type { runInstall } from './commands/install.js';
import type { runRemove } from './commands/remove.js';
import type { runList } from './commands/list.js';
import type { runUpdate } from './commands/update.js';
import type { runSearch } from './commands/search.js';
import type { runPublish } from './commands/publish.js';
import type { runScaffold } from './commands/scaffold.js';
import type { runValidate } from './commands/validate.js';
import type { runConfigSet, runConfigShow } from './commands/config.js';
import type { ScaffoldLanguage } from './commands/scaffold.js';
import type { runLogin } from './commands/login.js';
import type { runLogout } from './commands/logout.js';
import type { runWhoami } from './commands/whoami.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface DispatcherDeps {
  runInstall?: typeof runInstall;
  runRemove?: typeof runRemove;
  runList?: typeof runList;
  runUpdate?: typeof runUpdate;
  runSearch?: typeof runSearch;
  runPublish?: typeof runPublish;
  runScaffold?: typeof runScaffold;
  runValidate?: typeof runValidate;
  runConfigSet?: typeof runConfigSet;
  runConfigShow?: typeof runConfigShow;
  runLogin?: typeof runLogin;
  runLogout?: typeof runLogout;
  runWhoami?: typeof runWhoami;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function printResult(result: CommandResult): void {
  if (result.output.length > 0) {
    process.stdout.write(result.output + '\n');
  }
}

async function getClient(env?: NodeJS.ProcessEnv): Promise<ReturnType<typeof createMarketplaceClient>> {
  const homeDir = resolveHome(env);
  const config = await readConfig(homeDir);
  const apiUrl = resolveApiUrl(config.apiUrl, env);
  return createMarketplaceClient(apiUrl);
}

/**
 * Returns an authenticated client for publish (always requires credentials).
 * Credentials are loaded from homeDir; the returned client will throw
 * SessionExpiredError when credentials are absent or locally expired.
 */
async function getAuthenticatedPublishClient(
  env?: NodeJS.ProcessEnv,
): Promise<ReturnType<typeof createAuthenticatedClient>> {
  const homeDir = resolveHome(env);
  const config = await readConfig(homeDir);
  const apiUrl = resolveApiUrl(config.apiUrl, env);
  const base = createMarketplaceClient(apiUrl);
  const credentials = await readCredentials(homeDir);
  return createAuthenticatedClient(base, { credentials });
}

/**
 * Returns an auth-aware client for install/pull commands.
 * If credentials exist the client attaches Bearer to download requests
 * (enabling private pulls). If credentials are absent the client falls
 * back to anonymous downloads — preserving the public-pull guarantee.
 */
async function getInstallClient(env?: NodeJS.ProcessEnv): Promise<ReturnType<typeof createAuthenticatedClient>> {
  const homeDir = resolveHome(env);
  const config = await readConfig(homeDir);
  const apiUrl = resolveApiUrl(config.apiUrl, env);
  const base = createMarketplaceClient(apiUrl);
  const credentials = await readCredentials(homeDir);
  return createAuthenticatedClient(base, { credentials });
}

// ---------------------------------------------------------------------------
// Program factory
// ---------------------------------------------------------------------------

export function createProgram(deps: DispatcherDeps = {}): Command {
  const program = new Command();
  program.name('claude-plugin').description('Claude Code plugin marketplace CLI').version('0.1.0').exitOverride();

  const {
    runInstall: injectedInstall = defaultRunInstall,
    runRemove: injectedRemove = defaultRunRemove,
    runList: injectedList = defaultRunList,
    runUpdate: injectedUpdate = defaultRunUpdate,
    runSearch: injectedSearch = defaultRunSearch,
    runPublish: injectedPublish = defaultRunPublish,
    runScaffold: injectedScaffold = defaultRunScaffold,
    runValidate: injectedValidate = defaultRunValidate,
    runConfigSet: injectedConfigSet = defaultRunConfigSet,
    runConfigShow: injectedConfigShow = defaultRunConfigShow,
    runLogin: injectedLogin = defaultRunLogin,
    runLogout: injectedLogout = defaultRunLogout,
    runWhoami: injectedWhoami = defaultRunWhoami,
  } = deps;

  // ── install ──────────────────────────────────────────────────────────────
  program
    .command('install <pluginName>')
    .description('Install a plugin from the marketplace')
    .option('--version <version>', 'Specific version to install')
    .action(async (pluginName: string, options: { version?: string }) => {
      const client = await getInstallClient();
      const homeDir = resolveHome();
      const result = await injectedInstall(
        { pluginName, ...(options.version !== undefined ? { version: options.version } : {}) },
        { client, homeDir },
      );
      printResult(result);
    });

  // ── remove ───────────────────────────────────────────────────────────────
  program
    .command('remove <pluginName>')
    .description('Remove an installed plugin')
    .action(async (pluginName: string) => {
      const homeDir = resolveHome();
      const result = await injectedRemove({ pluginName }, { homeDir });
      printResult(result);
    });

  // ── list ─────────────────────────────────────────────────────────────────
  program
    .command('list')
    .description('List installed plugins')
    .option('--check-updates', 'Check for available updates')
    .action(async (options: { checkUpdates?: boolean }) => {
      const client = await getClient();
      const homeDir = resolveHome();
      const result = await injectedList(
        { ...(options.checkUpdates !== undefined ? { checkUpdates: options.checkUpdates } : {}) },
        { client, homeDir },
      );
      printResult(result);
    });

  // ── update ───────────────────────────────────────────────────────────────
  program
    .command('update <pluginName>')
    .description('Update a plugin to the latest version')
    .action(async (pluginName: string) => {
      const client = await getClient();
      const homeDir = resolveHome();
      const result = await injectedUpdate({ pluginName }, { client, homeDir });
      printResult(result);
    });

  // ── search ───────────────────────────────────────────────────────────────
  program
    .command('search <query>')
    .description('Search the plugin marketplace')
    .option('--limit <limit>', 'Number of results (default: 10)', '10')
    .action(async (query: string, options: { limit?: string }) => {
      const client = await getClient();
      const homeDir = resolveHome();
      const limit = options.limit ? parseInt(options.limit, 10) : 10;
      const result = await injectedSearch({ query, limit }, { client, homeDir });
      printResult(result);
    });

  // ── publish ──────────────────────────────────────────────────────────────
  program
    .command('publish')
    .description('Publish a plugin to the marketplace')
    .option('--path <path>', 'Plugin directory (default: cwd)')
    .option('--org <orgId>', 'Override active org for private publishing')
    .action(async (options: { path?: string; org?: string }) => {
      const client = await getAuthenticatedPublishClient();
      const homeDir = resolveHome();
      // Resolve org: CLI flag takes priority over stored activeOrg
      const activeOrg = await readActiveOrg(homeDir);
      const org = options.org ?? activeOrg ?? undefined;
      const result = await injectedPublish(
        {
          ...(options.path !== undefined ? { pluginPath: options.path } : {}),
          ...(org !== undefined ? { org } : {}),
        },
        { client, homeDir },
      );
      printResult(result);
    });

  // ── scaffold ─────────────────────────────────────────────────────────────
  program
    .command('scaffold')
    .description('Scaffold a new plugin from a template')
    .option('--name <name>', 'Plugin name')
    .option('--language <language>', 'Target language: typescript|python|go|rust')
    .option('--interactive', 'Guided interactive setup')
    .option('--target-dir <dir>', 'Output directory')
    .action(async (options: { name?: string; language?: string; interactive?: boolean; targetDir?: string }) => {
      const result = await injectedScaffold(
        {
          ...(options.name !== undefined ? { name: options.name } : {}),
          ...(options.language !== undefined ? { language: options.language as ScaffoldLanguage } : {}),
          ...(options.interactive !== undefined ? { interactive: options.interactive } : {}),
          ...(options.targetDir !== undefined ? { targetDir: options.targetDir } : {}),
        },
        {},
      );
      printResult(result);
    });

  // ── validate ─────────────────────────────────────────────────────────────
  program
    .command('validate')
    .description('Validate a plugin manifest')
    .option('--path <path>', 'Plugin directory (default: cwd)')
    .action(async (options: { path?: string }) => {
      const result = await injectedValidate(
        { ...(options.path !== undefined ? { pluginPath: options.path } : {}) },
        {},
      );
      printResult(result);
    });

  // ── config ───────────────────────────────────────────────────────────────
  const configCmd = new Command('config').description('Configure CLI settings').exitOverride();

  configCmd
    .command('set <key> <value>')
    .description('Set a configuration value')
    .exitOverride()
    .action(async (key: string, value: string) => {
      const homeDir = resolveHome();
      const result = await injectedConfigSet({ key, value }, { homeDir });
      printResult(result);
    });

  configCmd
    .command('show')
    .description('Show current configuration')
    .exitOverride()
    .action(async () => {
      const homeDir = resolveHome();
      const result = await injectedConfigShow({ homeDir });
      printResult(result);
    });

  program.addCommand(configCmd);

  // ── login ─────────────────────────────────────────────────────────────────
  program
    .command('login')
    .description('Authenticate with the plugin marketplace')
    .option('--provider <provider>', 'Identity provider: google|microsoft', 'google')
    .option('--device-code', 'Use device-code flow (headless environments)')
    .action(async (options: { provider?: string; deviceCode?: boolean }) => {
      const homeDir = resolveHome();
      const config = await readConfig(homeDir);
      const apiUrl = resolveApiUrl(config.apiUrl);
      const provider = (options.provider ?? 'google') as 'google' | 'microsoft';
      const result = await injectedLogin(
        { provider, ...(options.deviceCode !== undefined ? { deviceCode: options.deviceCode } : {}) },
        { homeDir, apiUrl },
      );
      printResult(result);
    });

  // ── logout ───────────────────────────────────────────────────────────────
  program
    .command('logout')
    .description('Log out and remove stored credentials')
    .action(async () => {
      const homeDir = resolveHome();
      const result = await injectedLogout({}, { homeDir });
      printResult(result);
    });

  // ── whoami ───────────────────────────────────────────────────────────────
  program
    .command('whoami')
    .description('Show current authenticated user')
    .option('--org <orgId>', 'Override active org')
    .action(async (options: { org?: string }) => {
      const homeDir = resolveHome();
      const result = await injectedWhoami({ ...(options.org !== undefined ? { org: options.org } : {}) }, { homeDir });
      printResult(result);
    });

  // ── Normalize parseAsync to support from: 'user' with node-prefixed arrays ──
  // Tests call parseAsync(['node', 'claude-plugin', 'install', ...], { from: 'user' })
  // which is equivalent to from: 'node' semantics. Normalize here so both work.
  const originalParseAsync = program.parseAsync.bind(program);
  program.parseAsync = (argv?: readonly string[], options?: ParseOptions): Promise<Command> => {
    if (options?.from === 'user' && Array.isArray(argv) && argv[0] === 'node') {
      return originalParseAsync(argv, { ...options, from: 'node' });
    }
    return originalParseAsync(argv, options);
  };

  return program;
}
