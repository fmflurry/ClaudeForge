/**
 * Scaffold-source module: pure factory for add-on scaffold output.
 *
 * Calls `generatePluginTemplate` from @claudeforge/plugin-template and adapts
 * the resulting GeneratedFileMap into an addon-model-shaped output:
 *   - Drops the marketplace `plugin.json` key
 *   - Remaps generated paths to the per-type canonical shape
 *   - Synthesizes a valid `addon.json` manifest
 *
 * No I/O — returns an in-memory file map.  All helpers are exported so the
 * future scaffold-command refactor (task 11.2) can reuse them.
 */

import { generatePluginTemplate } from '@claudeforge/plugin-template';
import type { GeneratedFileMap, PluginType, TemplateLanguage } from '@claudeforge/plugin-template';
import type { AddonType, AddonScope } from './manifest.js';

// ---------------------------------------------------------------------------
// Re-export from the generator so callers stay decoupled from the package
// ---------------------------------------------------------------------------

export type { TemplateLanguage } from '@claudeforge/plugin-template';

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** The result of buildAddonScaffold: a synthesized addon.json plus a full file map. */
export interface AddonScaffold {
  /** Serialised `addon.json` content. */
  addonJson: string;
  /**
   * Relative path → file content map, including:
   *   - All adapted source files from the generator
   *   - The synthesized `addon.json` itself
   */
  files: Record<string, string>;
}

// ---------------------------------------------------------------------------
// Exported helpers (reusable by scaffold command refactor, task 11.2)
// ---------------------------------------------------------------------------

/**
 * Maps an `AddonType` to the corresponding `PluginType` used by the generator.
 *
 * The generator's `PluginType` union is `'skill' | 'hook' | 'agent' | 'command' | 'plugin'`.
 * Our `AddonType` is `'hook' | 'plugin' | 'skill' | 'agent'` — a strict subset.
 * The mapping is therefore 1-to-1 for all four add-on types.
 */
export function mapAddonTypeToPluginType(addonType: AddonType): PluginType {
  switch (addonType) {
    case 'agent':
      return 'agent';
    case 'skill':
      return 'skill';
    case 'hook':
      return 'hook';
    case 'plugin':
      return 'plugin';
  }
}

/**
 * Returns the default `supportedScopes` for a given add-on type.
 *
 * Per Decision 7 and the manifest constraints:
 *   - `plugin` ⇒ `['global']` (global-only; local is forbidden)
 *   - all others ⇒ `['local', 'global']`
 */
export function defaultSupportedScopes(type: AddonType): AddonScope[] {
  if (type === 'plugin') {
    return ['global'];
  }
  return ['local', 'global'];
}

/**
 * Returns the canonical hook-script file extension for a given language.
 *
 * TypeScript scripts remain `.ts` for direct execution via ts-node/tsx;
 * Python, Go, and Rust use their native extensions; all others fall back to `.sh`.
 */
export function hookScriptExtension(language: string): string {
  switch (language) {
    case 'typescript':
      return '.ts';
    case 'python':
      return '.py';
    case 'go':
      return '.go';
    case 'rust':
      return '.rs';
    default:
      return '.sh';
  }
}

// ---------------------------------------------------------------------------
// Internal per-type path remapping
// ---------------------------------------------------------------------------

interface RemapResult {
  remappedFiles: Record<string, string>;
  /** Paths to declare in addon.json's `files` field (the installed subset). */
  canonicalFiles: string[];
}

/**
 * Remaps the raw GeneratedFileMap from the generator into the canonical addon
 * shape for the given type, dropping the marketplace `plugin.json` key.
 */
