/**
 * Spec 2 — language-detection.ts (pure, no Angular)
 *
 * RED: stubs throw "Not implemented" — all tests below will FAIL.
 *
 * GREEN contract for the coder:
 *
 *   parseAcceptLanguage(header: string | null): readonly string[]
 *     - null/empty string → []
 *     - "fr" → ["fr"]
 *     - "fr-FR,fr;q=0.9,en;q=0.8" → ["fr-FR","fr","en"] (q-value order, highest first)
 *     - "en;q=0.5,fr;q=0.9" → ["fr","en"] (q-value sorting)
 *     - "*" wildcard entry must be omitted from results
 *     - case is preserved in output (normalisation is done by pickLanguage)
 *
 *   pickLanguage(candidates: readonly string[], available: readonly Lang[], fallback: Lang): Lang
 *     - "en-US" candidate → matches "en" (strip region suffix)
 *     - "fr-FR" candidate → matches "fr"
 *     - "de" (unsupported) with no match → fallback "en"
 *     - empty candidates → fallback
 *     - case-insensitive matching ("EN" → "en")
 *     - first candidate that matches available wins
 */

import { parseAcceptLanguage, pickLanguage } from './language-detection';
import type { Lang } from './active-language';
import { LANG_VALUES } from './active-language';

// ---------------------------------------------------------------------------
// parseAcceptLanguage
// ---------------------------------------------------------------------------

describe('parseAcceptLanguage', () => {
  it('returns [] for null header', () => {
    expect(parseAcceptLanguage(null)).toEqual([]);
  });

  it('returns [] for empty string header', () => {
    expect(parseAcceptLanguage('')).toEqual([]);
  });

  it('returns [] for whitespace-only string', () => {
    expect(parseAcceptLanguage('   ')).toEqual([]);
  });

  it('returns single language for "fr"', () => {
    expect(parseAcceptLanguage('fr')).toEqual(['fr']);
  });

  it('returns single language for "en"', () => {
    expect(parseAcceptLanguage('en')).toEqual(['en']);
  });

  it('parses "fr-FR,fr;q=0.9,en;q=0.8" in q-value order', () => {
    const result = parseAcceptLanguage('fr-FR,fr;q=0.9,en;q=0.8');
    expect(result[0]).toBe('fr-FR');
    expect(result[1]).toBe('fr');
    expect(result[2]).toBe('en');
  });

  it('sorts by descending q-value: "en;q=0.5,fr;q=0.9" → fr first', () => {
    const result = parseAcceptLanguage('en;q=0.5,fr;q=0.9');
    expect(result[0]).toBe('fr');
    expect(result[1]).toBe('en');
  });

  it('omits wildcard "*" entries', () => {
    const result = parseAcceptLanguage('fr,*;q=0.1');
    expect(result).not.toContain('*');
  });

  it('handles languages with q=1.0 (explicit) before those with lower q', () => {
    const result = parseAcceptLanguage('de;q=1.0,en;q=0.8,fr;q=0.5');
    expect(result[0]).toBe('de');
    expect(result[1]).toBe('en');
    expect(result[2]).toBe('fr');
  });

  it('returns a readonly array (no mutation on the result)', () => {
    const result = parseAcceptLanguage('en,fr');
    // TypeScript ensures readonly — runtime: just assert it is array-like
    expect(Array.isArray(result)).toBe(true);
  });

  it('returns result with exactly 1 entry for a single lang', () => {
    expect(parseAcceptLanguage('en')).toHaveLength(1);
  });

  it('trims whitespace around language tags', () => {
    const result = parseAcceptLanguage(' en , fr ');
    expect(result).toContain('en');
    expect(result).toContain('fr');
  });
});

// ---------------------------------------------------------------------------
// pickLanguage
// ---------------------------------------------------------------------------

describe('pickLanguage', () => {
  const available: readonly Lang[] = LANG_VALUES; // ['en', 'fr']

  it('returns fallback for empty candidates', () => {
    expect(pickLanguage([], available, 'en')).toBe('en');
  });

  it('returns fallback when no candidates match available', () => {
    expect(pickLanguage(['de', 'es', 'it'], available, 'en')).toBe('en');
  });

  it('returns "fr" when "fr" is the first matching candidate', () => {
    expect(pickLanguage(['fr'], available, 'en')).toBe('fr');
  });

  it('returns "en" when "en" is the first matching candidate', () => {
    expect(pickLanguage(['en'], available, 'fr')).toBe('en');
  });

  it('normalises "fr-FR" → "fr" (region suffix stripped)', () => {
    expect(pickLanguage(['fr-FR'], available, 'en')).toBe('fr');
  });

  it('normalises "en-US" → "en"', () => {
    expect(pickLanguage(['en-US'], available, 'fr')).toBe('en');
  });

  it('normalises "en-GB" → "en"', () => {
    expect(pickLanguage(['en-GB'], available, 'fr')).toBe('en');
  });

  it('normalises "fr-BE" → "fr"', () => {
    expect(pickLanguage(['fr-BE'], available, 'en')).toBe('fr');
  });

  it('is case-insensitive: "FR" → "fr"', () => {
    expect(pickLanguage(['FR'], available, 'en')).toBe('fr');
  });

  it('is case-insensitive: "EN" → "en"', () => {
    expect(pickLanguage(['EN'], available, 'fr')).toBe('en');
  });

  it('picks first match from ordered candidates: "de,fr,en" → "fr"', () => {
    expect(pickLanguage(['de', 'fr', 'en'], available, 'en')).toBe('fr');
  });

  it('real Accept-Language value "fr-FR,fr;q=0.9,en;q=0.8" → "fr"', () => {
    const candidates = ['fr-FR', 'fr', 'en'];
    expect(pickLanguage(candidates, available, 'en')).toBe('fr');
  });

  it('"de" (unsupported) → returns "en" fallback', () => {
    expect(pickLanguage(['de'], available, 'en')).toBe('en');
  });

  it('fallback "fr" is returned when nothing matches', () => {
    expect(pickLanguage(['zh', 'ja'], available, 'fr')).toBe('fr');
  });

  it('returns a Lang (one of the LANG_VALUES)', () => {
    const result = pickLanguage(['fr'], available, 'en');
    expect(LANG_VALUES).toContain(result);
  });
});
