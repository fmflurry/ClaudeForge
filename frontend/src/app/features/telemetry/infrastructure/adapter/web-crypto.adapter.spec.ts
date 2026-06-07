/**
 * Unit tests for WebCryptoAdapter.
 * The adapter delegates to globalThis.crypto — tests verify the contract.
 */

import { TestBed } from '@angular/core/testing';
import { WebCryptoAdapter } from './web-crypto.adapter';
import { CryptoPort } from '../../domain/ports/crypto.port';

function setup(): WebCryptoAdapter {
  TestBed.configureTestingModule({
    providers: [
      { provide: CryptoPort, useClass: WebCryptoAdapter },
      WebCryptoAdapter,
    ],
  });
  return TestBed.inject(WebCryptoAdapter);
}

// ---------------------------------------------------------------------------
// randomUUID
// ---------------------------------------------------------------------------

describe('WebCryptoAdapter — randomUUID', () => {
  it('should return a string', () => {
    const adapter = setup();
    expect(typeof adapter.randomUUID()).toBe('string');
  });

  it('should return a UUID v4 shaped string (xxxxxxxx-xxxx-4xxx-...)', () => {
    const adapter = setup();
    const uuid = adapter.randomUUID();
    // Standard UUID format: 8-4-4-4-12 hex chars
    expect(uuid).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    );
  });

  it('should generate different UUIDs on successive calls', () => {
    const adapter = setup();
    const a = adapter.randomUUID();
    const b = adapter.randomUUID();
    expect(a).not.toBe(b);
  });

  it('should not return an empty string', () => {
    const adapter = setup();
    expect(adapter.randomUUID().length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// sha256Hex
// ---------------------------------------------------------------------------

describe('WebCryptoAdapter — sha256Hex', () => {
  it('should return a Promise', () => {
    const adapter = setup();
    const result = adapter.sha256Hex('hello');
    expect(result).toBeInstanceOf(Promise);
  });

  it('should produce a 64-char hex string for any non-empty input', async () => {
    const adapter = setup();
    const hex = await adapter.sha256Hex('hello world');
    expect(hex).toHaveLength(64);
    expect(hex).toMatch(/^[0-9a-f]{64}$/);
  });

  it('should produce the known SHA-256 of "hello"', async () => {
    const adapter = setup();
    // SHA-256("hello") = 2cf24db...
    const hex = await adapter.sha256Hex('hello');
    expect(hex).toBe('2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824');
  });

  it('should produce the known SHA-256 of empty string', async () => {
    const adapter = setup();
    // SHA-256("") = e3b0c44298fc1c149...
    const hex = await adapter.sha256Hex('');
    expect(hex).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855');
  });

  it('should produce different hashes for different inputs', async () => {
    const adapter = setup();
    const a = await adapter.sha256Hex('input-a');
    const b = await adapter.sha256Hex('input-b');
    expect(a).not.toBe(b);
  });

  it('should produce consistent output for the same input', async () => {
    const adapter = setup();
    const first = await adapter.sha256Hex('consistent');
    const second = await adapter.sha256Hex('consistent');
    expect(first).toBe(second);
  });
});

// ---------------------------------------------------------------------------
// Architecture — extends CryptoPort
// ---------------------------------------------------------------------------

describe('WebCryptoAdapter — architecture', () => {
  it('should be an instance of CryptoPort', () => {
    const adapter = setup();
    expect(adapter).toBeInstanceOf(CryptoPort);
  });
});
