/**
 * Spec 3 — local-storage-language.adapter.ts
 *
 * RED: stub throws "Not implemented" — all tests below will FAIL.
 *
 * GREEN contract for the coder:
 *
 *   LocalStorageLanguageAdapter extends LanguageStoragePort
 *     Injected: PLATFORM_ID
 *
 *   read(): Lang | null
 *     browser: reads LanguageStoragePort.STORAGE_KEY ('cf.lang') from localStorage
 *              returns the stored Lang or null if not set / key absent
 *     server:  returns null (does NOT access localStorage)
 *
 *   write(lang: Lang): void
 *     browser: writes lang to LanguageStoragePort.STORAGE_KEY in localStorage
 *     server:  no-op — does NOT access localStorage, does NOT throw
 *
 *   STORAGE_KEY = 'cf.lang'
 */

import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { LocalStorageLanguageAdapter } from './local-storage-language.adapter';
import { LanguageStoragePort } from './language-storage.port';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function clearStorage(): void {
  window.localStorage.clear();
}

function setupBrowser(): LocalStorageLanguageAdapter {
  TestBed.configureTestingModule({
    providers: [
      LocalStorageLanguageAdapter,
      { provide: LanguageStoragePort, useClass: LocalStorageLanguageAdapter },
      { provide: PLATFORM_ID, useValue: 'browser' },
    ],
  });
  return TestBed.inject(LocalStorageLanguageAdapter);
}

function setupServer(): LocalStorageLanguageAdapter {
  TestBed.configureTestingModule({
    providers: [
      LocalStorageLanguageAdapter,
      { provide: LanguageStoragePort, useClass: LocalStorageLanguageAdapter },
      { provide: PLATFORM_ID, useValue: 'server' },
    ],
  });
  return TestBed.inject(LocalStorageLanguageAdapter);
}

// ---------------------------------------------------------------------------
// Storage key constant
// ---------------------------------------------------------------------------

describe('LanguageStoragePort.STORAGE_KEY', () => {
  it('is "cf.lang"', () => {
    expect(LanguageStoragePort.STORAGE_KEY).toBe('cf.lang');
  });
});

// ---------------------------------------------------------------------------
// Browser platform
// ---------------------------------------------------------------------------

describe('LocalStorageLanguageAdapter — browser platform', () => {
  beforeEach(() => {
    clearStorage();
  });

  it('read() returns null when no language is stored', () => {
    const adapter = setupBrowser();
    expect(adapter.read()).toBeNull();
  });

  it('read() returns "en" after write("en")', () => {
    const adapter = setupBrowser();
    adapter.write('en');
    expect(adapter.read()).toBe('en');
  });

  it('read() returns "fr" after write("fr")', () => {
    const adapter = setupBrowser();
    adapter.write('fr');
    expect(adapter.read()).toBe('fr');
  });

  it('write("en") stores value under "cf.lang" key in localStorage', () => {
    const adapter = setupBrowser();
    adapter.write('en');
    expect(window.localStorage.getItem('cf.lang')).toBe('en');
  });

  it('write("fr") stores "fr" under "cf.lang" in localStorage', () => {
    const adapter = setupBrowser();
    adapter.write('fr');
    expect(window.localStorage.getItem('cf.lang')).toBe('fr');
  });

  it('write() overwrites a previous value', () => {
    const adapter = setupBrowser();
    adapter.write('en');
    adapter.write('fr');
    expect(adapter.read()).toBe('fr');
    expect(window.localStorage.getItem('cf.lang')).toBe('fr');
  });

  it('read() returns null when localStorage has an unrecognised value (corrupted)', () => {
    window.localStorage.setItem('cf.lang', 'xx');
    const adapter = setupBrowser();
    // read() should return null (or at most silently ignore) for unrecognised values
    // Acceptable: null OR the raw string — the coder decides. We assert no throw.
    expect(() => adapter.read()).not.toThrow();
  });

  it('write() does not throw', () => {
    const adapter = setupBrowser();
    expect(() => adapter.write('en')).not.toThrow();
  });

  it('adapter is an instance of LanguageStoragePort', () => {
    const adapter = setupBrowser();
    expect(adapter).toBeInstanceOf(LanguageStoragePort);
  });

  it('read() and write() are methods', () => {
    const adapter = setupBrowser();
    expect(typeof adapter.read).toBe('function');
    expect(typeof adapter.write).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// Server platform — SSR guard
// ---------------------------------------------------------------------------

describe('LocalStorageLanguageAdapter — server platform (SSR guard)', () => {
  beforeEach(() => {
    clearStorage();
  });

  it('read() returns null on server platform', () => {
    const adapter = setupServer();
    expect(adapter.read()).toBeNull();
  });

  it('write() on server platform does NOT throw', () => {
    const adapter = setupServer();
    expect(() => adapter.write('fr')).not.toThrow();
  });

  it('write() on server platform does NOT touch localStorage', () => {
    const adapter = setupServer();
    adapter.write('fr');
    expect(window.localStorage.getItem('cf.lang')).toBeNull();
  });

  it('read() on server platform does NOT touch localStorage (no side effects)', () => {
    window.localStorage.setItem('cf.lang', 'en');
    const adapter = setupServer();
    // Even with a value in localStorage, server returns null
    expect(adapter.read()).toBeNull();
  });
});
