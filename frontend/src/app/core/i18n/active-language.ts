/**
 * Active language constants for the i18n foundation.
 * Stub — to be replaced by coder (GREEN step).
 */

export const LANG_VALUES = ['en', 'fr'] as const;

export type Lang = (typeof LANG_VALUES)[number];

export const DEFAULT_LANG: Lang = 'en';
