/**
 * List command — list locally installed plugins.
 */

import type { IMarketplaceClient } from '../api/client.js';
import { readRegistry } from '../registry/registry.js';
import type { InstalledRecord } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface ListArgs {
  checkUpdates?: boolean;
}

export interface ListDeps {
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
const COL_DATE = 22;
const COL_STATUS = 16;

type RowStatus = 'up-to-date' | 'update-available' | 'unknown';

interface TableRow {
  name: string;
  version: string;
  installedAt: string;
  status: RowStatus;
}

function formatTable(rows: TableRow[]): string {
  const header =
    padEnd('Name', COL_NAME) + padEnd('Version', COL_VERSION) + padEnd('Installed Date', COL_DATE) + 'Status';
  const separator = '-'.repeat(COL_NAME) + '-'.repeat(COL_VERSION) + '-'.repeat(COL_DATE) + '-'.repeat(COL_STATUS);
  const rowLines = rows.map(
    (r) => padEnd(r.name, COL_NAME) + padEnd(r.version, COL_VERSION) + padEnd(r.installedAt, COL_DATE) + r.status,
  );
  return [header, separator, ...rowLines].join('\n');
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runList(args: ListArgs, deps: ListDeps): Promise<CommandResult> {
  const { checkUpdates } = args;
  const { client, homeDir } = deps;

  const registry = await readRegistry(homeDir);

  if (registry.plugins.length === 0) {
    return { exitCode: 0, output: 'No plugins installed' };
  }

  const rows: TableRow[] = await Promise.all(
    registry.plugins.map(async (plugin: InstalledRecord): Promise<TableRow> => {
      let status: RowStatus = 'unknown';
      if (checkUpdates) {
        try {
          const latest = await client.getLatestVersion(plugin.name);
          status = latest.version === plugin.version ? 'up-to-date' : 'update-available';
        } catch {
          status = 'unknown';
        }
      }
      return {
        name: plugin.name,
        version: plugin.version,
        installedAt: plugin.installedAt,
        status,
      };
    }),
  );

  return {
    exitCode: 0,
    output: formatTable(rows),
  };
}
