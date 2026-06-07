/**
 * Domain port for persisting the user's selected language.
 * Stub — to be replaced by coder (GREEN step).
 */

import type { Lang } from './active-language';

export abstract class LanguageStoragePort {
  static readonly STORAGE_KEY = 'cf.lang';

  abstract read(): Lang | null;
  abstract write(lang: Lang): void;
}
