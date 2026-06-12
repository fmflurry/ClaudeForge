/**
 * Security appeal command — file an appeal on a security analysis finding.
 * CLI: claudeforge security appeal <plugin-id> --reason "..." --finding <finding-id>
 */

import type { IMarketplaceClient } from '../api/client.js';
import type { AppealSubmissionResponse } from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface SecurityAppealArgs {
  pluginId: string;
  reason: string;
  findingId: string;
}

export interface SecurityAppealDeps {
  client: IMarketplaceClient;
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

/**
 * File an appeal on a security analysis finding.
 * Sends a POST to /api/v1/plugins/{id}/appeal with the reason and finding ID.
 */
export async function runAppeal(args: SecurityAppealArgs, deps: SecurityAppealDeps): Promise<CommandResult> {
  const { pluginId, reason, findingId } = args;
  const { client } = deps;

  try {
    const result = await client.post<AppealSubmissionResponse>(
      `/api/v1/plugins/${encodeURIComponent(pluginId)}/appeal`,
      {
        reason,
        findingId,
      },
    );

    return {
      exitCode: 0,
      output: [
        `Appeal filed for plugin ${pluginId}.`,
        `  Appeal ID: ${result.appealId}`,
        `  Status: ${result.status}`,
        '',
        'The appeal has been submitted for review.',
      ].join('\n'),
    };
  } catch (err) {
    if (err instanceof MarketplaceApiError) {
      switch (err.status) {
        case 401:
          return {
            exitCode: 1,
            output: "Authentication required. Please run 'claude-plugin login' first.",
          };
        case 404:
          return {
            exitCode: 1,
            output: `Plugin ${pluginId} not found or no analysis results available.`,
          };
        case 409:
          return {
            exitCode: 1,
            output: `An appeal for finding ${findingId} on plugin ${pluginId} has already been submitted.`,
          };
        case 429:
          return {
            exitCode: 1,
            output: 'Rate limit exceeded. Please wait before submitting again.',
          };
      }
    }

    const message = err instanceof Error ? err.message : String(err);
    return { exitCode: 1, output: `Appeal failed: ${message}` };
  }
}
