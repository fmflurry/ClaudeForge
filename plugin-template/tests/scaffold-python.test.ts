/**
 * Tests for Python plugin scaffold (Task 19.1 / 19.3)
 *
 * Production module: src/generator.ts
 * Python template module: src/templates/python.ts
 *
 * Asserts that generatePluginTemplate({ language: 'python', ... }) produces:
 *   - plugin.json           (canonical manifest with languages: ['python'])
 *   - pyproject.toml        (Python package manifest, PEP 517/518)
 *   - src/ or a package module with example entrypoint (*.py)
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
// Python scaffold — required files
// ---------------------------------------------------------------------------

describe('Python scaffold — required files', () => {
  it('generates plugin.json', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    expect(findFile(files, 'plugin.json')).toBeDefined();
  });

  it('generates pyproject.toml', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    expect(findFile(files, 'pyproject.toml')).toBeDefined();
  });

  it('generates at least one .py source file', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const hasPyFile = Object.keys(files).some((k) => k.endsWith('.py'));
    expect(hasPyFile).toBe(true);
  });

  it('generates README.md', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    expect(findFile(files, 'README.md')).toBeDefined();
  });

  it('generates at least one file under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const hasTests = Object.keys(files).some((k) => k.startsWith('tests/') || k.includes('/tests/'));
    expect(hasTests).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Python scaffold — subdirectories
// ---------------------------------------------------------------------------

describe('Python scaffold — subdirectories src/, docs/, tests/, assets/', () => {
  it('contains files under src/', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const hasSrc = Object.keys(files).some((k) => k.startsWith('src/') || k.includes('/src/'));
    expect(hasSrc).toBe(true);
  });

  it('contains an entry under docs/', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const hasDocs = Object.keys(files).some((k) => k.startsWith('docs/') || k.includes('/docs/'));
    expect(hasDocs).toBe(true);
  });

  it('contains an entry under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const hasTests = Object.keys(files).some((k) => k.startsWith('tests/') || k.includes('/tests/'));
    expect(hasTests).toBe(true);
  });

  it('contains an entry under assets/', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const hasAssets = Object.keys(files).some((k) => k.startsWith('assets/') || k.includes('/assets/'));
    expect(hasAssets).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Python scaffold — pyproject.toml content
// ---------------------------------------------------------------------------

describe('Python scaffold — pyproject.toml content', () => {
  it('pyproject.toml contains [project] or [tool.poetry] section', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const content = requireFile(files, 'pyproject.toml');
    expect(content).toMatch(/\[project\]|\[tool\.poetry\]/);
  });

  it('pyproject.toml contains the plugin name', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const content = requireFile(files, 'pyproject.toml');
    expect(content).toContain('my-py-plugin');
  });

  it('pyproject.toml contains a version field', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const content = requireFile(files, 'pyproject.toml');
    expect(content).toMatch(/version\s*=/);
  });
});

// ---------------------------------------------------------------------------
// Python scaffold — example entrypoint (.py)
// ---------------------------------------------------------------------------

describe('Python scaffold — example entrypoint', () => {
  it('the .py entrypoint contains a function definition', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const pyKey = Object.keys(files).find((k) => k.endsWith('.py') && (k.startsWith('src/') || k.includes('/src/')));
    expect(pyKey).toBeDefined();
    expect(files[pyKey as string]).toMatch(/def\s+\w+/);
  });

  it('the .py entrypoint contains a docstring', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const pyKey = Object.keys(files).find((k) => k.endsWith('.py') && (k.startsWith('src/') || k.includes('/src/')));
    expect(pyKey).toBeDefined();
    // Python docstring: triple quotes
    expect(files[pyKey as string]).toMatch(/"""|'''/);
  });
});

// ---------------------------------------------------------------------------
// Python scaffold — manifest: language is 'python'
// ---------------------------------------------------------------------------

describe('Python scaffold — manifest language', () => {
  it("plugin.json languages[] includes 'python'", () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const manifestKey = Object.keys(files).find((k) => k === 'plugin.json' || k.endsWith('/plugin.json'));
    expect(manifestKey).toBeDefined();
    const manifest = JSON.parse(files[manifestKey as string]) as { languages: string[] };
    expect(manifest.languages).toContain('python');
  });
});

// ---------------------------------------------------------------------------
// Python scaffold — README.md sections
// ---------------------------------------------------------------------------

describe('Python scaffold — README.md sections', () => {
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
      const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
      const readme = requireFile(files, 'README.md');
      const pattern = new RegExp(`#{1,3}\\s+${section}`, 'i');
      expect(readme).toMatch(pattern);
    });
  }

  it('README.md references pip or Python installation command', () => {
    const files = generatePluginTemplate({ name: 'my-py-plugin', language: 'python' });
    const readme = requireFile(files, 'README.md');
    expect(readme).toMatch(/pip|python|uv/i);
  });
});
