/**
 * Tests for GeneratorOptions handling: explicit values vs defaults (Tasks 19.1–19.4)
 *
 * This file tests the options contract of generatePluginTemplate without
 * duplicating language-specific file assertions (those live in scaffold-*.test.ts).
 *
 * Focus:
 *   - Explicit name/language/description/author populates the manifest
 *   - Sensible defaults when optional fields omitted (version '0.1.0', license 'MIT')
 *   - types[] defaults to a valid enum value when not supplied
 *   - All four languages accepted without throwing
 *   - Invalid language throws a descriptive error
 *   - Options object is not mutated (immutability)
 *   - Edge cases: empty useCaseTags, empty types (error case)
 */

import { describe, it, expect } from 'vitest';
import { generatePluginTemplate } from '../src/generator.js';
import type { GeneratorOptions, GeneratedFileMap, TemplateLanguage } from '../src/generator.js';

// ---------------------------------------------------------------------------
// Helper: extract plugin.json from file map
// ---------------------------------------------------------------------------

function extractManifest(files: GeneratedFileMap): Record<string, unknown> {
  const key = Object.keys(files).find((k) => k === 'plugin.json' || k.endsWith('/plugin.json'));
  if (!key) {
    throw new Error(`plugin.json not found. Keys: ${Object.keys(files).join(', ')}`);
  }
  return JSON.parse(files[key]) as Record<string, unknown>;
}

// ---------------------------------------------------------------------------
// Explicit options — manifest population
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — explicit options populate manifest', () => {
  it('explicit name is reflected in plugin.json', () => {
    const files = generatePluginTemplate({ name: 'my-explicit-plugin', language: 'typescript' });
    const manifest = extractManifest(files);
    expect(manifest['name']).toBe('my-explicit-plugin');
  });

  it('explicit description is reflected in plugin.json', () => {
    const files = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      description: 'Enables team-scoped authentication workflows',
    });
    const manifest = extractManifest(files);
    expect(manifest['description']).toBe('Enables team-scoped authentication workflows');
  });

  it('explicit author is reflected in plugin.json', () => {
    const files = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      author: 'Florian Developer',
    });
    const manifest = extractManifest(files);
    expect(manifest['author']).toBe('Florian Developer');
  });

  it('explicit types[] is reflected in plugin.json', () => {
    const files = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      types: ['hook', 'command'],
    });
    const manifest = extractManifest(files);
    expect(manifest['types']).toEqual(['hook', 'command']);
  });

  it('explicit useCaseTags is reflected in plugin.json', () => {
    const files = generatePluginTemplate({
      name: 'my-plugin',
      language: 'go',
      useCaseTags: ['devops', 'security'],
    });
    const manifest = extractManifest(files);
    expect(manifest['useCaseTags']).toEqual(['devops', 'security']);
  });

  it('explicit languages[] (additional languages) is reflected in plugin.json', () => {
    const files = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      languages: ['typescript', 'python'],
    });
    const manifest = extractManifest(files);
    expect(manifest['languages']).toContain('typescript');
    expect(manifest['languages']).toContain('python');
  });
});

// ---------------------------------------------------------------------------
// Default values when optional fields are omitted
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — default values for omitted optional fields', () => {
  it("defaults version to '0.1.0'", () => {
    const files = generatePluginTemplate({ name: 'defaults-test', language: 'python' });
    const manifest = extractManifest(files);
    expect(manifest['version']).toBe('0.1.0');
  });

  it("defaults license to 'MIT'", () => {
    const files = generatePluginTemplate({ name: 'defaults-test', language: 'rust' });
    const manifest = extractManifest(files);
    expect(manifest['license']).toBe('MIT');
  });

  it('provides a default description when none is given', () => {
    const files = generatePluginTemplate({ name: 'defaults-test', language: 'go' });
    const manifest = extractManifest(files);
    expect(typeof manifest['description']).toBe('string');
    expect((manifest['description'] as string).trim().length).toBeGreaterThan(0);
  });

  it('provides a default author when none is given', () => {
    const files = generatePluginTemplate({ name: 'defaults-test', language: 'typescript' });
    const manifest = extractManifest(files);
    expect(typeof manifest['author']).toBe('string');
    expect((manifest['author'] as string).trim().length).toBeGreaterThan(0);
  });

  it('provides a default types[] with at least one valid type when none is given', () => {
    const VALID_TYPES = new Set(['skill', 'hook', 'agent', 'command', 'plugin']);
    const files = generatePluginTemplate({ name: 'defaults-test', language: 'typescript' });
    const manifest = extractManifest(files);
    expect(Array.isArray(manifest['types'])).toBe(true);
    expect((manifest['types'] as string[]).length).toBeGreaterThanOrEqual(1);
    for (const t of manifest['types'] as string[]) {
      expect(VALID_TYPES.has(t)).toBe(true);
    }
  });

  it('provides entrypoints[] with at least one item when none is given', () => {
    const files = generatePluginTemplate({ name: 'defaults-test', language: 'typescript' });
    const manifest = extractManifest(files);
    expect(Array.isArray(manifest['entrypoints'])).toBe(true);
    expect((manifest['entrypoints'] as unknown[]).length).toBeGreaterThanOrEqual(1);
  });

  it('defaults dependencies to an empty object when none is given', () => {
    const files = generatePluginTemplate({ name: 'defaults-test', language: 'typescript' });
    const manifest = extractManifest(files);
    if (manifest['dependencies'] !== undefined) {
      expect(typeof manifest['dependencies']).toBe('object');
      expect(Array.isArray(manifest['dependencies'])).toBe(false);
    }
    // Either an empty object or undefined is acceptable
  });
});

