/**
 * Spec — table.component.ts (i18n migration)
 *
 * Verifies that:
 *  - EN: wrapper region gets aria-label "Data table" by default
 *  - FR: wrapper region gets aria-label "Tableau de données" by default when lang is fr
 *  - Explicit ariaLabel @Input override wins over translated default (in both EN and FR)
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { TableComponent, TableColumn } from './table.component';
import { I18nFacade } from '../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
  'shared.table.aria': 'Data table',
};

const FR_LANGS: Record<string, string> = {
  'shared.table.aria': 'Tableau de données',
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
// Test data
// ---------------------------------------------------------------------------

interface Row {
  name: string;
  value: string;
}

const COLUMNS: TableColumn<Row>[] = [
  { key: 'name', header: 'Name' },
  { key: 'value', header: 'Value' },
];

const ROWS: Row[] = [{ name: 'Alpha', value: '1' }];

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setup(ariaLabel?: string): {
  fixture: ComponentFixture<TableComponent<Record<string, unknown>>>;
  component: TableComponent<Record<string, unknown>>;
  translocoService: TranslocoService;
} {
  TestBed.configureTestingModule({
    imports: [
      TableComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_LANGS, fr: FR_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [I18nFacade, { provide: LanguageStoragePort, useClass: StubLanguageStorage }],
  });

  const fixture = TestBed.createComponent(TableComponent);
  fixture.componentRef.setInput('columns', COLUMNS);
  fixture.componentRef.setInput('rows', ROWS);
  if (ariaLabel !== undefined) {
    fixture.componentRef.setInput('ariaLabel', ariaLabel);
  }
  fixture.detectChanges();

  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, component: fixture.componentInstance, translocoService };
}

// ---------------------------------------------------------------------------
// Tests — EN default
// ---------------------------------------------------------------------------

describe('TableComponent — EN rendering (default)', () => {
  it('wrapper has aria-label "Data table" by default in EN', () => {
    const { fixture } = setup();
    const wrapper = fixture.nativeElement.querySelector('.cf-table-wrapper') as HTMLElement | null;
    expect(wrapper?.getAttribute('aria-label')).toBe('Data table');
  });

  it('explicit ariaLabel @Input wins over translated default in EN', () => {
    const { fixture } = setup('My Custom Table');
    const wrapper = fixture.nativeElement.querySelector('.cf-table-wrapper') as HTMLElement | null;
    expect(wrapper?.getAttribute('aria-label')).toBe('My Custom Table');
  });
});

// ---------------------------------------------------------------------------
// Tests — FR
// ---------------------------------------------------------------------------

describe('TableComponent — FR rendering', () => {
  it('wrapper has aria-label "Tableau de données" by default when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const wrapper = fixture.nativeElement.querySelector('.cf-table-wrapper') as HTMLElement | null;
    expect(wrapper?.getAttribute('aria-label')).toBe('Tableau de données');
  });

  it('explicit ariaLabel @Input still wins over translated default in FR', () => {
    const { fixture, translocoService } = setup('Remplacement personnalisé');
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const wrapper = fixture.nativeElement.querySelector('.cf-table-wrapper') as HTMLElement | null;
    expect(wrapper?.getAttribute('aria-label')).toBe('Remplacement personnalisé');
  });
});
