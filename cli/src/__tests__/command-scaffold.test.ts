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

// ---------------------------------------------------------------------------
// Characterization tests — lock the EXACT observable output of runScaffold
// per language. These must stay GREEN before and after any refactor.
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// Characterization tests — lock exact observable output per language
// These capture the current contract and must remain GREEN after any refactor.
// ---------------------------------------------------------------------------

describe('runScaffold characterization — typescript', () => {
  const PLUGIN_NAME = '@char/ts-plugin';
  const TARGET_DIR = '/projects/char-ts';
  const AUTHOR = 'testuser';

  it('writes exactly plugin.json and src/index.ts (two files, no extras)', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const writtenKeys = Object.keys(fakeFs.written).sort();
    expect(writtenKeys).toContain(`${TARGET_DIR}/plugin.json`);
    expect(writtenKeys).toContain(`${TARGET_DIR}/src/index.ts`);
    // Exactly two files — no extra scaffolded files from a richer generator
    expect(writtenKeys).toHaveLength(2);
  });

  it('plugin.json has types: ["skill"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['types']).toEqual(['skill']);
  });

  it('plugin.json has languages: ["typescript"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['languages']).toEqual(['typescript']);
  });

  it('plugin.json has entrypoints: ["src/index.ts"] (string array, not object array)', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['entrypoints']).toEqual(['src/index.ts']);
  });

  it('plugin.json has version: "0.1.0", description: "A Claude plugin"', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['version']).toBe('0.1.0');
    expect(parsed['description']).toBe('A Claude plugin');
  });

  it('src/index.ts contains the minimal entrypoint stub (comment + export default)', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const content = fakeFs.written[`${TARGET_DIR}/src/index.ts`];
    expect(content).toBe(`// ${PLUGIN_NAME} — Claude plugin entry point\nexport default {};\n`);
  });

  it('plugin.json does NOT contain a "dependencies" or "license" field (scaffold-only shape)', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(Object.keys(parsed)).not.toContain('license');
    // dependencies may or may not be present but must not be present per scaffold contract
    expect(Object.keys(parsed)).not.toContain('dependencies');
  });

  it('output message matches "Scaffolded plugin <name> (<lang>) at <dir>"', async () => {
    const fakeFs = makeCapturingFs();
    const result = await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    expect(result.output).toBe(`Scaffolded plugin ${PLUGIN_NAME} (typescript) at ${TARGET_DIR}`);
  });

  it('provides author from the non-interactive path default', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    // author is set to os.userInfo().username — just verify it is a non-empty string
    expect(typeof parsed['author']).toBe('string');
    expect((parsed['author'] as string).length).toBeGreaterThan(0);
  });

  it('passes validateManifest for typescript', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'typescript', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as PluginManifest;
    expect(validateManifest(parsed).valid).toBe(true);
    expect(validateManifest(parsed).errors).toEqual([]);
  });

  it('provides explicit author when passed via interactive prompter', async () => {
    const fakeFs = makeCapturingFs();
    const prompter = makePrompter([PLUGIN_NAME, 'typescript', 'A Claude plugin', AUTHOR]);
    await runScaffold({ interactive: true, targetDir: TARGET_DIR }, { fs: fakeFs, prompter });
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['author']).toBe(AUTHOR);
  });
});

describe('runScaffold characterization — python', () => {
  const PLUGIN_NAME = '@char/py-plugin';
  const TARGET_DIR = '/projects/char-py';

  it('entrypoint is src/main.py with python comment stub', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'python', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    expect(Object.keys(fakeFs.written)).toContain(`${TARGET_DIR}/src/main.py`);
    const content = fakeFs.written[`${TARGET_DIR}/src/main.py`];
    expect(content).toBe(`# ${PLUGIN_NAME} — Claude plugin entry point\n`);
  });

  it('plugin.json has entrypoints: ["src/main.py"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'python', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['entrypoints']).toEqual(['src/main.py']);
  });

  it('plugin.json has languages: ["python"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'python', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['languages']).toEqual(['python']);
  });

  it('writes exactly two files for python (plugin.json + src/main.py)', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'python', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    expect(Object.keys(fakeFs.written)).toHaveLength(2);
  });

  it('passes validateManifest for python', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'python', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as PluginManifest;
    expect(validateManifest(parsed).valid).toBe(true);
  });
});

describe('runScaffold characterization — go', () => {
  const PLUGIN_NAME = 'my-go-plugin';
  const TARGET_DIR = '/projects/char-go';

  it('entrypoint is src/main.go with go comment stub', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'go', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    expect(Object.keys(fakeFs.written)).toContain(`${TARGET_DIR}/src/main.go`);
    const content = fakeFs.written[`${TARGET_DIR}/src/main.go`];
    expect(content).toBe(
      `// ${PLUGIN_NAME} — Claude plugin entry point\npackage main\n\nfunc main() {}\n`,
    );
  });

  it('plugin.json has entrypoints: ["src/main.go"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'go', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['entrypoints']).toEqual(['src/main.go']);
  });

  it('plugin.json has languages: ["go"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'go', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['languages']).toEqual(['go']);
  });

  it('writes exactly two files for go (plugin.json + src/main.go)', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'go', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    expect(Object.keys(fakeFs.written)).toHaveLength(2);
  });

  it('passes validateManifest for go', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'go', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as PluginManifest;
    expect(validateManifest(parsed).valid).toBe(true);
  });
});

describe('runScaffold characterization — rust', () => {
  const PLUGIN_NAME = 'my-rust-plugin';
  const TARGET_DIR = '/projects/char-rust';

  it('entrypoint is src/main.rs with rust comment stub', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'rust', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    expect(Object.keys(fakeFs.written)).toContain(`${TARGET_DIR}/src/main.rs`);
    const content = fakeFs.written[`${TARGET_DIR}/src/main.rs`];
    expect(content).toBe(
      `// ${PLUGIN_NAME} — Claude plugin entry point\nfn main() {}\n`,
    );
  });

  it('plugin.json has entrypoints: ["src/main.rs"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'rust', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['entrypoints']).toEqual(['src/main.rs']);
  });

  it('plugin.json has languages: ["rust"]', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'rust', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    expect(parsed['languages']).toEqual(['rust']);
  });

  it('writes exactly two files for rust (plugin.json + src/main.rs)', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'rust', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    expect(Object.keys(fakeFs.written)).toHaveLength(2);
  });

  it('passes validateManifest for rust', async () => {
    const fakeFs = makeCapturingFs();
    await runScaffold(
      { name: PLUGIN_NAME, language: 'rust', targetDir: TARGET_DIR },
      { fs: fakeFs },
    );
    const raw = fakeFs.written[`${TARGET_DIR}/plugin.json`];
    const parsed = JSON.parse(raw) as PluginManifest;
    expect(validateManifest(parsed).valid).toBe(true);
  });
});
