/**
 * Tests for manifest generation (Task 19.4)
 *
 * Production module: src/generator.ts
 * Exported API:
 *   - generatePluginTemplate(options: GeneratorOptions): GeneratedFileMap
 *   - GeneratorOptions: {
 *       name: string;
 *       language: TemplateLanguage;
 *       description?: string;
 *       author?: string;
 *       types?: PluginType[];
 *       useCaseTags?: UseCaseTag[];
 *       version?: string;
 *     }
 *   - GeneratedFileMap: Record<string, string>  (relative path → file content)
 *   - TemplateLanguage = 'typescript' | 'python' | 'go' | 'rust'
 *   - PluginType = 'skill' | 'hook' | 'agent' | 'command' | 'plugin'
 *   - UseCaseTag = 'dev-team' | 'product-owner' | 'product-manager' | 'devops' | 'security' | 'data-analyst'
 *
 * Canonical manifest schema validated against the same rules as the CLI
 * (cli/src/commands/validate.ts → validateManifest).
 */

import { describe, it, expect } from 'vitest';

// These imports will FAIL until src/generator.ts is created (RED).
import {
  generatePluginTemplate,
} from '../src/generator.js';
import type {
  GeneratorOptions,
  GeneratedFileMap,
  TemplateLanguage,
  PluginType,
  UseCaseTag,
} from '../src/generator.js';

// ---------------------------------------------------------------------------
// Local mirror of validateManifest rules (does NOT import from cli/ — the
// template package is standalone; we mirror the canonical rules here).
// See: cli/src/commands/validate.ts
// ---------------------------------------------------------------------------

const VALID_TYPES = new Set<string>(['skill', 'hook', 'agent', 'command', 'plugin']);
const VALID_LANGUAGES = new Set<string>(['typescript', 'python', 'go', 'rust']);
const VALID_USE_CASE_TAGS = new Set<string>([
  'dev-team',
  'product-owner',
  'product-manager',
  'devops',
  'security',
  'data-analyst',
]);
const SEMVER_RE = /^\d+\.\d+\.\d+([+-][a-zA-Z0-9._+-]*)?$/;

interface CanonicalManifest {
  name: string;
  version: string;
  description: string;
  author: string;
  types: string[];
  languages: string[];
  entrypoints: Array<{ name: string; description: string; signature: string }> | string[];
  useCaseTags?: string[];
  dependencies?: Record<string, string>;
  license?: string;
  docsUrl?: string;
}

interface LocalValidationResult {
  valid: boolean;
  errors: string[];
}

function validateCanonicalManifest(manifest: unknown): LocalValidationResult {
  const errors: string[] = [];

  if (manifest === null || typeof manifest !== 'object') {
    return { valid: false, errors: ['Manifest must be an object'] };
  }

  const m = manifest as Record<string, unknown>;

  // Required string fields
  for (const field of ['name', 'version', 'description', 'author']) {
    if (!m[field] || typeof m[field] !== 'string' || (m[field] as string).trim() === '') {
      errors.push(`Missing required field: ${field}`);
    }
  }

  // types[] — required, >=1, all from enum
  if (!Array.isArray(m['types']) || (m['types'] as string[]).length === 0) {
    errors.push('Missing required field: types');
  } else {
    const invalid = (m['types'] as string[]).filter((t) => !VALID_TYPES.has(t));
    if (invalid.length > 0) {
      errors.push(`Invalid type value(s): ${invalid.join(', ')}`);
    }
  }

  // languages[] — required, >=1
  if (!Array.isArray(m['languages']) || (m['languages'] as string[]).length === 0) {
    errors.push('Missing required field: languages');
  } else {
    const invalid = (m['languages'] as string[]).filter((l) => !VALID_LANGUAGES.has(l));
    if (invalid.length > 0) {
      errors.push(`Invalid language value(s): ${invalid.join(', ')}`);
    }
  }

  // entrypoints[] — required, >=1
  if (!Array.isArray(m['entrypoints']) || (m['entrypoints'] as unknown[]).length === 0) {
    errors.push('Missing required field: entrypoints');
  }

  // semver validation
  if (typeof m['version'] === 'string' && m['version'].trim() !== '') {
    if (!SEMVER_RE.test(m['version'])) {
      errors.push(`Invalid version: "${m['version']}" is not valid semver`);
    }
  }

  // useCaseTags — optional but if present, must be valid
  if (m['useCaseTags'] !== undefined) {
    if (!Array.isArray(m['useCaseTags'])) {
      errors.push('useCaseTags must be an array');
    } else {
      const invalid = (m['useCaseTags'] as string[]).filter((t) => !VALID_USE_CASE_TAGS.has(t));
      if (invalid.length > 0) {
        errors.push(`Invalid useCaseTag value(s): ${invalid.join(', ')}`);
      }
    }
  }

  // dependencies — optional but if present, must be an object
  if (m['dependencies'] !== undefined && (typeof m['dependencies'] !== 'object' || Array.isArray(m['dependencies']))) {
    errors.push('dependencies must be an object');
  }

  return { valid: errors.length === 0, errors };
}

