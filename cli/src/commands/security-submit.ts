/**
 * Security submit command — submit a plugin for security analysis.
 * CLI: claudeforge security submit <plugin-id> [--version <version>]
 */

import type { IMarketplaceClient } from '../api/client.js';
import type { AnalysisSubmissionResponse } from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface SecuritySubmitArgs {
  pluginId: string;
  version: string;
}

export interface SecuritySubmitDeps {
  client: IMarketplaceClient;
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

/**
 * Submit a plugin for security analysis.
 * Sends a POST to /api/v1/plugins/submit with the plugin id and version.
 */
export async function runSubmit(args: SecuritySubmitArgs, deps: SecuritySubmitDeps): Promise<CommandResult> {
  const { pluginId, version } = args;
  const { client } = deps;

  try {
    const result = await client.post<AnalysisSubmissionResponse>('/api/v1/plugins/submit', {
      pluginId,
      version,
    });

    return {
      exitCode: 0,
      output: [
        `Plugin submitted for security analysis.`,
        `  Plugin: ${result.pluginId}`,
        `  Version: ${result.version}`,
        `  Job ID: ${result.jobId}`,
        `  Status: ${result.status}`,
        '',
        `Run \`claudeforge security status ${pluginId}\` to check analysis progress.`,
      ].join('\n'),
    };
  } catch (err) {
    if (err instanceof MarketplaceApiError) {
      switch (err.status) {
        case 409:
          return {
            exitCode: 1,
            output: `Plugin ${pluginId} v${version} has already been submitted for analysis.`,
          };
        case 401:
          return {
            exitCode: 1,
            output: "Authentication required. Please run 'claude-plugin login' first.",
          };
        case 429:
          return {
            exitCode: 1,
            output: 'Rate limit exceeded. Please wait before submitting again.',
          };
      }
    }

    const message = err instanceof Error ? err.message : String(err);
    return { exitCode: 1, output: `Submit failed: ${message}` };
  }
}
