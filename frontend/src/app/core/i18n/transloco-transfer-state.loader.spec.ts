/**
 * Spec 6 — transloco-transfer-state.loader.ts
 *
 * The stub is already functional (not a "Not implemented" throw), so this spec
 * tests the exact conditional branching. GREEN after the coder implements the
 * real loader with the same branching logic the stub already shows.
 *
 * GREEN contract for the coder:
 *
 *   @Injectable({ providedIn: 'root' })
 *   export class TranslocoTransferStateLoader implements TranslocoLoader {
 *     private readonly http = inject(HttpClient);
 *     private readonly transferState = inject(TransferState);
 *     private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
 *
 *     getTranslation(lang: string): Observable<Translation>
 *       browser + TransferState HAS key `transloco.<lang>`:
 *         → returns of(cachedValue)
 *         → removes key from TransferState after reading
 *         → does NOT make an HTTP request
 *       browser + TransferState does NOT have key:
 *         → makes GET /i18n/<lang>.json via HttpClient
 *       server platform:
 *         → reads translation from disk via injected I18N_FILE_READER + I18N_DIST_PATH
 *         → sets TransferState key so it serializes into the HTML
 *         → does NOT make an HTTP request when I18N_FILE_READER is provided
 *
 *   makeStateKey<Translation>(`transloco.${lang}`) is the key format.
 */

import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { PLATFORM_ID, TransferState, makeStateKey } from '@angular/core';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { vi } from 'vitest';
import { TranslocoTransferStateLoader } from './transloco-transfer-state.loader';
import { I18N_DIST_PATH, I18N_FILE_READER } from './i18n-dist-path.token';
import type { Translation } from '@jsverse/transloco';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function setupBrowser(): {
  loader: TranslocoTransferStateLoader;
  httpMock: HttpTestingController;
  transferState: TransferState;
} {
  TestBed.configureTestingModule({
    providers: [
      TranslocoTransferStateLoader,
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PLATFORM_ID, useValue: 'browser' },
    ],
  });
  return {
    loader: TestBed.inject(TranslocoTransferStateLoader),
    httpMock: TestBed.inject(HttpTestingController),
    transferState: TestBed.inject(TransferState),
  };
}

function setupServer(fileReaderSpy: (path: string) => string = () => '{}'): {
  loader: TranslocoTransferStateLoader;
  httpMock: HttpTestingController;
  transferState: TransferState;
} {
  TestBed.configureTestingModule({
    providers: [
      TranslocoTransferStateLoader,
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: I18N_DIST_PATH, useValue: '/fake/dist/browser' },
      { provide: I18N_FILE_READER, useValue: fileReaderSpy },
    ],
  });
  return {
    loader: TestBed.inject(TranslocoTransferStateLoader),
    httpMock: TestBed.inject(HttpTestingController),
    transferState: TestBed.inject(TransferState),
  };
}

// ---------------------------------------------------------------------------
// Browser — TransferState HIT (no HTTP request)
// ---------------------------------------------------------------------------

describe('TranslocoTransferStateLoader — browser, TransferState HIT', () => {
  it('returns the cached translation WITHOUT making an HTTP request', fakeAsync(() => {
    const { loader, httpMock, transferState } = setupBrowser();
    const cached: Translation = { hello: 'Hello', world: 'World' };
    const key = makeStateKey<Translation>('transloco.en');
    transferState.set(key, cached);

    let result: Translation | null = null;
    loader.getTranslation('en').subscribe((t) => (result = t));
    tick();

    // Assert: the returned translation equals the cached value
    expect(result).toEqual(cached);
    // Assert: NO HTTP request was made
    httpMock.expectNone('/i18n/en.json');
  }));

  it('removes the TransferState key after reading it', fakeAsync(() => {
    const { loader, httpMock, transferState } = setupBrowser();
    const cached: Translation = { key: 'value' };
    const key = makeStateKey<Translation>('transloco.en');
    transferState.set(key, cached);

    loader.getTranslation('en').subscribe(() => {
      /* consume */
    });
    tick();

    expect(transferState.hasKey(key)).toBe(false);
    httpMock.expectNone('/i18n/en.json');
  }));

  it('uses key format "transloco.<lang>" in TransferState', fakeAsync(() => {
    const { loader, httpMock, transferState } = setupBrowser();
    const cached: Translation = { bonjour: 'Bonjour' };
    const key = makeStateKey<Translation>('transloco.fr');
    transferState.set(key, cached);

    let result: Translation | null = null;
    loader.getTranslation('fr').subscribe((t) => (result = t));
    tick();

    expect(result).toEqual(cached);
    httpMock.expectNone('/i18n/fr.json');
  }));
});

