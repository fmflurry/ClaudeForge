/**
 * Spec — pagination.component.ts (i18n migration)
 *
 * Verifies that:
 *  - EN: nav aria-label "Pagination", prev "Previous page", next "Next page"
 *  - FR: nav aria-label "Pagination", prev "Page précédente", next "Page suivante"
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { PaginationComponent } from './pagination.component';
import { I18nFacade } from '../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
  'shared.pagination.aria': 'Pagination',
  'shared.pagination.prev-aria': 'Previous page',
  'shared.pagination.next-aria': 'Next page',
};

const FR_LANGS: Record<string, string> = {
  'shared.pagination.aria': 'Pagination',
  'shared.pagination.prev-aria': 'Page précédente',
  'shared.pagination.next-aria': 'Page suivante',
};

// ---------------------------------------------------------------------------
// Stub LanguageStoragePort
// ---------------------------------------------------------------------------

@Injectable()
class StubLanguageStorage {
  read = () => null;
  write = () => undefined;
}

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setup(
  currentPage = 2,
  totalPages = 5,
): {
  fixture: ComponentFixture<PaginationComponent>;
  component: PaginationComponent;
  translocoService: TranslocoService;
} {
  TestBed.configureTestingModule({
    imports: [
      PaginationComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_LANGS, fr: FR_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [I18nFacade, { provide: LanguageStoragePort, useClass: StubLanguageStorage }],
  });

  const fixture = TestBed.createComponent(PaginationComponent);
  fixture.componentRef.setInput('currentPage', currentPage);
  fixture.componentRef.setInput('totalPages', totalPages);
  fixture.detectChanges();

  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, component: fixture.componentInstance, translocoService };
}

// ---------------------------------------------------------------------------
// Tests — EN
// ---------------------------------------------------------------------------

describe('PaginationComponent — EN rendering', () => {
  it('nav has aria-label "Pagination" in EN', () => {
    const { fixture } = setup();
    const nav = fixture.nativeElement.querySelector('.cf-pagination') as HTMLElement | null;
    expect(nav?.getAttribute('aria-label')).toBe('Pagination');
  });

  it('previous button has aria-label "Previous page" in EN', () => {
    const { fixture } = setup();
    const buttons = fixture.nativeElement.querySelectorAll('.cf-pagination__btn') as NodeListOf<HTMLButtonElement>;
    expect(buttons[0]?.getAttribute('aria-label')).toBe('Previous page');
  });

  it('next button has aria-label "Next page" in EN', () => {
    const { fixture } = setup();
    const buttons = fixture.nativeElement.querySelectorAll('.cf-pagination__btn') as NodeListOf<HTMLButtonElement>;
    expect(buttons[buttons.length - 1]?.getAttribute('aria-label')).toBe('Next page');
  });
});

// ---------------------------------------------------------------------------
// Tests — FR
// ---------------------------------------------------------------------------

describe('PaginationComponent — FR rendering', () => {
  it('nav has aria-label "Pagination" in FR', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const nav = fixture.nativeElement.querySelector('.cf-pagination') as HTMLElement | null;
    expect(nav?.getAttribute('aria-label')).toBe('Pagination');
  });

  it('previous button has aria-label "Page précédente" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('.cf-pagination__btn') as NodeListOf<HTMLButtonElement>;
    expect(buttons[0]?.getAttribute('aria-label')).toBe('Page précédente');
  });

  it('next button has aria-label "Page suivante" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('.cf-pagination__btn') as NodeListOf<HTMLButtonElement>;
    expect(buttons[buttons.length - 1]?.getAttribute('aria-label')).toBe('Page suivante');
  });
});
