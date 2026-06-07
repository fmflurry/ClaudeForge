/**
 * Spec — toast.component.ts (i18n migration)
 *
 * Verifies that:
 *  - EN: dismiss button has aria-label "Dismiss"
 *  - FR: dismiss button has aria-label "Ignorer" when lang is fr
 *  - message @Input always renders as-is (not translated)
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { ToastComponent } from './toast.component';
import { I18nFacade } from '../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
  'shared.toast.dismiss-aria': 'Dismiss',
};

const FR_LANGS: Record<string, string> = {
  'shared.toast.dismiss-aria': 'Ignorer',
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

function setup(message = 'Something happened'): {
  fixture: ComponentFixture<ToastComponent>;
  component: ToastComponent;
  translocoService: TranslocoService;
} {
  TestBed.configureTestingModule({
    imports: [
      ToastComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_LANGS, fr: FR_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [I18nFacade, { provide: LanguageStoragePort, useClass: StubLanguageStorage }],
  });

  const fixture = TestBed.createComponent(ToastComponent);
  fixture.componentRef.setInput('message', message);
  fixture.detectChanges();

  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, component: fixture.componentInstance, translocoService };
}

// ---------------------------------------------------------------------------
// Tests — EN
// ---------------------------------------------------------------------------

describe('ToastComponent — EN rendering', () => {
  it('dismiss button has aria-label "Dismiss" in EN', () => {
    const { fixture } = setup();
    const dismissBtn = fixture.nativeElement.querySelector('.cf-toast__dismiss') as HTMLButtonElement | null;
    expect(dismissBtn?.getAttribute('aria-label')).toBe('Dismiss');
  });

  it('renders the message @Input as-is', () => {
    const { fixture } = setup('Upload complete');
    const messageEl = fixture.nativeElement.querySelector('.cf-toast__message') as HTMLElement | null;
    expect(messageEl?.textContent?.trim()).toBe('Upload complete');
  });
});

// ---------------------------------------------------------------------------
// Tests — FR
// ---------------------------------------------------------------------------

describe('ToastComponent — FR rendering', () => {
  it('dismiss button has aria-label "Ignorer" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const dismissBtn = fixture.nativeElement.querySelector('.cf-toast__dismiss') as HTMLButtonElement | null;
    expect(dismissBtn?.getAttribute('aria-label')).toBe('Ignorer');
  });

  it('message @Input override still renders as-is in FR', () => {
    const { fixture, translocoService } = setup('Opération réussie');
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const messageEl = fixture.nativeElement.querySelector('.cf-toast__message') as HTMLElement | null;
    expect(messageEl?.textContent?.trim()).toBe('Opération réussie');
  });
});