// ---------------------------------------------------------------------------
// Browser — TransferState MISS (HTTP request must be made)
// ---------------------------------------------------------------------------

describe('TranslocoTransferStateLoader — browser, TransferState MISS', () => {
  it('makes a GET request to /i18n/<lang>.json when TransferState has no key', fakeAsync(() => {
    const { loader, httpMock } = setupBrowser();
    const serverData: Translation = { greeting: 'Hello' };

    let result: Translation | null = null;
    loader.getTranslation('en').subscribe((t) => (result = t));
    tick();

    const req = httpMock.expectOne('/i18n/en.json');
    expect(req.request.method).toBe('GET');
    req.flush(serverData);
    tick();

    expect(result).toEqual(serverData);
  }));

  it('makes GET /i18n/fr.json for lang "fr" (TransferState miss)', fakeAsync(() => {
    const { loader, httpMock } = setupBrowser();

    loader.getTranslation('fr').subscribe(() => {
      /* consume */
    });
    tick();

    const req = httpMock.expectOne('/i18n/fr.json');
    expect(req.request.method).toBe('GET');
    req.flush({ bonjour: 'Bonjour' });
  }));
});

// ---------------------------------------------------------------------------
// Server platform — disk-read branch (AUTHORIZED spec change)
// ---------------------------------------------------------------------------

describe('TranslocoTransferStateLoader — server platform', () => {
  it('reads translation from disk via I18N_FILE_READER without making an HTTP request', fakeAsync(() => {
    const diskData: Translation = { hello: 'Hello' };
    const fileReaderSpy = vi.fn((_path: string) => JSON.stringify(diskData));
    const { loader, httpMock, transferState } = setupServer(fileReaderSpy);

    let result: Translation | null = null;
    loader.getTranslation('en').subscribe((t) => (result = t));
    tick();

    // Assert: disk reader was called (not HTTP)
    expect(fileReaderSpy).toHaveBeenCalledTimes(1);
    expect(fileReaderSpy.mock.calls[0]?.[0]).toContain('en.json');

    // Assert: NO HTTP request was made
    httpMock.expectNone('/i18n/en.json');

    // Assert: returned translation matches disk data
    expect(result).toEqual(diskData);

    // Assert: TransferState key was set for client reuse
    const key = makeStateKey<Translation>('transloco.en');
    expect(transferState.hasKey(key)).toBe(true);
    expect(transferState.get(key, {})).toEqual(diskData);
  }));

  it('sets TransferState key so client can reuse without HTTP', fakeAsync(() => {
    const diskData: Translation = { bonjour: 'Bonjour' };
    const fileReaderSpy = vi.fn((_path: string) => JSON.stringify(diskData));
    const { loader, transferState } = setupServer(fileReaderSpy);

    loader.getTranslation('fr').subscribe(() => {
      /* consume */
    });
    tick();

    const key = makeStateKey<Translation>('transloco.fr');
    expect(transferState.hasKey(key)).toBe(true);
    expect(transferState.get(key, {})).toEqual(diskData);
  }));
});

// ---------------------------------------------------------------------------
// Key format contract
// ---------------------------------------------------------------------------

describe('TranslocoTransferStateLoader — key format', () => {
  it('uses makeStateKey("transloco.en") — key is detectable in TransferState', fakeAsync(() => {
    const { loader, httpMock, transferState } = setupBrowser();
    const cached: Translation = { x: 'y' };

    // Simulate server having placed the key
    const key = makeStateKey<Translation>('transloco.en');
    transferState.set(key, cached);

    expect(transferState.hasKey(key)).toBe(true);

    loader.getTranslation('en').subscribe(() => {
      /* consume */
    });
    tick();

    // After reading, key is removed
    expect(transferState.hasKey(key)).toBe(false);
    httpMock.expectNone('/i18n/en.json');
  }));
});