// ---------------------------------------------------------------------------
// Helper: extract parsed plugin.json from a GeneratedFileMap
// ---------------------------------------------------------------------------

function extractManifest(files: GeneratedFileMap): CanonicalManifest {
  const manifestKey = Object.keys(files).find((k) => k === 'plugin.json' || k.endsWith('/plugin.json'));
  if (!manifestKey) {
    throw new Error(`plugin.json not found in generated files. Keys: ${Object.keys(files).join(', ')}`);
  }
  return JSON.parse(files[manifestKey]) as CanonicalManifest;
}

// ---------------------------------------------------------------------------
// 19.4 — Manifest generation: canonical schema compliance
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — manifest schema compliance', () => {
  const ALL_LANGUAGES: TemplateLanguage[] = ['typescript', 'python', 'go', 'rust'];

  for (const language of ALL_LANGUAGES) {
    describe(`language = ${language}`, () => {
      it('generates a plugin.json that parses as valid JSON', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifestKey = Object.keys(files).find((k) => k === 'plugin.json' || k.endsWith('/plugin.json'));
        expect(manifestKey).toBeDefined();
        expect(() => JSON.parse(files[manifestKey as string])).not.toThrow();
      });

      it('generated plugin.json passes canonical manifest validation', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        const result = validateCanonicalManifest(manifest);
        expect(result.errors).toEqual([]);
        expect(result.valid).toBe(true);
      });

      it('manifest name matches the provided name', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `my-${language}-plugin`,
          language,
        });
        const manifest = extractManifest(files);
        expect(manifest.name).toBe(`my-${language}-plugin`);
      });

      it(`manifest languages[] includes '${language}'`, () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(Array.isArray(manifest.languages)).toBe(true);
        expect(manifest.languages).toContain(language);
      });

      it('manifest types[] has >=1 valid type from the canonical enum', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(Array.isArray(manifest.types)).toBe(true);
        expect((manifest.types as string[]).length).toBeGreaterThanOrEqual(1);
        for (const t of manifest.types) {
          expect(VALID_TYPES.has(t)).toBe(true);
        }
      });

      it("manifest version defaults to '0.1.0'", () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(manifest.version).toBe('0.1.0');
      });

      it("manifest license defaults to 'MIT'", () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(manifest.license).toBe('MIT');
      });

      it('manifest entrypoints[] is a non-empty array', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(Array.isArray(manifest.entrypoints)).toBe(true);
        expect((manifest.entrypoints as unknown[]).length).toBeGreaterThanOrEqual(1);
      });

      it('manifest dependencies is an object (not an array)', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        if (manifest.dependencies !== undefined) {
          expect(typeof manifest.dependencies).toBe('object');
          expect(Array.isArray(manifest.dependencies)).toBe(false);
        }
      });

      it('manifest version is valid semver', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(SEMVER_RE.test(manifest.version)).toBe(true);
      });

      it('manifest has non-empty description field', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(typeof manifest.description).toBe('string');
        expect(manifest.description.trim().length).toBeGreaterThan(0);
      });

      it('manifest has non-empty author field', () => {
        const files: GeneratedFileMap = generatePluginTemplate({
          name: `test-plugin-${language}`,
          language,
        });
        const manifest = extractManifest(files);
        expect(typeof manifest.author).toBe('string');
        expect(manifest.author.trim().length).toBeGreaterThan(0);
      });
    });
  }

  it('manifest reflects explicit description when provided', () => {
    const files: GeneratedFileMap = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      description: 'Handles authentication flows for Claude agents',
    });
    const manifest = extractManifest(files);
    expect(manifest.description).toBe('Handles authentication flows for Claude agents');
  });

  it('manifest reflects explicit author when provided', () => {
    const files: GeneratedFileMap = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      author: 'Jane Developer',
    });
    const manifest = extractManifest(files);
    expect(manifest.author).toBe('Jane Developer');
  });

  it('manifest reflects explicit types[] when provided', () => {
    const explicitTypes: PluginType[] = ['skill', 'hook'];
    const files: GeneratedFileMap = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      types: explicitTypes,
    });
    const manifest = extractManifest(files);
    expect(manifest.types).toEqual(explicitTypes);
  });

  it('manifest reflects explicit useCaseTags[] when provided', () => {
    const tags: UseCaseTag[] = ['dev-team', 'devops'];
    const files: GeneratedFileMap = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      useCaseTags: tags,
    });
    const manifest = extractManifest(files);
    expect(manifest.useCaseTags).toEqual(tags);
    // all tags must be from valid controlled vocab
    for (const tag of manifest.useCaseTags ?? []) {
      expect(VALID_USE_CASE_TAGS.has(tag)).toBe(true);
    }
  });

  it('manifest useCaseTags defaults to an empty array or is omitted when not provided', () => {
    const files: GeneratedFileMap = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
    });
    const manifest = extractManifest(files);
    // useCaseTags may be omitted or empty — both are valid
    if (manifest.useCaseTags !== undefined) {
      expect(Array.isArray(manifest.useCaseTags)).toBe(true);
    }
  });

  it('manifest reflects explicit languages[] when provided', () => {
    const files: GeneratedFileMap = generatePluginTemplate({
      name: 'my-plugin',
      language: 'typescript',
      languages: ['typescript', 'python'],
    });
    const manifest = extractManifest(files);
    expect(manifest.languages).toContain('typescript');
    expect(manifest.languages).toContain('python');
  });
});

