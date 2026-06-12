/**
 * Migrate plugin manifests from legacy categorization to new category+keywords schema.
 *
 * Old fields removed: types, useCaseTags
 * New fields added:   category, keywords
 * Preserved fields:   languages (required in new schema)
 */

import * as nodeFsPromises from 'node:fs/promises';
import * as path from 'node:path';
import type { CommandResult, PluginManifest } from './validate.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Shape of old manifests that still carry deprecated fields. */
interface OldManifest {
  name?: string;
  version?: string;
  description?: string;
  author?: string;
  types?: string[];
  useCaseTags?: string[];
  languages?: string[];
  entrypoints?: string[];
  keywords?: string[];
  dependencies?: Record<string, string>;
  license?: string;
  docsUrl?: string;
}

export interface MigrationResult {
  pluginPath: string;
  oldManifest: OldManifest;
  newManifest: PluginManifest;
  changes: string[];
}

export interface MigrationReport {
  timestamp: string;
  plugins: MigrationResult[];
  summary: {
    total: number;
    migrated: number;
    skipped: number;
  };
}

export interface MigrationArgs {
  pluginPath?: string;
  dryRun?: boolean;
}

export interface MigrationFsPort {
  readFile(p: string): Promise<string>;
  writeFile(p: string, data: string): Promise<void>;
  readdir(p: string): Promise<string[]>;
  stat(p: string): Promise<{ isDirectory(): boolean }>;
}

export interface MigrationDeps {
  fs?: MigrationFsPort;
  cwd?: string;
}

// ---------------------------------------------------------------------------
// Mapping tables — Task 2.1, 2.2, 2.3
// ---------------------------------------------------------------------------

/** Task 2.1: useCaseTags → domain category */
const USE_CASE_TO_DOMAIN: Record<string, string> = {
  'dev-team': 'workflow-orchestration',
  'solo-dev': 'productivity-utilities',
  'code-review': 'code-intelligence',
  testing: 'testing-qa',
  deployment: 'devops-infrastructure',
  security: 'security',
  data: 'data-analytics',
  documentation: 'documentation',
  integration: 'external-service',
  language: 'language-framework',
  domain: 'domain-vertical',
};

/** Task 2.2: kind → domain category (for manifests that carried this field) */
const KIND_TO_DOMAIN: Record<string, string> = {
  SWE: 'code-intelligence',
  Engineering: 'code-intelligence',
  Product: 'productivity-utilities',
  UX: 'productivity-utilities',
  UI: 'productivity-utilities',
  DevOps: 'devops-infrastructure',
};

/** Task 2.3: types → structural keyword (goes into keywords array) */
const TYPE_TO_STRUCTURAL: Record<string, string> = {
  skill: 'skill',
  hook: 'hook',
  agent: 'subagent',
  command: 'command',
  plugin: 'mcp-server',
};

// ---------------------------------------------------------------------------
// Task 2.4: Fallback
// ---------------------------------------------------------------------------

const DEFAULT_DOMAIN_CATEGORY = 'productivity-utilities';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function normalize(raw: string): string {
  return raw
    .trim()
    .toLowerCase()
    .replace(/[\s_]+/g, '-');
}

function firstMapped(values: string[] | undefined, map: Record<string, string>): string | undefined {
  if (!values) return undefined;
  for (const v of values) {
    const mapped = map[normalize(v)];
    if (mapped) return mapped;
  }
  return undefined;
}

// ---------------------------------------------------------------------------
// Task 2.6: Idempotency check
// ---------------------------------------------------------------------------

export function isAlreadyMigrated(manifest: Record<string, unknown>): boolean {
  return 'category' in manifest && !('types' in manifest) && !('useCaseTags' in manifest);
}

// ---------------------------------------------------------------------------
// Task 2.5: Migration core
// ---------------------------------------------------------------------------

