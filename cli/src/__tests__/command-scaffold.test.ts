/**
 * Tests for src/commands/scaffold.ts
 *
 * Production module path: src/commands/scaffold.ts
 * Exported functions:
 *   - runScaffold(args: ScaffoldArgs, deps: ScaffoldDeps): Promise<CommandResult>
 *       args: {
 *         name?: string;
 *         language?: ScaffoldLanguage;
 *         interactive?: boolean;
 *         targetDir?: string;
 *       }
 *       deps: {
 *         fs?: ScaffoldFsPort;
 *         cwd?: string;
 *         prompter?: Prompter;   — injectable for --interactive mode
 *       }
 *   - ScaffoldLanguage = 'typescript' | 'python' | 'go' | 'rust'
 *   - ScaffoldFsPort: {
 *       mkdir(dir: string): Promise<void>;
 *       writeFile(p: string, content: string): Promise<void>;
 *       exists(p: string): Promise<boolean>;
 *       basename(p: string): string;
 *     }
 *   - Prompter: {
 *       ask(question: string, defaultValue?: string): Promise<string>;
 *     }
 *   - CommandResult: { exitCode: number; output: string }
 *
 * NOTE: Group 19 provides full language templates. Group 18 only requires:
 *   - plugin.json is generated with correct canonical fields
 *   - required args/flags are handled (--name, --language, infer-from-dir)
 *   - --interactive invokes Prompter in the right order
 *   - generated plugin.json passes validateManifest
 *   - at minimum a src/ subdirectory and plugin.json are created
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/commands/scaffold.ts is created (RED state).
import { runScaffold } from '../commands/scaffold.js';
import type { CommandResult, ScaffoldFsPort, ScaffoldLanguage, Prompter } from '../commands/scaffold.js';
import { validateManifest } from '../commands/validate.js';
import type { PluginManifest } from '../commands/validate.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type WrittenFiles = Record<string, string>;

function makeCapturingFs(existingFiles: string[] = []): ScaffoldFsPort & { written: WrittenFiles } {
  const written: WrittenFiles = {};
  return {
    written,
    mkdir: vi.fn().mockResolvedValue(undefined),
    writeFile: vi.fn(async (p: string, content: string) => {
      written[p] = content;
    }),
    exists: vi.fn(async (p: string) => existingFiles.includes(p)),
    basename: (p: string) => p.split('/').pop() ?? p,
  };
}

function makePrompter(answers: string[]): Prompter {
  let idx = 0;
  return {
    ask: vi.fn(async () => {
      const answer = answers[idx] ?? '';
      idx++;
      return answer;
    }),
  };
}

// ---------------------------------------------------------------------------
// runScaffold – name and language from args
// ---------------------------------------------------------------------------

describe('runScaffold – explicit --name and --language', () => {
  it('returns exitCode 0', async () => {
    const fakeFs = makeCapturingFs();
    const result: CommandResult = await runScaffold(
      { name: '@test/my-plugin', language: 'typescript', targetDir: '/tmp/output' },
      { fs: fakeFs },
    );
    expect(result.exitCode).toBe(0);
  });

  it('creates plugin.json in the target directory', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold({ name: '@test/my-plugin', language: 'typescript', targetDir: '/tmp/output' }, { fs: fakeFs });
    const manifestPath = Object.keys(fakeFs.written).find((k) => k.endsWith('plugin.json'));
    expect(manifestPath).toBeDefined();
  });

  it('generated plugin.json passes validateManifest', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold({ name: '@test/my-plugin', language: 'typescript', targetDir: '/tmp/output' }, { fs: fakeFs });
    const manifestPath = Object.keys(fakeFs.written).find((k) => k.endsWith('plugin.json'));
    if (!manifestPath) throw new Error('plugin.json not written');
    const parsed = JSON.parse(fakeFs.written[manifestPath]) as PluginManifest;
    const validation = validateManifest(parsed);
    expect(validation.valid).toBe(true);
  });

  it('generated plugin.json includes the plugin name', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold({ name: '@test/my-plugin', language: 'typescript', targetDir: '/tmp/output' }, { fs: fakeFs });
    const manifestPath = Object.keys(fakeFs.written).find((k) => k.endsWith('plugin.json'));
    if (!manifestPath) throw new Error('plugin.json not written');
    const parsed = JSON.parse(fakeFs.written[manifestPath]) as { name: string };
    expect(parsed.name).toBe('@test/my-plugin');
  });

  it('generated plugin.json includes the correct language', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold({ name: '@test/my-plugin', language: 'python', targetDir: '/tmp/output' }, { fs: fakeFs });
    const manifestPath = Object.keys(fakeFs.written).find((k) => k.endsWith('plugin.json'));
    if (!manifestPath) throw new Error('plugin.json not written');
    const parsed = JSON.parse(fakeFs.written[manifestPath]) as { languages: string[] };
    expect(parsed.languages).toContain('python');
  });

  it('creates at minimum a src/ subdirectory', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold({ name: '@test/my-plugin', language: 'typescript', targetDir: '/tmp/output' }, { fs: fakeFs });
    const mkdirCalls = (fakeFs.mkdir as ReturnType<typeof vi.fn>).mock.calls as [string][];
    const hasSrc = mkdirCalls.some(([d]) => d.includes('src'));
    expect(hasSrc).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// runScaffold – infer name from directory
// ---------------------------------------------------------------------------

describe('runScaffold – name inferred from directory', () => {
  it('uses the basename of targetDir as the plugin name when --name is omitted', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold({ language: 'typescript', targetDir: '/projects/my-awesome-plugin' }, { fs: fakeFs });
    const manifestPath = Object.keys(fakeFs.written).find((k) => k.endsWith('plugin.json'));
    if (!manifestPath) throw new Error('plugin.json not written');
    const parsed = JSON.parse(fakeFs.written[manifestPath]) as { name: string };
    expect(parsed.name).toContain('my-awesome-plugin');
  });
});

// ---------------------------------------------------------------------------
// runScaffold – valid language options
// ---------------------------------------------------------------------------

describe('runScaffold – language options', () => {
  const languages: ScaffoldLanguage[] = ['typescript', 'python', 'go', 'rust'];

  for (const lang of languages) {
    it(`scaffolds successfully for language=${lang}`, async () => {
      const fakeFs = makeCapturingFs();
      const result = await runScaffold(
        { name: `@test/plugin-${lang}`, language: lang, targetDir: '/tmp/output' },
        { fs: fakeFs },
      );
      expect(result.exitCode).toBe(0);
    });
  }

  it('returns non-zero exitCode for unknown language', async () => {
    const fakeFs = makeCapturingFs();
    const result = await runScaffold(
      { name: '@test/plugin', language: 'cobol' as ScaffoldLanguage, targetDir: '/tmp/output' },
      { fs: fakeFs },
    );
    expect(result.exitCode).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// runScaffold – --interactive mode
// ---------------------------------------------------------------------------

describe('runScaffold – --interactive', () => {
  it('calls prompter.ask for the plugin name', async () => {
    const fakeFs = makeCapturingFs();
    const prompter = makePrompter(['@test/interactive-plugin', 'typescript', 'An interactive plugin', 'author']);
    await runScaffold({ interactive: true, targetDir: '/tmp/output' }, { fs: fakeFs, prompter });
    const askMock = prompter.ask as ReturnType<typeof vi.fn>;
    const questions = askMock.mock.calls.map(([q]: [string]) => q.toLowerCase()) as string[];
    expect(questions.some((q) => q.includes('name'))).toBe(true);
  });

  it('calls prompter.ask for the language', async () => {
    const fakeFs = makeCapturingFs();
    const prompter = makePrompter(['@test/interactive-plugin', 'typescript', 'A plugin', 'author']);
    await runScaffold({ interactive: true, targetDir: '/tmp/output' }, { fs: fakeFs, prompter });
    const askMock = prompter.ask as ReturnType<typeof vi.fn>;
    const questions = askMock.mock.calls.map(([q]: [string]) => q.toLowerCase()) as string[];
    expect(questions.some((q) => q.includes('language'))).toBe(true);
  });

  it('generates a valid plugin.json from interactive answers', async () => {
    const fakeFs = makeCapturingFs();
    const prompter = makePrompter(['@test/interactive-plugin', 'typescript', 'A test plugin', 'Test Author']);
    await runScaffold({ interactive: true, targetDir: '/tmp/output' }, { fs: fakeFs, prompter });
    const manifestPath = Object.keys(fakeFs.written).find((k) => k.endsWith('plugin.json'));
    if (!manifestPath) throw new Error('plugin.json not written');
    const parsed = JSON.parse(fakeFs.written[manifestPath]) as PluginManifest;
    const validation = validateManifest(parsed);
    expect(validation.valid).toBe(true);
  });
});