// ---------------------------------------------------------------------------
// 19.4 — GeneratedFileMap shape and type-safety
// ---------------------------------------------------------------------------

describe('generatePluginTemplate — return type', () => {
  it('returns a plain Record<string, string> (file map)', () => {
    const files: GeneratedFileMap = generatePluginTemplate({
      name: 'shape-test',
      language: 'typescript',
    });
    expect(typeof files).toBe('object');
    expect(files).not.toBeNull();
    // All values must be strings
    for (const [key, value] of Object.entries(files)) {
      expect(typeof key).toBe('string');
      expect(typeof value).toBe('string');
    }
  });

  it('is a pure function — two calls with identical options produce identical output', () => {
    const opts: GeneratorOptions = { name: 'pure-test', language: 'go' };
    const first: GeneratedFileMap = generatePluginTemplate(opts);
    const second: GeneratedFileMap = generatePluginTemplate(opts);
    expect(Object.keys(first).sort()).toEqual(Object.keys(second).sort());
    for (const key of Object.keys(first)) {
      expect(first[key]).toBe(second[key]);
    }
  });

  it('does not mutate the options object', () => {
    const opts: GeneratorOptions = { name: 'no-mutate', language: 'rust' };
    const before = JSON.stringify(opts);
    generatePluginTemplate(opts);
    expect(JSON.stringify(opts)).toBe(before);
  });
});
