/**
 * Tests for src/commands/migrate-categorization.ts
 *
 * Tests migration logic from legacy categorization to new category+keywords schema.
 * Covers: known tag mapping, ambiguous tag fallback, missing tags default,
 * idempotent re-run, type → structural keyword conversion.
 */

import { describe, it, expect, vi } from 'vitest';
import {
  migratePlugin,
  isAlreadyMigrated,
  generateMigrationReport,
  runMigration,
} from '../commands/migrate-categorization.js';
import type {
  MigrationResult,
  MigrationReport,
  MigrationFsPort,
} from '../commands/migrate-categorization.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeFakeFs(files: Record<string, string>): MigrationFsPort {
  const written: Record<string, string> = {};
  return {
    readFile: vi.fn(async (p: string) => {
      if (p in files) return files[p];
      throw Object.assign(new Error('ENOENT'), { code: 'ENOENT' });
    }),
    writeFile: vi.fn(async (p: string, data: string) => {
      written[p] = data;
    }),
    readdir: vi.fn(async (p: string) => {
      // Return directory names derived from keys
      const dirs = new Set<string>();
      for (const key of Object.keys(files)) {
        const relative = key.replace(p + '/', '');
        const parts = relative.split('/');
        if (parts.length > 1) {
          dirs.add(parts[0]);
        }
      }
      return [...dirs];
    }),
    stat: vi.fn(async (p: string) => ({
      isDirectory: () => {
        // Check if any file starts with this path
        return Object.keys(files).some((k) => k.startsWith(p + '/'));
      },
    })),
  };
}

// ---------------------------------------------------------------------------
// isAlreadyMigrated — Task 2.6
// ---------------------------------------------------------------------------

describe('isAlreadyMigrated', () => {
  it('returns true when category present and no deprecated fields', () => {
    expect(
      isAlreadyMigrated({
        name: 'test',
        category: 'code-intelligence',
        keywords: ['skill'],
      }),
    ).toBe(true);
  });

  it('returns false when types field present', () => {
    expect(
      isAlreadyMigrated({
        name: 'test',
        category: 'code-intelligence',
        types: ['skill'],
      }),
    ).toBe(false);
  });

  it('returns false when useCaseTags field present', () => {
    expect(
      isAlreadyMigrated({
        name: 'test',
        category: 'code-intelligence',
        useCaseTags: ['dev-team'],
      }),
    ).toBe(false);
  });

  it('returns false when category missing', () => {
    expect(
      isAlreadyMigrated({
        name: 'test',
        types: ['skill'],
      }),
    ).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// migratePlugin — Task 2.5
// ---------------------------------------------------------------------------

describe('migratePlugin – known tag mapping', () => {
  it('maps useCaseTag "dev-team" to workflow-orchestration', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['dev-team'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.category).toBe('workflow-orchestration');
    expect(result.changes).toContain('category set to "workflow-orchestration"');
  });

  it('maps useCaseTag "testing" to testing-qa', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['testing'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.category).toBe('testing-qa');
  });

  it('maps useCaseTag "code-review" to code-intelligence', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['code-review'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.category).toBe('code-intelligence');
  });

  it('maps useCaseTag "security" to security', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['security'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.category).toBe('security');
  });

  it('maps useCaseTag "deployment" to devops-infrastructure', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['deployment'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.category).toBe('devops-infrastructure');
  });
});

describe('migratePlugin – ambiguous tag fallback', () => {
  it('defaults to productivity-utilities when useCaseTags has no mapping', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['unknown-tag'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.category).toBe('productivity-utilities');
    expect(result.changes.some((c) => c.includes('defaulting'))).toBe(true);
  });

  it('uses first mapped useCaseTag when multiple provided', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['testing', 'dev-team'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    // "testing" maps to testing-qa, should be used first
    expect(result.newManifest.category).toBe('testing-qa');
  });
});

describe('migratePlugin – missing tags default', () => {
  it('defaults category when no useCaseTags or types mapping', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.category).toBe('productivity-utilities');
  });

  it('defaults category when types has no domain mapping', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    // types=['skill'] → TYPE_TO_STRUCTURAL maps to keyword 'skill'
    // No domain mapping from types (KIND_TO_DOMAIN has no 'skill' key)
    expect(result.newManifest.category).toBe('productivity-utilities');
  });
});

describe('migratePlugin – type → structural keyword', () => {
  it('converts "skill" type to "skill" keyword', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.keywords).toContain('skill');
    expect(result.newManifest.types).toBeUndefined();
  });

  it('converts "agent" type to "subagent" keyword', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['agent'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.keywords).toContain('subagent');
    expect(result.newManifest.types).toBeUndefined();
  });

  it('converts "plugin" type to "mcp-server" keyword', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['plugin'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.keywords).toContain('mcp-server');
  });

  it('preserves existing keywords alongside converted types', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      keywords: ['typescript'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.keywords).toContain('skill');
    expect(result.newManifest.keywords).toContain('typescript');
  });

  it('removes useCaseTags from output', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['dev-team'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect((result.newManifest as Record<string, unknown>)['useCaseTags']).toBeUndefined();
  });
});

describe('migratePlugin – idempotent re-run', () => {
  it('skips migration when already migrated', async () => {
    const migratedManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      category: 'code-intelligence',
      keywords: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(migratedManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.changes).toContain('already migrated — skipped');
    expect(result.newManifest.category).toBe('code-intelligence');
  });

  it('does not write file when already migrated', async () => {
    const migratedManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      category: 'code-intelligence',
      keywords: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(migratedManifest) });
    await migratePlugin('/plugin', fs);

    expect(fs.writeFile).not.toHaveBeenCalled();
  });
});