function remapFiles(
  name: string,
  type: AddonType,
  language: string,
  generated: GeneratedFileMap,
): RemapResult {
  // Start from the generated map, always dropping the marketplace plugin.json.
  const base: Record<string, string> = {};
  for (const [key, value] of Object.entries(generated)) {
    if (key !== 'plugin.json') {
      base[key] = value;
    }
  }

  switch (type) {
    case 'agent': {
      // Agent is a single <name>.md file.
      // Use the generated README.md content as the agent markdown body;
      // fall back to a minimal stub if the template did not include one.
      const agentContent = base['README.md'] ?? `# ${name}\n\nAgent description.\n`;
      // Remove README.md from base — replaced by <name>.md.
      const { 'README.md': _readme, ...rest } = base;
      void _readme; // intentionally unused — replaced by <name>.md
      const agentPath = `${name}.md`;
      return {
        remappedFiles: { ...rest, [agentPath]: agentContent },
        canonicalFiles: [agentPath],
      };
    }

    case 'skill': {
      // Skill is a directory tree; SKILL.md is the required entry point.
      // Rename README.md → SKILL.md if SKILL.md is absent.
      const files: Record<string, string> = { ...base };
      if (!('SKILL.md' in files)) {
        const readmeContent = files['README.md'] ?? `# ${name}\n\nSkill description.\n`;
        delete files['README.md'];
        files['SKILL.md'] = readmeContent;
      }

      // Canonical install files: all generated paths (excluding addon.json which is added later).
      const canonicalFiles = Object.keys(files).filter((k) => k !== 'addon.json');
      return { remappedFiles: files, canonicalFiles };
    }

    case 'hook': {
      // Hook is a script placed under hooks/<name><ext>.
      // The main source file from the generator becomes the hook script.
      const ext = hookScriptExtension(language);
      const scriptPath = `hooks/${name}${ext}`;

      // Pick the primary source entry as the hook script content.
      const scriptContent =
        base['src/index.ts'] ??
        base['src/main.py'] ??
        base['src/main.go'] ??
        base['src/lib.rs'] ??
        `#!/usr/bin/env bash\n# ${name} hook\n`;

      // Build remapped files: include non-source generated files alongside the hook script.
      const files: Record<string, string> = {};
      for (const [key, value] of Object.entries(base)) {
        // Skip primary source entries that are remapped to scriptPath.
        if (
          key === 'src/index.ts' ||
          key === 'src/main.py' ||
          key === 'src/main.go' ||
          key === 'src/lib.rs'
        ) {
          continue;
        }
        files[key] = value;
      }
      files[scriptPath] = scriptContent;

      return {
        remappedFiles: files,
        canonicalFiles: [scriptPath],
      };
    }

    case 'plugin': {
      // Plugin bundle: the marketplace plugin.json (from `generated`) becomes
      // the inner bundle manifest at .claude-plugin/plugin.json.
      const bundleManifestPath = '.claude-plugin/plugin.json';
      const bundleManifestContent = generated['plugin.json'] ?? JSON.stringify({ name }, null, 2);

      const files: Record<string, string> = { ...base, [bundleManifestPath]: bundleManifestContent };

      // Canonical files: all paths in the bundle (source + inner manifest).
      const canonicalFiles = Object.keys(files).filter((k) => k !== 'addon.json');
      return { remappedFiles: files, canonicalFiles };
    }
  }
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Generates a complete add-on scaffold as an in-memory file map.
 *
 * Delegates to `generatePluginTemplate` for all source file content — no
 * template bodies are duplicated here.  The returned `AddonScaffold` contains:
 *   - `addonJson`: the serialised `addon.json` string
 *   - `files`: the full file map, including `addon.json` under its key
 *
 * @param args.name     - Add-on name (e.g. "my-hook")
 * @param args.type     - Add-on type: 'agent' | 'skill' | 'hook' | 'plugin'
 * @param args.scope    - Optional explicit scope (unused by this pure function;
 *                        included for caller convenience / future expansion)
 * @param args.language - Template language
 */
export function buildAddonScaffold(args: {
  name: string;
  type: AddonType;
  scope?: AddonScope;
  language: TemplateLanguage;
}): AddonScaffold {
  const { name, type, language } = args;

  // 1. Delegate to the generator — no template bodies duplicated here.
  const generated = generatePluginTemplate({
    name,
    language,
    types: [mapAddonTypeToPluginType(type)],
  });

  // 2. Remap generated paths to the per-type canonical addon shape.
  const { remappedFiles, canonicalFiles } = remapFiles(name, type, language, generated);

  // 3. Synthesize the addon.json manifest.
  const supportedScopes = defaultSupportedScopes(type);

  // Base manifest object (hook field added conditionally below).
  const manifest: Record<string, unknown> = {
    name,
    version: '0.1.0',
    type,
    supportedScopes,
    files: canonicalFiles,
  };

  // For hook type: add the hook stub referencing the canonical script path.
  if (type === 'hook') {
    const ext = hookScriptExtension(language);
    const scriptPath = `hooks/${name}${ext}`;
    manifest['hook'] = {
      event: 'PreToolUse',
      matcher: '*',
      command: scriptPath,
      type: 'command',
    };
  }

  const addonJson = JSON.stringify(manifest, null, 2);

  // 4. Include addon.json in the file map.
  const files: Record<string, string> = { ...remappedFiles, 'addon.json': addonJson };

  return { addonJson, files };
}
