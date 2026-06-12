/**
 * Org command — manage organization context for the CLI.
 * Subcommands: register, use, show, list, request-approval
 */

import { readActiveOrg, writeActiveOrg } from '../auth/active-org-store.js';
import type { IMarketplaceClient } from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface OrgRegisterArgs {
  name: string;
}

export interface OrgUseArgs {
  orgId: string;
}

// eslint-disable-next-line @typescript-eslint/no-empty-object-type
export interface OrgShowArgs {
  // No args needed
}

// eslint-disable-next-line @typescript-eslint/no-empty-object-type
export interface OrgListArgs {
  // No args needed
}

export interface OrgRequestApprovalArgs {
  pluginId: string;
  version?: string;
}

export interface OrgDeps {
  homeDir: string;
  client?: IMarketplaceClient;
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

/**
 * Register a new organization (stub — placeholder for future endpoint).
 * CLI: claudeforge org register <name>
 */
export async function runOrgRegister(args: OrgRegisterArgs, _deps: OrgDeps): Promise<CommandResult> {
  return {
    exitCode: 0,
    output: [
      `Organization registration requested for "${args.name}".`,
      '  Note: Registration is not yet implemented. The backend endpoint will be available in a future release.',
    ].join('\n'),
  };
}

/**
 * Set the active organization context.
 * CLI: claudeforge org use <org-id>
 */
export async function runOrgUse(args: OrgUseArgs, deps: OrgDeps): Promise<CommandResult> {
  const { orgId } = args;
  const { homeDir } = deps;

  try {
    await writeActiveOrg(homeDir, orgId);
    return {
      exitCode: 0,
      output: `Active organization set to: ${orgId}`,
    };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return { exitCode: 1, output: `Failed to set active organization: ${message}` };
  }
}

/**
 * Display the current active organization.
 * CLI: claudeforge org show
 */
export async function runOrgShow(_args: OrgShowArgs, deps: OrgDeps): Promise<CommandResult> {
  const { homeDir } = deps;

  try {
    const activeOrg = await readActiveOrg(homeDir);

    if (activeOrg) {
      return {
        exitCode: 0,
        output: `Active organization: ${activeOrg}`,
      };
    }

    return {
      exitCode: 0,
      output: 'No active organization set. Use `claudeforge org use <org-id>` to set one.',
    };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return { exitCode: 1, output: `Failed to read active organization: ${message}` };
  }
}

/**
 * List available organizations (stub — placeholder for future endpoint).
 * CLI: claudeforge org list
 */
export async function runOrgList(_args: OrgListArgs, _deps: OrgDeps): Promise<CommandResult> {
  return {
    exitCode: 0,
    output: [
      'Available organizations:',
      '  (Listing not yet implemented. The backend endpoint will be available in a future release.)',
    ].join('\n'),
  };
}

/**
 * Request approval for a plugin to be added to the org safe zone.
 * CLI: claudeforge org request-approval <plugin-id> [--version]
 */
export async function runOrgRequestApproval(args: OrgRequestApprovalArgs, deps: OrgDeps): Promise<CommandResult> {
  const { pluginId, version } = args;
  const { homeDir, client } = deps;

  if (!client) {
    return { exitCode: 1, output: 'API client is required for this command.' };
  }

  const activeOrg = await readActiveOrg(homeDir);
  if (!activeOrg) {
    return {
      exitCode: 1,
      output: ['No active organization set.', 'Set one with `claudeforge org use <org-id>` first.'].join('\n'),
    };
  }

  try {
    const result = await client.post<{ message: string; orgId: string; pluginId: string }>(
      `/api/v1/safe-zone/${encodeURIComponent(activeOrg)}/requests`,
      { pluginId, pluginVersion: version ?? 'latest' },
    );

    return {
      exitCode: 0,
      output: [
        `Approval request submitted for plugin "${pluginId}" to organization "${activeOrg}".`,
        result.message ? `  ${result.message}` : '',
        '',
        'An admin can approve it with:',
        `  claudeforge org approve ${pluginId} --org ${activeOrg}`,
      ]
        .filter(Boolean)
        .join('\n'),
    };
  } catch (err) {
    const message =
      err instanceof MarketplaceApiError
        ? (err.problemDetails.detail ?? err.message)
        : err instanceof Error
          ? err.message
          : String(err);
    return { exitCode: 1, output: `Failed to request approval: ${message}` };
  }
}
