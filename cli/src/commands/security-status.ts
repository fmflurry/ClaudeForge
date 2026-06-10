/**
 * Security status command — check analysis status and results for a plugin.
 * CLI: claudeforge security status <plugin-id> [--watch]
 */

import type { IMarketplaceClient } from '../api/client.js';
import type { AnalysisResult, AnalysisFinding } from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface SecurityStatusArgs {
  pluginId: string;
  watch: boolean;
}

export interface SecurityStatusDeps {
  client: IMarketplaceClient;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const POLL_INTERVAL_MS = 5000;
const MAX_POLL_ATTEMPTS = 100;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function formatFindings(findings: AnalysisFinding[]): string {
  if (findings.length === 0) {
    return '  No findings.';
  }

  const tableData = findings.map((f) => ({
    ID: f.id,
    Type: f.type,
    Severity: f.severity,
    Title: f.title,
  }));

  const lines: string[] = [];
  lines.push('');
  lines.push('  Findings:');
  for (const row of tableData) {
    lines.push(`    ${row.ID}  ${row.Severity.padEnd(10)} ${row.Type.padEnd(10)} ${row.Title}`);
  }
  return lines.join('\n');
}

function formatResult(result: AnalysisResult): string {
  const scoreLine =
    result.security_score !== null ? `  Security Score: ${result.security_score}` : '  Security Score: N/A';
  const statusLine = `  Status: ${result.security_status}`;

  return [scoreLine, statusLine, formatFindings(result.findings)].join('\n');
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

/**
 * Check security analysis status/results for a plugin.
 * Calls GET /api/v1/plugins/{id}/analysis.
 * When --watch is set, polls every 5s until status is complete or failed.
 */
export async function runStatus(args: SecurityStatusArgs, deps: SecurityStatusDeps): Promise<CommandResult> {
  const { pluginId, watch } = args;
  const { client } = deps;

  async function fetchAnalysis(): Promise<AnalysisResult> {
    return client.get<AnalysisResult>(`/api/v1/plugins/${encodeURIComponent(pluginId)}/analysis`);
  }

  if (watch) {
    let attempts = 0;
    while (attempts < MAX_POLL_ATTEMPTS) {
      attempts++;

      try {
        const result = await fetchAnalysis();

        if (result.security_status === 'complete' || result.security_status === 'failed') {
          return {
            exitCode: result.security_status === 'complete' ? 0 : 1,
            output: [
              `Analysis complete for ${pluginId}:`,
              formatResult(result),
            ].join('\n'),
          };
        }

        // Still pending or analyzing — wait and poll again
        process.stdout.write(`Status: ${result.security_status} (polling... attempt ${attempts})\n`);
        await sleep(POLL_INTERVAL_MS);
      } catch (err) {
        if (err instanceof MarketplaceApiError && err.status === 404) {
          return {
            exitCode: 1,
            output: `No analysis found for plugin ${pluginId}. Submit it first with \`claudeforge security submit ${pluginId}\`.`,
          };
        }
        const message = err instanceof Error ? err.message : String(err);
        return { exitCode: 1, output: `Failed to fetch analysis status: ${message}` };
      }
    }

    return {
      exitCode: 1,
      output: `Polling timeout: analysis for ${pluginId} did not complete within the expected time.`,
    };
  }

  // Single fetch
  try {
    const result = await fetchAnalysis();
    return {
      exitCode: result.security_status === 'complete' ? 0 : result.security_status === 'failed' ? 1 : 0,
      output: [`Analysis for ${pluginId}:`, formatResult(result)].join('\n'),
    };
  } catch (err) {
    if (err instanceof MarketplaceApiError && err.status === 404) {
      return {
        exitCode: 1,
        output: `No analysis found for plugin ${pluginId}. Submit it first with \`claudeforge security submit ${pluginId}\`.`,
      };
    }
    const message = err instanceof Error ? err.message : String(err);
    return { exitCode: 1, output: `Failed to fetch analysis status: ${message}` };
  }
}
