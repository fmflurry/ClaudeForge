/**
 * SearchBarComponent — render + action wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { SearchBarComponent } from './search-bar.component';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for search scope (Wave 1 i18n)
// En map returns EXACT current literals so all existing assertions stay green.
// Fr map returns French — fr assertions confirm reactive translation.
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
    initialKeyword?: string;
    isLoading?: boolean;
  } = {},
): { fixture: ComponentFixture<SearchBarComponent>; translocoService: TranslocoService } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      SearchBarComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_SEARCH_LANGS, fr: FR_SEARCH_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [I18nFacade, { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } }],
  }).overrideComponent(SearchBarComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(SearchBarComponent);
  if (opts.initialKeyword !== undefined) {
    fixture.componentRef.setInput('initialKeyword', opts.initialKeyword);
  }
  if (opts.isLoading !== undefined) {
    fixture.componentRef.setInput('isLoading', opts.isLoading);
  }
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, translocoService };
}

// ---------------------------------------------------------------------------
// Render tests
// ---------------------------------------------------------------------------

describe('SearchBarComponent — render', () => {
  it('should instantiate', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render a search input', () => {
    const { fixture } = setup();
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect(input).not.toBeNull();
  });

  it('should render the Search button', () => {
    const { fixture } = setup();
    const btn = fixture.debugElement.query(By.css('button[data-testid="search-button"]'));
    expect(btn).not.toBeNull();
  });

  it('should render the Search button with translated label', () => {
    const { fixture } = setup();
    const btn = fixture.debugElement.query(By.css('button[data-testid="search-button"]'));
    expect((btn.nativeElement as HTMLButtonElement).textContent?.trim()).toBe('Search');
  });

  it('should render placeholder from translation', () => {
    const { fixture } = setup();
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect((input.nativeElement as HTMLInputElement).placeholder).toBe('Search plugins…');
  });

  it('should populate input value from initialKeyword input', () => {
    const { fixture } = setup({ initialKeyword: 'typescript' });
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect((input.nativeElement as HTMLInputElement).value).toBe('typescript');
  });

  it('should set aria-busy on input when isLoading is true', () => {
    const { fixture } = setup({ isLoading: true });
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect((input.nativeElement as HTMLInputElement).getAttribute('aria-busy')).toBe('true');
  });

  it('should NOT set aria-busy when isLoading is false', () => {
    const { fixture } = setup({ isLoading: false });
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    const ariaBusy = (input.nativeElement as HTMLInputElement).getAttribute('aria-busy');
    // aria-busy should be null or absent when false
    expect(ariaBusy).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// onSearch
// ---------------------------------------------------------------------------

describe('SearchBarComponent — onSearch', () => {
  it('should emit searchSubmitted with trimmed keyword', () => {
    const { fixture } = setup();
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    fixture.componentInstance.onSearch('  typescript  ');
    expect(emitted[0]).toBe('typescript');
  });

  it('should emit searchSubmitted with empty string when keyword is only spaces', () => {
    const { fixture } = setup();
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    fixture.componentInstance.onSearch('   ');
    expect(emitted[0]).toBe('');
  });

  it('should emit searchSubmitted when Search button is clicked', () => {
    const { fixture } = setup({ initialKeyword: 'plugin' });
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    const btn = fixture.debugElement.query(By.css('button[data-testid="search-button"]'));
    btn.triggerEventHandler('click', null);
    expect(emitted).toHaveLength(1);
  });

  it('should not throw when onSearch is called', () => {
    const { fixture } = setup();
    expect(() => fixture.componentInstance.onSearch('test')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Inputs / Outputs contract
// ---------------------------------------------------------------------------

describe('SearchBarComponent — inputs / outputs', () => {
  it('should expose searchSubmitted output', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance.searchSubmitted).toBeDefined();
  });

  it('should expose filtersChanged output', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance.filtersChanged).toBeDefined();
  });

  it('should expose initialKeyword input', () => {
    const { fixture } = setup();
    expect('initialKeyword' in fixture.componentInstance).toBe(true);
  });

  it('should expose isLoading input', () => {
    const { fixture } = setup();
    expect('isLoading' in fixture.componentInstance).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// i18n — FR language rendering
// ---------------------------------------------------------------------------

describe('SearchBarComponent — i18n FR language rendering', () => {
  it('[FR] search button label is "Rechercher" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const btn = fixture.debugElement.query(By.css('button[data-testid="search-button"]'));
    expect((btn.nativeElement as HTMLButtonElement).textContent?.trim()).toBe('Rechercher');
  });

  it('[FR] placeholder is "Rechercher des plugins…" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect((input.nativeElement as HTMLInputElement).placeholder).toBe('Rechercher des plugins…');
  });
});
