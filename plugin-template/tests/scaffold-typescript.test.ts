/**
 * Tests for TypeScript plugin scaffold (Task 19.1 / 19.2)
 *
 * Production module: src/generator.ts
 * TypeScript template module: src/templates/typescript.ts
 *
 * Asserts that generatePluginTemplate({ language: 'typescript', ... }) produces:
 *   - plugin.json           (canonical manifest)
 *   - package.json          (Node.js package manifest)
 *   - src/index.ts          (example entrypoint with JSDoc)
 *   - tests/                (at least a placeholder test file)
 *   - .gitignore
 *   - README.md             (with required sections per spec)
 *   - Subdirectories: src/, docs/, tests/, assets/
 *
 * README required sections (verbatim from spec.md §"Generated README with sections"):
 *   Overview, Installation, Configuration, Usage, API, Contributing, License
 *
 * src/index.ts requirements (verbatim from spec.md §"Entrypoint documentation in code"):
 *   - JSDoc comments for each exported function/entrypoint
 *   - Type definitions for parameters and return values
 *   - Example usage in comments
 *   - Error handling documentation
 */

import { describe, it, expect } from 'vitest';
import { generatePluginTemplate } from '../src/generator.js';
import type { GeneratedFileMap } from '../src/generator.js';

// ---------------------------------------------------------------------------
// Helper: find a file in the map by its relative path (exact or ending match)
// ---------------------------------------------------------------------------

function findFile(files: GeneratedFileMap, relativePath: string): string | undefined {
  // Exact match first
  if (relativePath in files) return files[relativePath];
  // Ending match (handles leading './' variations)
  const key = Object.keys(files).find(
    (k) => k === relativePath || k.endsWith(`/${relativePath}`) || k === `./${relativePath}`,
  );
  return key !== undefined ? files[key] : undefined;
}

function requireFile(files: GeneratedFileMap, relativePath: string): string {
  const content = findFile(files, relativePath);
  if (content === undefined) {
    throw new Error(
      `Expected file '${relativePath}' not found. Available: ${Object.keys(files).join(', ')}`,
    );
  }
  return content;
}

function hasPathMatching(files: GeneratedFileMap, prefix: string): boolean {
  return Object.keys(files).some((k) => k.startsWith(prefix) || k.includes(`/${prefix}`));
}

// ---------------------------------------------------------------------------
// TypeScript scaffold — required files
// ---------------------------------------------------------------------------

