/**
 * Spec 4 — i18n.facade.ts
 *
 * RED: stub throws "Not implemented" for setLanguage() — tests will FAIL.
 *
 * GREEN contract for the coder:
 *
 *   @Injectable()
 *   export class I18nFacade {
 *     private readonly transloco = inject(TranslocoService);
 *     private readonly storage = inject(LanguageStoragePort);
 *
 *     private readonly _activeLang = signal<Lang>(DEFAULT_LANG);
 *
 *     readonly activeLang: Signal<Lang>  — readonly signal, reflects transloco.activeLang()
 *     readonly availableLangs: readonly Lang[] = LANG_VALUES;
 *
 *     setLanguage(lang: Lang): void
 *       — calls this.transloco.setActiveLang(lang)
 *       — calls this.storage.write(lang)
 *       — updates the internal signal so activeLang() reflects lang
 *   }
 *
 *   LanguageStoragePort must be provided (inject token).
 *   TranslocoService is provided via TranslocoTestingModule.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { vi } from 'vitest';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { I18nFacade } from './i18n.facade';
import { LanguageStoragePort } from '../../core/i18n/language-storage.port';
import { LANG_VALUES, DEFAULT_LANG } from '../../core/i18n/active-language';
import type { Lang } from '../../core/i18n/active-language';

// ---------------------------------------------------------------------------
// Stub LanguageStoragePort
// ---------------------------------------------------------------------------

@Injectable()
class SpyLanguageStoragePort extends LanguageStoragePort {
  private stored: Lang | null = null;

  readonly writeSpy = vi.fn((lang: Lang) => {
    this.stored = lang;
  });

  override read(): Lang | null {
    return this.stored;
  }

  override write(lang: Lang): void {
    this.writeSpy(lang);
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setup(): { facade: I18nFacade; storageSpy: SpyLanguageStoragePort } {
  const storageSpy = new SpyLanguageStoragePort();

  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: {
          en: { 'language-switcher.en': 'English', 'language-switcher.fr': 'French' },
          fr: { 'language-switcher.en': 'Anglais', 'language-switcher.fr': 'Français' },
        },
        translocoConfig: {
          defaultLang: 'en',
          availableLangs: ['en', 'fr'],
        },
        preloadLangs: true,
      }),
    ],
    providers: [I18nFacade, { provide: LanguageStoragePort, useValue: storageSpy }],
  });

  return { facade: TestBed.inject(I18nFacade), storageSpy };
}

// ---------------------------------------------------------------------------
// API surface
// ---------------------------------------------------------------------------

describe('I18nFacade — API surface', () => {
  it('activeLang is a callable Signal function', () => {
    const { facade } = setup();
    expect(typeof facade.activeLang).toBe('function');
  });

  it('availableLangs equals LANG_VALUES', () => {
    const { facade } = setup();
    expect(facade.availableLangs).toEqual(LANG_VALUES);
  });

  it('setLanguage is a method', () => {
    const { facade } = setup();
    expect(typeof facade.setLanguage).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

describe('I18nFacade — initial state', () => {
  it('activeLang() defaults to DEFAULT_LANG ("en")', () => {
    const { facade } = setup();
    expect(facade.activeLang()).toBe(DEFAULT_LANG);
  });
});

// ---------------------------------------------------------------------------
// setLanguage — side effects
// ---------------------------------------------------------------------------

describe('I18nFacade — setLanguage("fr")', () => {
  it('updates activeLang signal to "fr"', async () => {
    const { facade } = setup();
    await facade.setLanguage('fr');
    expect(facade.activeLang()).toBe('fr');
  });

  it('calls storage.write("fr")', async () => {
    const { facade, storageSpy } = setup();
    await facade.setLanguage('fr');
    expect(storageSpy.writeSpy).toHaveBeenCalledWith('fr');
  });

  it('calls storage.write exactly once per setLanguage call', async () => {
    const { facade, storageSpy } = setup();
    await facade.setLanguage('fr');
    expect(storageSpy.writeSpy).toHaveBeenCalledTimes(1);
  });
});

describe('I18nFacade — setLanguage("en") after "fr"', () => {
  it('updates activeLang signal back to "en"', async () => {
    const { facade } = setup();
    await facade.setLanguage('fr');
    await facade.setLanguage('en');
    expect(facade.activeLang()).toBe('en');
  });

  it('calls storage.write twice (once per setLanguage call)', async () => {
    const { facade, storageSpy } = setup();
    await facade.setLanguage('fr');
    await facade.setLanguage('en');
    expect(storageSpy.writeSpy).toHaveBeenCalledTimes(2);
  });

  it('last storage.write call was with "en"', async () => {
    const { facade, storageSpy } = setup();
    await facade.setLanguage('fr');
    await facade.setLanguage('en');
    expect(storageSpy.writeSpy).toHaveBeenLastCalledWith('en');
  });
});

// ---------------------------------------------------------------------------
// setLanguage — TranslocoService integration
// ---------------------------------------------------------------------------

describe('I18nFacade — TranslocoService integration', () => {
  it('does not throw when setLanguage is called with valid lang', async () => {
    const { facade } = setup();
    await expect(facade.setLanguage('fr')).resolves.not.toThrow();
  });

  it('activeLang() is a readonly Signal (not writable)', () => {
    const { facade } = setup();
    // Signal<Lang> should only have the call signature — not a set() method
    expect(typeof (facade.activeLang as unknown as { set?: unknown }).set).not.toBe('function');
  });
});

// ---------------------------------------------------------------------------
// availableLangs immutability
// ---------------------------------------------------------------------------

describe('I18nFacade — availableLangs', () => {
  it('contains "en" and "fr"', () => {
    const { facade } = setup();
    expect(facade.availableLangs).toContain('en');
    expect(facade.availableLangs).toContain('fr');
  });

  it('has length 2', () => {
    const { facade } = setup();
    expect(facade.availableLangs).toHaveLength(2);
  });
});
