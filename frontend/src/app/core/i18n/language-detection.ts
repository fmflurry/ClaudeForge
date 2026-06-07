/**
 * Pure (no-Angular) language detection utilities.
 * parseAcceptLanguage: parses Accept-Language header into sorted language list.
 * pickLanguage: matches candidates against available langs, normalizing region variants.
 */

import type { Lang } from './active-language';

interface LangEntry {
  readonly tag: string;
  readonly q: number;
}

/**
 * Parses an HTTP Accept-Language header value into an ordered list of language tags
 * (highest quality-value first). Wildcards ('*') are omitted. Returns [] for null/empty.
 */
export function parseAcceptLanguage(header: string | null): readonly string[] {
  if (!header || !header.trim()) {
    return [];
  }

  const entries: LangEntry[] = header
    .split(',')
    .map((part): LangEntry | null => {
      const trimmed = part.trim();
      if (!trimmed) return null;

      const semicolonIndex = trimmed.indexOf(';');
      if (semicolonIndex === -1) {
        const tag = trimmed.trim();
        return tag ? { tag, q: 1.0 } : null;
      }

      const tag = trimmed.slice(0, semicolonIndex).trim();
      const qualityPart = trimmed.slice(semicolonIndex + 1).trim();
      const q = qualityPart.startsWith('q=') ? parseFloat(qualityPart.slice(2)) : 1.0;
      return tag ? { tag, q: isNaN(q) ? 1.0 : q } : null;
    })
    .filter((entry): entry is LangEntry => entry !== null && entry.tag !== '*');

  return entries.sort((a, b) => b.q - a.q).map((e) => e.tag);
}

/**
 * Picks the first candidate that matches an available language, normalizing
 * region suffixes (e.g. 'fr-FR' → 'fr') and case. Returns fallback if no match.
 */
export function pickLanguage(candidates: readonly string[], available: readonly Lang[], fallback: Lang): Lang {
  for (const candidate of candidates) {
    const normalized = candidate.toLowerCase().split('-')[0] as Lang;

    const match = available.find((lang) => lang.toLowerCase() === normalized);
    if (match !== undefined) {
      return match;
    }
  }

  return fallback;
}
