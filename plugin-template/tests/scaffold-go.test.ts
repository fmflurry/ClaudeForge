/**
 * Tests for Go plugin scaffold (Task 19.1 / 19.3)
 *
 * Production module: src/generator.ts
 * Go template module: src/templates/go.ts
 *
 * Asserts that generatePluginTemplate({ language: 'go', ... }) produces:
 *   - plugin.json           (canonical manifest with languages: ['go'])
 *   - go.mod                (Go module manifest)
 *   - main/handler .go      (example entrypoint Go file in main package or handler)
 *   - tests/                (at least a placeholder test file)
 *   - README.md             (with required sections)
 *   - Subdirectories: src/, docs/, tests/, assets/
 *
 * README required sections (verbatim from spec.md):
 *   Overview, Installation, Configuration, Usage, API, Contributing, License
 */

import { describe, it, expect } from 'vitest';
import { generatePluginTemplate } from '../src/generator.js';
import type { GeneratedFileMap } from '../src/generator.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function findFile(files: GeneratedFileMap, relativePath: string): string | undefined {
  if (relativePath in files) return files[relativePath];
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

// ---------------------------------------------------------------------------
// Go scaffold — required files
// ---------------------------------------------------------------------------

describe('Go scaffold — required files', () => {
  it('generates plugin.json', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    expect(findFile(files, 'plugin.json')).toBeDefined();
  });

  it('generates go.mod', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    expect(findFile(files, 'go.mod')).toBeDefined();
  });

  it('generates at least one .go source file', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const hasGoFile = Object.keys(files).some((k) => k.endsWith('.go'));
    expect(hasGoFile).toBe(true);
  });

  it('generates README.md', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    expect(findFile(files, 'README.md')).toBeDefined();
  });

  it('generates at least one file under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const hasTests = Object.keys(files).some((k) => k.startsWith('tests/') || k.includes('/tests/'));
    expect(hasTests).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Go scaffold — subdirectories
// ---------------------------------------------------------------------------

describe('Go scaffold — subdirectories src/, docs/, tests/, assets/', () => {
  it('contains files under src/', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const hasSrc = Object.keys(files).some((k) => k.startsWith('src/') || k.includes('/src/'));
    expect(hasSrc).toBe(true);
  });

  it('contains an entry under docs/', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const hasDocs = Object.keys(files).some((k) => k.startsWith('docs/') || k.includes('/docs/'));
    expect(hasDocs).toBe(true);
  });

  it('contains an entry under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const hasTests = Object.keys(files).some((k) => k.startsWith('tests/') || k.includes('/tests/'));
    expect(hasTests).toBe(true);
  });

  it('contains an entry under assets/', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const hasAssets = Object.keys(files).some((k) => k.startsWith('assets/') || k.includes('/assets/'));
    expect(hasAssets).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Go scaffold — go.mod content
// ---------------------------------------------------------------------------

describe('Go scaffold — go.mod content', () => {
  it('go.mod starts with "module" declaration', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const content = requireFile(files, 'go.mod');
    expect(content.trim()).toMatch(/^module\s+/);
  });

  it('go.mod contains the plugin name in the module path', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const content = requireFile(files, 'go.mod');
    expect(content).toContain('my-go-plugin');
  });

  it('go.mod contains a "go" version directive', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const content = requireFile(files, 'go.mod');
    expect(content).toMatch(/^go\s+\d+\.\d+/m);
  });
});

// ---------------------------------------------------------------------------
// Go scaffold — .go entrypoint content
// ---------------------------------------------------------------------------

describe('Go scaffold — .go entrypoint', () => {
  it('the .go file declares a package', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const goKey = Object.keys(files).find((k) => k.endsWith('.go'));
    expect(goKey).toBeDefined();
    expect(files[goKey as string]).toMatch(/^package\s+\w+/m);
  });

  it('the .go file contains at least one func declaration', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const goKey = Object.keys(files).find((k) => k.endsWith('.go'));
    expect(goKey).toBeDefined();
    expect(files[goKey as string]).toMatch(/func\s+\w+/);
  });

  it('the .go file contains a comment documenting the function', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const goKey = Object.keys(files).find((k) => k.endsWith('.go'));
    expect(goKey).toBeDefined();
    // Go doc comments: // FunctionName ...
    expect(files[goKey as string]).toMatch(/\/\/\s+\w/);
  });
});

// ---------------------------------------------------------------------------
// Go scaffold — manifest language
// ---------------------------------------------------------------------------

describe('Go scaffold — manifest language', () => {
  it("plugin.json languages[] includes 'go'", () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const manifestKey = Object.keys(files).find((k) => k === 'plugin.json' || k.endsWith('/plugin.json'));
    expect(manifestKey).toBeDefined();
    const manifest = JSON.parse(files[manifestKey as string]) as { languages: string[] };
    expect(manifest.languages).toContain('go');
  });
});

// ---------------------------------------------------------------------------
// Go scaffold — README.md sections
// ---------------------------------------------------------------------------

describe('Go scaffold — README.md sections', () => {
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
      const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
      const readme = requireFile(files, 'README.md');
      const pattern = new RegExp(`#{1,3}\\s+${section}`, 'i');
      expect(readme).toMatch(pattern);
    });
  }

  it('README.md references go build or go run', () => {
    const files = generatePluginTemplate({ name: 'my-go-plugin', language: 'go' });
    const readme = requireFile(files, 'README.md');
    expect(readme).toMatch(/go\s+(build|run|install|get)/i);
  });
});
