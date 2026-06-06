/**
 * Search command — search the plugin marketplace.
 */

import type { IMarketplaceClient } from '../api/client.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface SearchArgs {
  query: string;
  limit?: number;
}

export interface SearchDeps {
  client: IMarketplaceClient;
  homeDir: string;
  env?: NodeJS.ProcessEnv;
}

// ---------------------------------------------------------------------------
// Formatting helpers
// ---------------------------------------------------------------------------

function padEnd(str: string, length: number): string {
  return str.length >= length ? str : str + ' '.repeat(length - str.length);
}

const COL_NAME = 35;
const COL_VERSION = 12;
const COL_DESC = 40;
const COL_DL = 10;

function formatTable(
  results: { name: string; latestVersion?: string | null; description: string; downloadCount: number }[],
): string {
  const header =
    padEnd('Name', COL_NAME) +
    padEnd('Version', COL_VERSION) +
    padEnd('Description', COL_DESC) +
    'Downloads';
  const separator =
    '-'.repeat(COL_NAME) +
    '-'.repeat(COL_VERSION) +
    '-'.repeat(COL_DESC) +
    '-'.repeat(COL_DL);
  const rows = results.map(
    (r) =>
      padEnd(r.name, COL_NAME) +
      padEnd(r.latestVersion ?? '-', COL_VERSION) +
      padEnd(r.description.slice(0, COL_DESC - 1), COL_DESC) +
      String(r.downloadCount),
  );
  return [header, separator, ...rows].join('\n');
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

const DEFAULT_LIMIT = 10;

export async function runSearch(
  args: SearchArgs,
  deps: SearchDeps,
): Promise<CommandResult> {
  const { query, limit = DEFAULT_LIMIT } = args;
  const { client } = deps;

  try {
    const response = await client.searchPlugins(query, limit);

    if (response.data.length === 0) {
      const output = [
        `No plugins found matching '${query}'`,
        `Tip: browse all plugins with \`claude plugin list-available\``,
      ].join('\n');
      return { exitCode: 0, output };
    }

    const table = formatTable(
      response.data.map((r) => ({
        name: r.name,
        latestVersion: null,
        description: r.description,
        downloadCount: r.downloadCount,
      })),
    );

    const output = [
      table,
      '',
      `Showing ${response.data.length} of ${response.totalCount} results.`,
      `Use --limit 20 to fetch more results.`,
    ].join('\n');

    return { exitCode: 0, output };
  } catch (err) {
    const message =
      err instanceof Error ? err.message : String(err);
    return {
      exitCode: 1,
      output: `Could not reach marketplace: ${message}\nCheck your API URL or network connection.`,
    };
  }
}
