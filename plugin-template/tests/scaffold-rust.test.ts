/**
 * Tests for Rust plugin scaffold (Task 19.1 / 19.3)
 *
 * Production module: src/generator.ts
 * Rust template module: src/templates/rust.ts
 *
 * Asserts that generatePluginTemplate({ language: 'rust', ... }) produces:
 *   - plugin.json           (canonical manifest with languages: ['rust'])
 *   - Cargo.toml            (Rust package manifest)
 *   - src/lib.rs or src/main.rs (example entrypoint)
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
// Rust scaffold — required files
// ---------------------------------------------------------------------------

describe('Rust scaffold — required files', () => {
  it('generates plugin.json', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    expect(findFile(files, 'plugin.json')).toBeDefined();
  });

  it('generates Cargo.toml', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    expect(findFile(files, 'Cargo.toml')).toBeDefined();
  });

  it('generates src/lib.rs or src/main.rs', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const hasRsEntrypoint =
      findFile(files, 'src/lib.rs') !== undefined ||
      findFile(files, 'src/main.rs') !== undefined;
    expect(hasRsEntrypoint).toBe(true);
  });

  it('generates README.md', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    expect(findFile(files, 'README.md')).toBeDefined();
  });

  it('generates at least one file under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const hasTests = Object.keys(files).some((k) => k.startsWith('tests/') || k.includes('/tests/'));
    expect(hasTests).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Rust scaffold — subdirectories
// ---------------------------------------------------------------------------

describe('Rust scaffold — subdirectories src/, docs/, tests/, assets/', () => {
  it('contains files under src/', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const hasSrc = Object.keys(files).some((k) => k.startsWith('src/') || k.includes('/src/'));
    expect(hasSrc).toBe(true);
  });

  it('contains an entry under docs/', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const hasDocs = Object.keys(files).some((k) => k.startsWith('docs/') || k.includes('/docs/'));
    expect(hasDocs).toBe(true);
  });

  it('contains an entry under tests/', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const hasTests = Object.keys(files).some((k) => k.startsWith('tests/') || k.includes('/tests/'));
    expect(hasTests).toBe(true);
  });

  it('contains an entry under assets/', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const hasAssets = Object.keys(files).some((k) => k.startsWith('assets/') || k.includes('/assets/'));
    expect(hasAssets).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Rust scaffold — Cargo.toml content
// ---------------------------------------------------------------------------

describe('Rust scaffold — Cargo.toml content', () => {
  it('Cargo.toml contains [package] section', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const content = requireFile(files, 'Cargo.toml');
    expect(content).toMatch(/\[package\]/);
  });

  it('Cargo.toml name field contains the plugin name', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const content = requireFile(files, 'Cargo.toml');
    expect(content).toContain('my-rust-plugin');
  });

  it('Cargo.toml contains a version field', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const content = requireFile(files, 'Cargo.toml');
    expect(content).toMatch(/version\s*=/);
  });

  it('Cargo.toml contains an edition field', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const content = requireFile(files, 'Cargo.toml');
    expect(content).toMatch(/edition\s*=/);
  });
});

// ---------------------------------------------------------------------------
// Rust scaffold — src/lib.rs or src/main.rs content
// ---------------------------------------------------------------------------

describe('Rust scaffold — Rust entrypoint content', () => {
  function getRsContent(files: GeneratedFileMap): string {
    const libContent = findFile(files, 'src/lib.rs');
    if (libContent !== undefined) return libContent;
    const mainContent = findFile(files, 'src/main.rs');
    if (mainContent !== undefined) return mainContent;
    throw new Error('Neither src/lib.rs nor src/main.rs found');
  }

  it('the .rs entrypoint contains a pub fn or fn declaration', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const content = getRsContent(files);
    expect(content).toMatch(/pub\s+fn\s+\w+|fn\s+\w+/);
  });

  it('the .rs entrypoint contains a doc comment (/// or //!)', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const content = getRsContent(files);
    // Rust doc comments: /// or //!
    expect(content).toMatch(/\/\/\/|\/\/!/);
  });

  it('the .rs entrypoint compiles syntactically (has valid #[...] or fn pattern)', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const content = getRsContent(files);
    // Should contain either fn main, pub fn, or a #[...] attribute
    expect(content).toMatch(/fn\s+\w+|#\[/);
  });
});

// ---------------------------------------------------------------------------
// Rust scaffold — manifest language
// ---------------------------------------------------------------------------

describe('Rust scaffold — manifest language', () => {
  it("plugin.json languages[] includes 'rust'", () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const manifestKey = Object.keys(files).find((k) => k === 'plugin.json' || k.endsWith('/plugin.json'));
    expect(manifestKey).toBeDefined();
    const manifest = JSON.parse(files[manifestKey as string]) as { languages: string[] };
    expect(manifest.languages).toContain('rust');
  });
});

// ---------------------------------------------------------------------------
// Rust scaffold — README.md sections
// ---------------------------------------------------------------------------

describe('Rust scaffold — README.md sections', () => {
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
      const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
      const readme = requireFile(files, 'README.md');
      const pattern = new RegExp(`#{1,3}\\s+${section}`, 'i');
      expect(readme).toMatch(pattern);
    });
  }

  it('README.md references cargo build or cargo add', () => {
    const files = generatePluginTemplate({ name: 'my-rust-plugin', language: 'rust' });
    const readme = requireFile(files, 'README.md');
    expect(readme).toMatch(/cargo\s+(build|run|install|add|test)/i);
  });
});