export async function migratePlugin(pluginPath: string, fsPort: MigrationFsPort): Promise<MigrationResult> {
  const manifestPath = path.join(pluginPath, 'plugin.json');
  const raw = await fsPort.readFile(manifestPath);
  const oldManifest = JSON.parse(raw) as OldManifest;
  const changes: string[] = [];

  // ── Idempotency — Task 2.6 ──────────────────────────────────────────────
  if (isAlreadyMigrated(oldManifest as Record<string, unknown>)) {
    return {
      pluginPath,
      oldManifest,
      newManifest: oldManifest as unknown as PluginManifest,
      changes: ['already migrated — skipped'],
    };
  }

  // ── Determine category ──────────────────────────────────────────────────
  let category = firstMapped(oldManifest.useCaseTags, USE_CASE_TO_DOMAIN);

  if (!category && oldManifest.types) {
    category = firstMapped(oldManifest.types, KIND_TO_DOMAIN);
  }

  if (!category) {
    category = DEFAULT_DOMAIN_CATEGORY;
    changes.push(`no mapping found — defaulting category to "${DEFAULT_DOMAIN_CATEGORY}"`);
  } else {
    changes.push(`category set to "${category}"`);
  }

  // ── Build keywords from types (Task 2.3) ───────────────────────────────
  const keywords: string[] = [];
  if (oldManifest.types) {
    for (const t of oldManifest.types) {
      const structural = TYPE_TO_STRUCTURAL[normalize(t)];
      if (structural) {
        keywords.push(structural);
      } else {
        keywords.push(normalize(t));
        changes.push(`unknown type "${t}" → kept as keyword "${normalize(t)}"`);
      }
    }
  }

  if (oldManifest.keywords) {
    for (const k of oldManifest.keywords) {
      if (!keywords.includes(k)) {
        keywords.push(k);
      }
    }
  }

  // ── Assemble new manifest ───────────────────────────────────────────────
  // keywords is always present (may be empty array).
  const base: Omit<PluginManifest, 'dependencies' | 'license' | 'docsUrl'> = {
    name: oldManifest.name ?? '',
    version: oldManifest.version ?? '0.0.0',
    description: oldManifest.description ?? '',
    author: oldManifest.author ?? '',
    category,
    languages: oldManifest.languages ?? [],
    entrypoints: oldManifest.entrypoints ?? [],
    keywords,
  };

  const newManifest = {
    ...base,
    ...(oldManifest.dependencies ? { dependencies: oldManifest.dependencies } : {}),
    ...(oldManifest.license ? { license: oldManifest.license } : {}),
    ...(oldManifest.docsUrl ? { docsUrl: oldManifest.docsUrl } : {}),
  } as PluginManifest;

  // ── Remove deprecated fields (cast through unknown to bypass index-sig restriction) ──
  const mutable = newManifest as unknown as Record<string, unknown>;
  delete mutable['types'];
  delete mutable['useCaseTags'];

  // ── Persist ─────────────────────────────────────────────────────────────
  await fsPort.writeFile(manifestPath, JSON.stringify(newManifest, null, 2) + '\n');

  return { pluginPath, oldManifest, newManifest, changes };
}

// ---------------------------------------------------------------------------
// Task 2.7: Migration report
// ---------------------------------------------------------------------------

