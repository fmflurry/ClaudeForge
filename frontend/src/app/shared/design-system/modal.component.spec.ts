/**
 * Spec — modal.component.ts (i18n migration)
 *
 * Verifies that:
 *  - EN: close button has aria-label "Close modal" (default from i18n key)
 *  - FR: close button has aria-label "Fermer la fenêtre" when lang switches to fr
 *  - title @Input always wins (not translated)
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { ModalComponent } from './modal.component';
import { I18nFacade } from '../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
  'shared.modal.close-aria': 'Close modal',
};

const FR_LANGS: Record<string, string> = {
  'shared.modal.close-aria': 'Fermer la fenêtre',
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

function setup(title = 'Test Modal'): {
  fixture: ComponentFixture<ModalComponent>;
  component: ModalComponent;
  translocoService: TranslocoService;
} {
  TestBed.configureTestingModule({
    imports: [
      ModalComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_LANGS, fr: FR_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [I18nFacade, { provide: LanguageStoragePort, useClass: StubLanguageStorage }],
  });

  const fixture = TestBed.createComponent(ModalComponent);
  fixture.componentRef.setInput('title', title);
  fixture.detectChanges();

  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, component: fixture.componentInstance, translocoService };
}

// ---------------------------------------------------------------------------
// Tests — EN
// ---------------------------------------------------------------------------

describe('ModalComponent — EN rendering', () => {
  it('renders the title passed via @Input', () => {
    const { fixture } = setup('My Dialog');
    const titleEl = fixture.nativeElement.querySelector('.cf-modal__title') as HTMLElement | null;
    expect(titleEl?.textContent?.trim()).toBe('My Dialog');
  });

  it('close button has aria-label "Close modal" in EN', () => {
    const { fixture } = setup();
    const closeBtn = fixture.nativeElement.querySelector('.cf-modal__close') as HTMLButtonElement | null;
    expect(closeBtn?.getAttribute('aria-label')).toBe('Close modal');
  });
});

// ---------------------------------------------------------------------------
// Tests — FR
// ---------------------------------------------------------------------------

describe('ModalComponent — FR rendering', () => {
  it('close button has aria-label "Fermer la fenêtre" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const closeBtn = fixture.nativeElement.querySelector('.cf-modal__close') as HTMLButtonElement | null;
    expect(closeBtn?.getAttribute('aria-label')).toBe('Fermer la fenêtre');
  });

  it('title @Input override still wins in FR (title is not translated)', () => {
    const { fixture, translocoService } = setup('Override Title');
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const titleEl = fixture.nativeElement.querySelector('.cf-modal__title') as HTMLElement | null;
    expect(titleEl?.textContent?.trim()).toBe('Override Title');
  });
});