describe('migratePlugin – preserves optional fields', () => {
  it('preserves dependencies', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
      dependencies: { 'some-dep': '>=1.0.0' },
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.dependencies).toEqual({ 'some-dep': '>=1.0.0' });
  });

  it('preserves license', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
      license: 'MIT',
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.license).toBe('MIT');
  });

  it('preserves docsUrl', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
      docsUrl: 'https://docs.example.com',
    };
    const fs = makeFakeFs({ '/plugin/plugin.json': JSON.stringify(oldManifest) });
    const result = await migratePlugin('/plugin', fs);

    expect(result.newManifest.docsUrl).toBe('https://docs.example.com');
  });
});

// ---------------------------------------------------------------------------
// generateMigrationReport — Task 2.7
// ---------------------------------------------------------------------------

describe('generateMigrationReport', () => {
  it('generates markdown report with summary table', () => {
    const report: MigrationReport = {
      timestamp: '2024-01-01T00:00:00.000Z',
      plugins: [],
      summary: { total: 0, migrated: 0, skipped: 0 },
    };
    const output = generateMigrationReport(report);

    expect(output).toContain('# Plugin Categorization Migration Report');
    expect(output).toContain('2024-01-01T00:00:00.000Z');
    expect(output).toContain('| Total plugins scanned | 0 |');
  });

  it('lists migrated plugins', () => {
    const report: MigrationReport = {
      timestamp: '2024-01-01T00:00:00.000Z',
      plugins: [
        {
          pluginPath: '/plugin-a',
          oldManifest: { types: ['skill'], useCaseTags: ['dev-team'] },
          newManifest: {
            name: 'plugin-a',
            version: '1.0.0',
            description: 'A',
            author: 'Author',
            category: 'workflow-orchestration',
            languages: ['ts'],
            entrypoints: ['index.ts'],
            keywords: ['skill'],
          },
          changes: ['category set to "workflow-orchestration"'],
        },
      ],
      summary: { total: 1, migrated: 1, skipped: 0 },
    };
    const output = generateMigrationReport(report);

    expect(output).toContain('## Migrated Plugins');
    expect(output).toContain('plugin-a');
    expect(output).toContain('workflow-orchestration');
  });

  it('lists skipped plugins', () => {
    const report: MigrationReport = {
      timestamp: '2024-01-01T00:00:00.000Z',
      plugins: [
        {
          pluginPath: '/plugin-b',
          oldManifest: {},
          newManifest: {
            name: 'plugin-b',
            version: '1.0.0',
            description: 'B',
            author: 'Author',
            category: 'code-intelligence',
            languages: ['ts'],
            entrypoints: ['index.ts'],
            keywords: [],
          },
          changes: ['already migrated — skipped'],
        },
      ],
      summary: { total: 1, migrated: 0, skipped: 1 },
    };
    const output = generateMigrationReport(report);

    expect(output).toContain('## Skipped Plugins (already migrated)');
    expect(output).toContain('plugin-b');
  });
});

// ---------------------------------------------------------------------------
// runMigration — CLI entry point
// ---------------------------------------------------------------------------

describe('runMigration', () => {
  it('returns "No plugins found" when directory has no plugin.json files', async () => {
    const fs = makeFakeFs({});
    fs.readdir = vi.fn(async () => ['empty-dir']);
    fs.stat = vi.fn(async () => ({ isDirectory: () => true }));
    const result = await runMigration({ pluginPath: '/plugins' }, { fs, cwd: '/' });

    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('No plugins found');
  });

  it('migrates plugin in directory', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      useCaseTags: ['dev-team'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const files: Record<string, string> = {
      '/plugins/my-plugin/plugin.json': JSON.stringify(oldManifest),
    };
    const fs = makeFakeFs(files);
    fs.readdir = vi.fn(async () => ['my-plugin']);
    fs.stat = vi.fn(async (p: string) => ({
      isDirectory: () => p.endsWith('my-plugin'),
    }));

    const result = await runMigration({ pluginPath: '/plugins' }, { fs, cwd: '/' });

    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('Migrated: 1');
  });

  it('dry run does not write report file', async () => {
    const oldManifest = {
      name: 'test-plugin',
      version: '1.0.0',
      description: 'Test',
      author: 'Tester',
      types: ['skill'],
      languages: ['typescript'],
      entrypoints: ['index.ts'],
    };
    const files: Record<string, string> = {
      '/plugins/my-plugin/plugin.json': JSON.stringify(oldManifest),
    };
    const fs = makeFakeFs(files);
    fs.readdir = vi.fn(async () => ['my-plugin']);
    fs.stat = vi.fn(async (p: string) => ({
      isDirectory: () => p.endsWith('my-plugin'),
    }));

    const result = await runMigration({ pluginPath: '/plugins', dryRun: true }, { fs, cwd: '/' });

    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('dry run');
    // dryRun prevents report file writing; plugin migration still runs
    const writeFileCalls = (fs.writeFile as ReturnType<typeof vi.fn>).mock.calls;
    const reportWrite = writeFileCalls.find((call: string[]) => call[0]?.includes('migration-report'));
    expect(reportWrite).toBeUndefined();
  });
});
