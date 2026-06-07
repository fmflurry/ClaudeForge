/**
 * FilterChipsComponent — render tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { FilterChipsComponent } from './filter-chips.component';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for search scope (Wave 1 i18n)
// ---------------------------------------------------------------------------

const EN_SEARCH_LANGS: Record<string, string> = {
  'search.search-input-placeholder': 'Search plugins…',
  'search.search-button': 'Search',
  'search.loading-results': 'Loading results…',
  'search.error-message': 'Failed to load search results. Please try again.',
  'search.no-results-with-suggestions': 'No results found. Try one of these categories:',
  'search.no-results': 'No results found. Try a different search term.',
};

const FR_SEARCH_LANGS: Record<string, string> = {
  'search.search-input-placeholder': 'Rechercher des plugins…',
  'search.search-button': 'Rechercher',
  'search.loading-results': 'Chargement des résultats…',
  'search.error-message': 'Impossible de charger les résultats. Veuillez réessayer.',
  'search.no-results-with-suggestions': "Aucun résultat. Essayez l'une de ces catégories :",
  'search.no-results': 'Aucun résultat. Essayez un autre terme de recherche.',
};

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(
  opts: {
    activeTypes?: string[];
    activeLanguages?: string[];
    activeUseCases?: string[];
  } = {},
): { fixture: ComponentFixture<FilterChipsComponent>; translocoService: TranslocoService } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      FilterChipsComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_SEARCH_LANGS, fr: FR_SEARCH_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [I18nFacade, { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } }],
  }).overrideComponent(FilterChipsComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(FilterChipsComponent);
  if (opts.activeTypes !== undefined) {
    fixture.componentRef.setInput('activeTypes', opts.activeTypes);
  }
  if (opts.activeLanguages !== undefined) {
    fixture.componentRef.setInput('activeLanguages', opts.activeLanguages);
  }
  if (opts.activeUseCases !== undefined) {
    fixture.componentRef.setInput('activeUseCases', opts.activeUseCases);
  }
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, translocoService };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('FilterChipsComponent — render', () => {
  it('should instantiate', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render the filter-chips container', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-filter-chips')).not.toBeNull();
  });

  it('should render type chips when activeTypes has values', () => {
    const { fixture } = setup({ activeTypes: ['formatter', 'linter'] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-type"]'));
    expect(chips).toHaveLength(2);
  });

  it('should render language chips when activeLanguages has values', () => {
    const { fixture } = setup({ activeLanguages: ['typescript', 'python'] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-language"]'));
    expect(chips).toHaveLength(2);
  });

  it('should render use-case chips when activeUseCases has values', () => {
    const { fixture } = setup({ activeUseCases: ['code-quality'] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-usecase"]'));
    expect(chips).toHaveLength(1);
  });

  it('should display chip text content', () => {
    const { fixture } = setup({ activeTypes: ['formatter'] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('formatter');
  });

  it('should render no type chips when activeTypes is empty', () => {
    const { fixture } = setup({ activeTypes: [] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-type"]'));
    expect(chips).toHaveLength(0);
  });

  it('should render chips from all three categories simultaneously', () => {
    const { fixture } = setup({
      activeTypes: ['formatter'],
      activeLanguages: ['typescript'],
      activeUseCases: ['code-quality'],
    });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('formatter');
    expect(el.textContent).toContain('typescript');
    expect(el.textContent).toContain('code-quality');
  });

  it('should expose filtersChanged output', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance.filtersChanged).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// i18n — FR language rendering
// ---------------------------------------------------------------------------

describe('FilterChipsComponent — i18n FR language rendering', () => {
  it('[FR] chip data values are still rendered correctly when lang is fr', () => {
    const { fixture, translocoService } = setup({ activeTypes: ['formatter'] });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    // Data values (plugin categories) are not translated — they are brand values
    expect(el.textContent).toContain('formatter');
  });
});
