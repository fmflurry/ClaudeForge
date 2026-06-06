/**
 * Plugin template generator — core module.
 *
 * Exports types and the pure `generatePluginTemplate` function which produces
 * a file map (relative path → content) for a plugin project scaffold.
 *
 * No disk I/O is performed here; the caller is responsible for writing files.
 */

import { generateTypescriptTemplate } from './templates/typescript.js';
import { generatePythonTemplate } from './templates/python.js';
import { generateGoTemplate } from './templates/go.js';
import { generateRustTemplate } from './templates/rust.js';

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** Supported template languages. */
export type TemplateLanguage = 'typescript' | 'python' | 'go' | 'rust';

/** Canonical plugin types (mirrors the marketplace enum). */
export type PluginType = 'skill' | 'hook' | 'agent' | 'command' | 'plugin';

/** Audience targeting tags for marketplace discovery. */
export type UseCaseTag =
  | 'dev-team'
  | 'product-owner'
  | 'product-manager'
  | 'devops'
  | 'security'
  | 'data-analyst';

/** Output of generatePluginTemplate: a map of relative-path → file-content. */
export type GeneratedFileMap = Record<string, string>;

/** Options accepted by generatePluginTemplate. */
export interface GeneratorOptions {
  /** Plugin name (e.g. "my-auth-plugin" or "@scope/my-plugin"). Required. */
  name: string;
  /** Primary language for the scaffold. Required. */
  language: TemplateLanguage;
  /** Human-readable description. Defaults to a placeholder. */
  description?: string;
  /** Author string. Defaults to "Plugin Author". */
  author?: string;
  /** One or more canonical plugin types. Defaults to ['plugin']. */
  types?: PluginType[];
  /** Additional language strings for multi-language plugins. Defaults to [language]. */
  languages?: string[];
  /** Audience targeting tags. Defaults to omitted (no tags). */
  useCaseTags?: UseCaseTag[];
  /** Semver version string. Defaults to '0.1.0'. */
  version?: string;
}

// ---------------------------------------------------------------------------
// Internal resolved options shape (all fields required)
// ---------------------------------------------------------------------------

interface ResolvedOptions {
  name: string;
  language: TemplateLanguage;
  version: string;
  description: string;
  author: string;
  types: PluginType[];
  languages: string[];
  useCaseTags: UseCaseTag[] | undefined;
  entrypoints: Array<{ name: string; description: string; signature: string }>;
  dependencies: Record<string, string>;
  license: string;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const VALID_LANGUAGES = new Set<string>(['typescript', 'python', 'go', 'rust']);

const DEFAULT_VERSION = '0.1.0';
const DEFAULT_AUTHOR = 'Plugin Author';
const DEFAULT_LICENSE = 'MIT';
const DEFAULT_TYPES: PluginType[] = ['plugin'];

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------

/**
 * Resolves final options, applying defaults for any omitted optional fields.
 * Does NOT mutate the input object.
 */
function resolveOptions(opts: GeneratorOptions): ResolvedOptions {
  const name = opts.name;
  const language = opts.language;
  const version = opts.version ?? DEFAULT_VERSION;
  const description = opts.description ?? `A ClaudeForge plugin: ${name}`;
  const author = opts.author ?? DEFAULT_AUTHOR;
  const types: PluginType[] = opts.types !== undefined ? [...opts.types] : [...DEFAULT_TYPES];
  const languages: string[] =
    opts.languages !== undefined ? [...opts.languages] : [language];

  // useCaseTags: pass through as-is (may be empty array or undefined)
  const useCaseTags: UseCaseTag[] | undefined =
    opts.useCaseTags !== undefined ? [...opts.useCaseTags] : undefined;

  const entrypoints: Array<{ name: string; description: string; signature: string }> = [
    {
      name: 'execute',
      description: `Execute the ${name} plugin action`,
      signature: 'execute(input: string): Promise<string>',
    },
  ];

  const dependencies: Record<string, string> = {};

  return {
    name,
    language,
    version,
    description,
    author,
    types,
    languages,
    useCaseTags,
    entrypoints,
    dependencies,
    license: DEFAULT_LICENSE,
  };
}

/**
 * Generates a complete plugin project scaffold as an in-memory file map.
 *
 * This function is pure: it performs no disk I/O, has no side effects, and
 * returns the same output for the same input.
 *
 * @param options - Generator options; only `name` and `language` are required.
 * @returns A record mapping relative file paths to their string contents.
 * @throws {Error} When `language` is not one of the supported values.
 *
 * @example
 * const files = generatePluginTemplate({ name: 'my-plugin', language: 'typescript' });
 * // files['plugin.json'] → canonical manifest JSON string
 * // files['src/index.ts'] → TypeScript entrypoint source
 */
export function generatePluginTemplate(options: GeneratorOptions): GeneratedFileMap {
  if (!VALID_LANGUAGES.has(options.language)) {
    throw new Error(
      `Unsupported language: "${options.language}". Must be one of: ${[...VALID_LANGUAGES].join(', ')}.`,
    );
  }

  const resolved = resolveOptions(options);

  switch (resolved.language) {
    case 'typescript':
      return generateTypescriptTemplate(resolved);
    case 'python':
      return generatePythonTemplate(resolved);
    case 'go':
      return generateGoTemplate(resolved);
    case 'rust':
      return generateRustTemplate(resolved);
  }
}