export function generateMigrationReport(report: MigrationReport): string {
  const lines: string[] = [];

  lines.push('# Plugin Categorization Migration Report');
  lines.push('');
  lines.push(`**Generated:** ${report.timestamp}`);
  lines.push('');
  lines.push('## Summary');
  lines.push('');
  lines.push(`| Metric | Count |`);
  lines.push(`|--------|-------|`);
  lines.push(`| Total plugins scanned | ${report.summary.total} |`);
  lines.push(`| Migrated | ${report.summary.migrated} |`);
  lines.push(`| Skipped (already migrated) | ${report.summary.skipped} |`);
  lines.push('');

  // ── Detail per plugin ───────────────────────────────────────────────────
  const migrated = report.plugins.filter((p) => !p.changes.includes('already migrated — skipped'));
  const skipped = report.plugins.filter((p) => p.changes.includes('already migrated — skipped'));

  if (migrated.length > 0) {
    lines.push('## Migrated Plugins');
    lines.push('');
    for (const p of migrated) {
      lines.push(`### ${p.newManifest.name}`);
      lines.push('');
      lines.push(`**Path:** \`${p.pluginPath}\``);
      lines.push('');
      lines.push('| Field | Old | New |');
      lines.push('|-------|-----|-----|');
      lines.push(`| category | — | ${p.newManifest.category} |`);
      lines.push(
        `| keywords | ${JSON.stringify((p.oldManifest as Record<string, unknown>)['types'] ?? [])} | ${JSON.stringify(p.newManifest.keywords ?? [])} |`,
      );
      lines.push(`| useCaseTags | ${JSON.stringify(p.oldManifest.useCaseTags ?? [])} | removed |`);
      lines.push(`| types | ${JSON.stringify(p.oldManifest.types ?? [])} | removed |`);
      lines.push('');
      if (p.changes.length > 0) {
        lines.push('**Changes:**');
        for (const c of p.changes) {
          lines.push(`- ${c}`);
        }
        lines.push('');
      }
    }
  }

  if (skipped.length > 0) {
    lines.push('## Skipped Plugins (already migrated)');
    lines.push('');
    for (const p of skipped) {
      lines.push(`- \`${p.newManifest.name}\` at \`${p.pluginPath}\``);
    }
    lines.push('');
  }

  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// Default real FS
// ---------------------------------------------------------------------------

const realMigrationFsPort: MigrationFsPort = {
  readFile(p) {
    return nodeFsPromises.readFile(p, 'utf-8');
  },
  writeFile(p, data) {
    return nodeFsPromises.writeFile(p, data, 'utf-8');
  },
  readdir(p) {
    return nodeFsPromises.readdir(p);
  },
  stat(p) {
    return nodeFsPromises.stat(p);
  },
};

// ---------------------------------------------------------------------------
// CLI entry point
// ---------------------------------------------------------------------------

export async function runMigration(args: MigrationArgs, deps: MigrationDeps): Promise<CommandResult> {
  const { pluginPath: rawPluginPath, dryRun = false } = args;
  const { fs: fsPort = realMigrationFsPort, cwd = process.cwd() } = deps;

  // ── Resolve plugin directory ────────────────────────────────────────────
  const targetDir = rawPluginPath ? path.resolve(cwd, rawPluginPath) : cwd;
  let entries: string[];

  try {
    entries = await fsPort.readdir(targetDir);
  } catch {
    return { exitCode: 2, output: `Cannot read directory: ${targetDir}` };
  }

  const pluginDirs: string[] = [];
  for (const entry of entries) {
    const entryPath = path.join(targetDir, entry);
    try {
      const stat = await fsPort.stat(entryPath);
      if (stat.isDirectory()) {
        try {
          await fsPort.readFile(path.join(entryPath, 'plugin.json'));
          pluginDirs.push(entryPath);
        } catch {
          // No plugin.json — skip
        }
      }
    } catch {
      // stat failed — skip
    }
  }

  if (pluginDirs.length === 0) {
    return { exitCode: 0, output: 'No plugins found to migrate.' };
  }

  // ── Run migration per plugin ────────────────────────────────────────────
  const results: MigrationResult[] = [];

  for (const dir of pluginDirs) {
    try {
      const result = await migratePlugin(dir, fsPort);
      results.push(result);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      results.push({
        pluginPath: dir,
        oldManifest: {},
        newManifest: {} as PluginManifest,
        changes: [`ERROR: ${message}`],
      });
    }
  }

  // ── Build report ────────────────────────────────────────────────────────
  const migrated = results.filter((r) => !r.changes.includes('already migrated — skipped'));
  const skipped = results.filter((r) => r.changes.includes('already migrated — skipped'));
  const errors = results.filter((r) => r.changes.some((c) => c.startsWith('ERROR:')));

  const report: MigrationReport = {
    timestamp: new Date().toISOString(),
    plugins: results,
    summary: {
      total: results.length,
      migrated: migrated.length,
      skipped: skipped.length,
    },
  };

  // ── Persist report file ─────────────────────────────────────────────────
  const reportPath = path.join(targetDir, 'migration-report.md');
  const reportContent = generateMigrationReport(report);

  if (!dryRun) {
    await fsPort.writeFile(reportPath, reportContent);
  }

  // ── Output ──────────────────────────────────────────────────────────────
  const outputLines: string[] = [];
  outputLines.push(`Scanned ${report.summary.total} plugin(s).`);
  outputLines.push(`Migrated: ${report.summary.migrated}  Skipped: ${report.summary.skipped}`);
  if (errors.length > 0) {
    outputLines.push(`Errors: ${errors.length}`);
    for (const e of errors) {
      outputLines.push(`  ${e.pluginPath}: ${e.changes.join('; ')}`);
    }
  }
  if (!dryRun) {
    outputLines.push(`Report written to: ${reportPath}`);
  } else {
    outputLines.push('(dry run — no files written)');
  }

  const hasErrors = errors.length > 0;
  return {
    exitCode: hasErrors ? 1 : 0,
    output: outputLines.join('\n'),
  };
}