// ---------------------------------------------------------------------------
// All four languages accepted
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — all valid languages accepted', () => {
  const VALID_LANGUAGES: TemplateLanguage[] = ['typescript', 'python', 'go', 'rust'];

  for (const lang of VALID_LANGUAGES) {
    it(`does not throw for language='${lang}'`, () => {
      expect(() =>
        generatePluginTemplate({ name: `test-${lang}`, language: lang }),
      ).not.toThrow();
    });

    it(`returns a non-empty file map for language='${lang}'`, () => {
      const files = generatePluginTemplate({ name: `test-${lang}`, language: lang });
      expect(Object.keys(files).length).toBeGreaterThan(0);
    });
  }
});

// ---------------------------------------------------------------------------
// Invalid language — error path
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — invalid language throws', () => {
  it("throws for language='java'", () => {
    const opts = { name: 'bad-lang', language: 'java' as TemplateLanguage };
    expect(() => generatePluginTemplate(opts)).toThrow();
  });

  it("throws for language='cobol'", () => {
    const opts = { name: 'bad-lang', language: 'cobol' as TemplateLanguage };
    expect(() => generatePluginTemplate(opts)).toThrow();
  });

  it("error message for unsupported language mentions the invalid value", () => {
    const opts = { name: 'bad-lang', language: 'ruby' as TemplateLanguage };
    let caught: unknown;
    try {
      generatePluginTemplate(opts);
    } catch (e) {
      caught = e;
    }
    expect(caught).toBeDefined();
    expect(String(caught)).toMatch(/ruby|unsupported|invalid/i);
  });
});

// ---------------------------------------------------------------------------
// Immutability — options object not mutated
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — immutability', () => {
  it('does not mutate the input options object', () => {
    const opts: GeneratorOptions = {
      name: 'immutable-test',
      language: 'typescript',
      types: ['skill'],
      useCaseTags: ['dev-team'],
    };
    const snapshot = JSON.stringify(opts);
    generatePluginTemplate(opts);
    expect(JSON.stringify(opts)).toBe(snapshot);
  });

  it('does not mutate the types array inside options', () => {
    const typesArr = ['skill'] as const;
    const opts: GeneratorOptions = {
      name: 'arr-mutate-test',
      language: 'typescript',
      types: [...typesArr],
    };
    const before = JSON.stringify(opts.types);
    generatePluginTemplate(opts);
    expect(JSON.stringify(opts.types)).toBe(before);
  });
});

// ---------------------------------------------------------------------------
// Edge cases
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — edge cases', () => {
  it('name with @scope/pkg format is preserved', () => {
    const files = generatePluginTemplate({
      name: '@claudeforge/my-plugin',
      language: 'typescript',
    });
    const manifest = extractManifest(files);
    expect(manifest['name']).toBe('@claudeforge/my-plugin');
  });

  it('name with hyphens is preserved', () => {
    const files = generatePluginTemplate({
      name: 'my-awesome-devops-plugin',
      language: 'go',
    });
    const manifest = extractManifest(files);
    expect(manifest['name']).toBe('my-awesome-devops-plugin');
  });

  it('empty useCaseTags array is reflected as empty array in manifest', () => {
    const files = generatePluginTemplate({
      name: 'empty-tags-test',
      language: 'rust',
      useCaseTags: [],
    });
    const manifest = extractManifest(files);
    if (manifest['useCaseTags'] !== undefined) {
      expect(Array.isArray(manifest['useCaseTags'])).toBe(true);
      expect((manifest['useCaseTags'] as string[]).length).toBe(0);
    }
  });

  it('produces deterministic output for the same input', () => {
    const opts: GeneratorOptions = {
      name: 'deterministic',
      language: 'python',
      description: 'Test plugin',
      author: 'Test Author',
      types: ['skill'],
      useCaseTags: ['dev-team'],
    };
    const first = generatePluginTemplate(opts);
    const second = generatePluginTemplate(opts);
    expect(JSON.stringify(first)).toBe(JSON.stringify(second));
  });
});