describe('TypeScript scaffold — required files', () => {
  it('generates plugin.json', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    expect(findFile(files, 'plugin.json')).toBeDefined();
  });

  it('generates package.json', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    expect(findFile(files, 'package.json')).toBeDefined();
  });

  it('generates src/index.ts', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    expect(findFile(files, 'src/index.ts')).toBeDefined();
  });

  it('generates .gitignore', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    expect(findFile(files, '.gitignore')).toBeDefined();
  });

  it('generates README.md', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    expect(findFile(files, 'README.md')).toBeDefined();
  });

  it('generates at least one file under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    expect(hasPathMatching(files, 'tests/')).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// TypeScript scaffold — subdirectory markers
// ---------------------------------------------------------------------------

describe('TypeScript scaffold — subdirectories src/, docs/, tests/, assets/', () => {
  it('contains files under src/', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const hasSrc = Object.keys(files).some((k) => k.startsWith('src/') || k.includes('/src/'));
    expect(hasSrc).toBe(true);
  });

  it('contains an entry under docs/ or docs directory marker', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    // docs/ may be represented as a .gitkeep or any placeholder file
    const hasDocs = Object.keys(files).some((k) => k.startsWith('docs/') || k.includes('/docs/'));
    expect(hasDocs).toBe(true);
  });

  it('contains an entry under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const hasTests = Object.keys(files).some((k) => k.startsWith('tests/') || k.includes('/tests/'));
    expect(hasTests).toBe(true);
  });

  it('contains an entry under assets/', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const hasAssets = Object.keys(files).some((k) => k.startsWith('assets/') || k.includes('/assets/'));
    expect(hasAssets).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// TypeScript scaffold — package.json content
// ---------------------------------------------------------------------------

describe('TypeScript scaffold — package.json content', () => {
  it('package.json is valid JSON', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const content = requireFile(files, 'package.json');
    expect(() => JSON.parse(content)).not.toThrow();
  });

  it('package.json name reflects the plugin name', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const pkgJson = JSON.parse(requireFile(files, 'package.json')) as { name: string };
    expect(pkgJson.name).toContain('my-ts-plugin');
  });

  it('package.json has a main or exports field', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const pkgJson = JSON.parse(requireFile(files, 'package.json')) as Record<string, unknown>;
    expect(pkgJson['main'] !== undefined || pkgJson['exports'] !== undefined).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// TypeScript scaffold — src/index.ts content (per spec §"Entrypoint documentation")
// ---------------------------------------------------------------------------

describe('TypeScript scaffold — src/index.ts content', () => {
  it('src/index.ts contains JSDoc comment (/** ... */)', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const content = requireFile(files, 'src/index.ts');
    // JSDoc: /**
    expect(content).toMatch(/\/\*\*/);
  });

  it('src/index.ts contains at least one exported function', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const content = requireFile(files, 'src/index.ts');
    expect(content).toMatch(/export\s+(async\s+)?function\s+\w+/);
  });

  it('src/index.ts contains @param or @returns JSDoc tag', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const content = requireFile(files, 'src/index.ts');
    expect(content).toMatch(/@param|@returns?/);
  });

  it('src/index.ts contains @example tag or example comment', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const content = requireFile(files, 'src/index.ts');
    // @example or an "Example" comment block
    expect(content).toMatch(/@example|[Ee]xample/);
  });

  it('src/index.ts contains error handling (try/catch or throws documentation)', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const content = requireFile(files, 'src/index.ts');
    // try/catch OR @throws JSDoc tag
    expect(content).toMatch(/try\s*\{|@throws/);
  });

  it('src/index.ts uses TypeScript types (not plain JS)', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const content = requireFile(files, 'src/index.ts');
    // Must have at least one explicit type annotation (: Type or interface/type keyword)
    expect(content).toMatch(/:\s*\w+|interface\s+\w+|type\s+\w+\s*=/);
  });
});

// ---------------------------------------------------------------------------
// TypeScript scaffold — README.md sections (per spec §"Generated README with sections")
// Verbatim required sections: Overview, Installation, Configuration, Usage, API,
// Contributing, License
// ---------------------------------------------------------------------------

describe('TypeScript scaffold — README.md sections', () => {
  const REQUIRED_SECTIONS = [
    'Overview',
    'Installation',
    'Configuration',
    'Usage',
    'API',
    'Contributing',
    'License',
  ];

  for (const section of REQUIRED_SECTIONS) {
    it(`README.md contains section: ${section}`, () => {
      const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
      const readme = requireFile(files, 'README.md');
      // Must appear as a markdown heading (# Section or ## Section)
      const pattern = new RegExp(`#{1,3}\\s+${section}`, 'i');
      expect(readme).toMatch(pattern);
    });
  }

  it('README.md Overview section is auto-populated with the plugin name', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const readme = requireFile(files, 'README.md');
    expect(readme).toContain('my-ts-plugin');
  });

  it('README.md License section references MIT', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const readme = requireFile(files, 'README.md');
    expect(readme).toMatch(/MIT/);
  });
});

// ---------------------------------------------------------------------------
// TypeScript scaffold — .gitignore content
// ---------------------------------------------------------------------------

describe('TypeScript scaffold — .gitignore', () => {
  it('.gitignore contains node_modules', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const gitignore = requireFile(files, '.gitignore');
    expect(gitignore).toContain('node_modules');
  });

  it('.gitignore contains dist or build output', () => {
    const files = generatePluginTemplate({ name: 'my-ts-plugin', language: 'typescript' });
    const gitignore = requireFile(files, '.gitignore');
    expect(gitignore).toMatch(/dist|build/);
  });
});
