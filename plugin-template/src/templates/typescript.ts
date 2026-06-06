/**
 * TypeScript plugin scaffold template.
 *
 * Returns a GeneratedFileMap containing all files for a TypeScript plugin project.
 */

import type { GeneratedFileMap } from '../generator.js';

interface TypeScriptTemplateOptions {
  name: string;
  version: string;
  description: string;
  author: string;
  license: string;
  types: string[];
  languages: string[];
  useCaseTags?: string[] | undefined;
  entrypoints: Array<{ name: string; description: string; signature: string }>;
  dependencies: Record<string, string>;
}

/**
 * Generates the canonical plugin.json manifest content for a TypeScript plugin.
 */
function buildManifest(opts: TypeScriptTemplateOptions): string {
  const manifest: Record<string, unknown> = {
    name: opts.name,
    version: opts.version,
    description: opts.description,
    author: opts.author,
    types: opts.types,
    languages: opts.languages,
    entrypoints: opts.entrypoints,
    dependencies: opts.dependencies,
    license: opts.license,
  };

  if (opts.useCaseTags !== undefined) {
    manifest['useCaseTags'] = opts.useCaseTags;
  }

  return JSON.stringify(manifest, null, 2);
}

/**
 * Generates a TypeScript plugin scaffold file map.
 *
 * @param opts - Template options populated from GeneratorOptions
 * @returns A map of relative file paths to file contents
 */
export function generateTypescriptTemplate(opts: TypeScriptTemplateOptions): GeneratedFileMap {
  const pluginName = opts.name;
  const pluginVersion = opts.version;

  const pluginJson = buildManifest(opts);

  const packageJson = JSON.stringify(
    {
      name: pluginName,
      version: pluginVersion,
      description: opts.description,
      type: 'module',
      main: 'dist/index.js',
      exports: {
        '.': {
          import: './dist/index.js',
          types: './dist/index.d.ts',
        },
      },
      types: 'dist/index.d.ts',
      scripts: {
        build: 'tsc --project tsconfig.json',
        test: 'vitest run',
        lint: 'eslint src --ext .ts',
      },
      author: opts.author,
      license: opts.license,
      devDependencies: {
        typescript: '~5.4.0',
        vitest: '^2.0.0',
      },
    },
    null,
    2,
  );

  const srcIndexTs = `/**
 * ${pluginName} — main entrypoint
 *
 * This module exports the primary plugin functions.
 *
 * @example
 * // Example: call the main execute function
 * const result = await execute({ input: 'hello' });
 * console.log(result.output);
 */

/** Options passed to the execute function */
export interface ExecuteOptions {
  /** The input payload for this plugin */
  input: string;
  /** Optional context object */
  context?: Record<string, unknown>;
}

/** Result returned by the execute function */
export interface ExecuteResult {
  /** The processed output */
  output: string;
  /** Whether the operation succeeded */
  success: boolean;
}

/**
 * Executes the plugin's primary action.
 *
 * @param options - The input options for this invocation
 * @returns A promise resolving to the execution result
 * @throws {Error} When the input is empty or invalid
 *
 * @example
 * const result = await execute({ input: 'process this' });
 * if (result.success) {
 *   console.log(result.output);
 * }
 */
export async function execute(options: ExecuteOptions): Promise<ExecuteResult> {
  try {
    if (!options.input || options.input.trim() === '') {
      throw new Error('Input must be a non-empty string');
    }

    const output = options.input.trim();

    return { output, success: true };
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return { output: message, success: false };
  }
}
`;

  const readmeMd = `# ${pluginName}

${opts.description}

## Overview

**${pluginName}** is a ClaudeForge plugin authored by ${opts.author}.
Version: ${pluginVersion} | License: ${opts.license}

## Installation

\`\`\`bash
npm install ${pluginName}
\`\`\`

Or add it to your ClaudeForge configuration:

\`\`\`bash
claude plugin install ${pluginName}
\`\`\`

## Configuration

Configure the plugin by setting the following environment variables or editing your \`claude.config.json\`:

\`\`\`json
{
  "plugins": {
    "${pluginName}": {
      "enabled": true
    }
  }
}
\`\`\`

## Usage

\`\`\`typescript
import { execute } from '${pluginName}';

const result = await execute({ input: 'your input here' });
console.log(result.output);
\`\`\`

## API

### \`execute(options: ExecuteOptions): Promise<ExecuteResult>\`

Runs the plugin's primary action.

**Parameters:**
- \`options.input\` (\`string\`) — The input payload.
- \`options.context\` (\`Record<string, unknown>\`, optional) — Additional context.

**Returns:** \`Promise<ExecuteResult>\` — \`{ output: string, success: boolean }\`

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

1. Fork the repository
2. Create a feature branch: \`git checkout -b feat/my-feature\`
3. Commit your changes: \`git commit -m 'feat: add my feature'\`
4. Push and open a pull request.

## License

This project is licensed under the ${opts.license} License.
`;

  const gitignore = `node_modules/
dist/
build/
coverage/
.env
*.log
`;

  return {
    'plugin.json': pluginJson,
    'package.json': packageJson,
    'src/index.ts': srcIndexTs,
    'README.md': readmeMd,
    '.gitignore': gitignore,
    'tests/index.test.ts': `// Tests for ${pluginName}\nimport { describe, it, expect } from 'vitest';\nimport { execute } from '../src/index.js';\n\ndescribe('execute', () => {\n  it('returns output on valid input', async () => {\n    const result = await execute({ input: 'hello' });\n    expect(result.success).toBe(true);\n    expect(result.output).toBe('hello');\n  });\n});\n`,
    'docs/.gitkeep': '',
    'assets/.gitkeep': '',
  };
}
