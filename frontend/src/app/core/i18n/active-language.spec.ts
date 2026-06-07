/**
 * Spec 1 — active-language.ts constants
 *
 * These assertions are trivial but pin the contract so renames are caught.
 * All assertions are currently GREEN because the stub exports the constants.
 * The coder must preserve these exact shapes.
 */

import { LANG_VALUES, DEFAULT_LANG } from './active-language';
import type { Lang } from './active-language';

describe('active-language constants', () => {
  it('LANG_VALUES contains exactly "en" and "fr"', () => {
    expect(LANG_VALUES).toEqual(['en', 'fr']);
  });

  it('LANG_VALUES has length 2', () => {
    expect(LANG_VALUES).toHaveLength(2);
  });

  it('LANG_VALUES is readonly (as const)', () => {
    // tuple type check — both members must be present
    const values: readonly ['en', 'fr'] = LANG_VALUES;
    expect(values).toBeDefined();
  });

  it('DEFAULT_LANG is "en"', () => {
    expect(DEFAULT_LANG).toBe('en');
  });

  it('DEFAULT_LANG is assignable to Lang type', () => {
    const lang: Lang = DEFAULT_LANG;
    expect(lang).toBe('en');
  });

  it('"en" is a valid Lang value', () => {
    const lang: Lang = 'en';
    expect(LANG_VALUES).toContain(lang);
  });

  it('"fr" is a valid Lang value', () => {
    const lang: Lang = 'fr';
    expect(LANG_VALUES).toContain(lang);
  });
});
