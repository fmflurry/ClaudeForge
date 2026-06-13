/**
 * Scaffold command — generate a minimal plugin project structure.
 *
 * Design note (task 11.2): This command intentionally does NOT delegate to
 * `generatePluginTemplate` from `@claudeforge/plugin-template`. The generator
 * produces a richer, incompatible output (object-array entrypoints, extra fields
 * like `dependencies`/`license`, many extra files, and different source file
 * paths for python/go/rust). Delegating while preserving exact observable output
 * would require post-processing that is more complex than the current inline
 * implementation and would couple the scaffold contract to generator internals.
 *
 * Characterization tests in `command-scaffold.test.ts` lock the exact per-language
 * output (file set, plugin.json shape, entrypoint content) as the stable contract.
 *
 * The addon scaffold path (`cli/src/addon/scaffold-source.ts`) is the primary
 * consumer of `generatePluginTemplate` — it handles path remapping and manifest
 * synthesis for the addon model where the richer generator output is appropriate.
 */

import * as nodeFsPromises from 'node:fs/promises';
import * as path from 'node:path';
import * as nodeOs from 'node:os';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type ScaffoldLanguage = 'typescript' | 'python' | 'go' | 'rust';

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface ScaffoldFsPort {
  mkdir(dir: string): Promise<void>;
  writeFile(p: string, content: string): Promise<void>;
  exists(p: string): Promise<boolean>;
  basename(p: string): string;
}

export interface Prompter {
  ask(question: string, defaultValue?: string): Promise<string>;
}

export interface ScaffoldArgs {
  name?: string;
  language?: ScaffoldLanguage;
  interactive?: boolean;
  targetDir?: string;
}

export interface ScaffoldDeps {
  fs?: ScaffoldFsPort;
  cwd?: string;
  prompter?: Prompter;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const VALID_LANGUAGES: ReadonlySet<string> = new Set(['typescript', 'python', 'go', 'rust']);

const DEFAULT_ENTRYPOINTS: Record<ScaffoldLanguage, string[]> = {
  typescript: ['src/index.ts'],
  python: ['src/main.py'],
  go: ['src/main.go'],
  rust: ['src/main.rs'],
};

// ---------------------------------------------------------------------------
// Default real FS implementation
// ---------------------------------------------------------------------------

const realScaffoldFsPort: ScaffoldFsPort = {
  async mkdir(dir) {
    await nodeFsPromises.mkdir(dir, { recursive: true });
  },
  async writeFile(p, content) {
    await nodeFsPromises.writeFile(p, content, 'utf-8');
  },
  async exists(p) {
    try {
      await nodeFsPromises.stat(p);
      return true;
    } catch {
      return false;
    }
  },
  basename(p) {
    return path.basename(p);
  },
};

// ---------------------------------------------------------------------------
// Manifest builder (immutable)
// ---------------------------------------------------------------------------

interface ManifestArgs {
  name: string;
  version: string;
  description: string;
  author: string;
  language: ScaffoldLanguage;
}

function buildManifest(args: ManifestArgs): object {
  const entrypoints = DEFAULT_ENTRYPOINTS[args.language];
  return {
    name: args.name,
    version: args.version,
    description: args.description,
    author: args.author,
    types: ['skill'],
    languages: [args.language],
    entrypoints,
  };
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runScaffold(args: ScaffoldArgs, deps: ScaffoldDeps): Promise<CommandResult> {
  const { name: nameArg, language: languageArg, interactive = false, targetDir } = args;
  const { fs: fsPort = realScaffoldFsPort, cwd = process.cwd(), prompter } = deps;

  const dir = targetDir ?? cwd;

  // ── 1. Collect name and language ─────────────────────────────────────────
  let pluginName: string;
  let pluginLanguage: ScaffoldLanguage;
  let pluginDescription: string;
  let pluginAuthor: string;

  if (interactive && prompter) {
    pluginName = await prompter.ask('Plugin name', nameArg ?? fsPort.basename(dir));
    const langAnswer = await prompter.ask('Language (typescript|python|go|rust)', 'typescript');
    pluginDescription = await prompter.ask('Description', 'A Claude plugin');
    pluginAuthor = await prompter.ask('Author', nodeOs.userInfo().username);
    pluginLanguage = langAnswer as ScaffoldLanguage;
  } else {
    pluginName = nameArg ?? fsPort.basename(dir);
    pluginLanguage = languageArg ?? 'typescript';
    pluginDescription = 'A Claude plugin';
    pluginAuthor = nodeOs.userInfo().username;
  }

  // ── 2. Validate language ──────────────────────────────────────────────────
  if (!VALID_LANGUAGES.has(pluginLanguage)) {
    return {
      exitCode: 1,
      output: `Unknown language: ${pluginLanguage}. Valid languages: typescript, python, go, rust`,
    };
  }

  const lang = pluginLanguage as ScaffoldLanguage;

  // ── 3. Create directory structure ────────────────────────────────────────
  const srcDir = path.join(dir, 'src');
  await fsPort.mkdir(srcDir);

  // ── 4. Write plugin.json ─────────────────────────────────────────────────
  const manifest = buildManifest({
    name: pluginName,
    version: '0.1.0',
    description: pluginDescription,
    author: pluginAuthor,
    language: lang,
  });
  await fsPort.writeFile(path.join(dir, 'plugin.json'), JSON.stringify(manifest, null, 2));

  // ── 5. Write minimal entrypoint ──────────────────────────────────────────
  const entrypoints = DEFAULT_ENTRYPOINTS[lang];
  const mainEntry = entrypoints[0];
  const entryPath = path.join(dir, mainEntry);
  await fsPort.writeFile(entryPath, generateEntrypoint(lang, pluginName));

  return {
    exitCode: 0,
    output: `Scaffolded plugin ${pluginName} (${lang}) at ${dir}`,
  };
}

// ---------------------------------------------------------------------------
// Entrypoint templates (minimal)
// ---------------------------------------------------------------------------

function generateEntrypoint(lang: ScaffoldLanguage, name: string): string {
  switch (lang) {
    case 'typescript':
      return `// ${name} — Claude plugin entry point\nexport default {};\n`;
    case 'python':
      return `# ${name} — Claude plugin entry point\n`;
    case 'go':
      return `// ${name} — Claude plugin entry point\npackage main\n\nfunc main() {}\n`;
    case 'rust':
      return `// ${name} — Claude plugin entry point\nfn main() {}\n`;
  }
}
