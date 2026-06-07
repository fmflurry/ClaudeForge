/**
 * Unit tests for AnonIdAdapter.
 * The pre-existing anon-id.spec.ts has 2 failing tests due to a test issue.
 * This spec covers the adapter's key behaviors with correct patterns.
 */

import { AnonIdAdapter } from './anon-id.adapter';
import { CryptoPort } from '../../domain/ports/crypto.port';

// ---------------------------------------------------------------------------
// Fake CryptoPort — does NOT extend CryptoPort to avoid the circular DI issue
// ---------------------------------------------------------------------------

class FakeCrypto {
  static readonly ANON_ID_STORAGE_KEY = CryptoPort.ANON_ID_STORAGE_KEY;

  private counter = 0;
  readonly generatedUUIDs: string[] = [];

  randomUUID(): string {
    const uuid = `fake-uuid-${++this.counter}`;
    this.generatedUUIDs.push(uuid);
    return uuid;
  }

  async sha256Hex(input: string): Promise<string> {
    // Deterministic fake: prefix + input
    return `hash(${input})`;
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function clearStorage(): void {
  window.localStorage.removeItem(CryptoPort.ANON_ID_STORAGE_KEY);
}

function writeStorage(value: string): void {
  window.localStorage.setItem(CryptoPort.ANON_ID_STORAGE_KEY, value);
}

function readStorage(): string | null {
  return window.localStorage.getItem(CryptoPort.ANON_ID_STORAGE_KEY);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('AnonIdAdapter — getOrCreate', () => {
  beforeEach(() => {
    clearStorage();
  });

  afterEach(() => {
    clearStorage();
  });

  it('should generate a new id and store it when none exists', async () => {
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const id = await adapter.getOrCreate();
    expect(id).toBe('hash(fake-uuid-1)');
    expect(readStorage()).toBe('hash(fake-uuid-1)');
  });

  it('should return the stored id when one exists without generating a new UUID', async () => {
    writeStorage('existing-id');
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const id = await adapter.getOrCreate();
    expect(id).toBe('existing-id');
    expect(crypto.generatedUUIDs).toHaveLength(0);
  });

  it('should return the same id on repeated calls when already stored', async () => {
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const first = await adapter.getOrCreate();
    const second = await adapter.getOrCreate();
    expect(first).toBe(second);
    expect(crypto.generatedUUIDs).toHaveLength(1); // only generated once
  });

  it('should return a non-empty string', async () => {
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const id = await adapter.getOrCreate();
    expect(id.length).toBeGreaterThan(0);
  });
});

describe('AnonIdAdapter — rotate', () => {
  beforeEach(() => {
    clearStorage();
  });

  afterEach(() => {
    clearStorage();
  });

  it('should clear existing id and generate a new one', async () => {
    writeStorage('old-id');
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const newId = await adapter.rotate();
    expect(newId).toBe('hash(fake-uuid-1)');
    expect(readStorage()).toBe('hash(fake-uuid-1)');
  });

  it('should generate a new id even when no id was stored', async () => {
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const newId = await adapter.rotate();
    expect(newId.length).toBeGreaterThan(0);
  });

  it('should return a different id than the one that was stored before rotation', async () => {
    writeStorage('old-id');
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const newId = await adapter.rotate();
    expect(newId).not.toBe('old-id');
  });

  it('getOrCreate after rotate should return rotated id', async () => {
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    const rotated = await adapter.rotate();
    const retrieved = await adapter.getOrCreate();
    expect(retrieved).toBe(rotated);
  });
});

describe('AnonIdAdapter — clear', () => {
  beforeEach(() => {
    clearStorage();
  });

  afterEach(() => {
    clearStorage();
  });

  it('should remove the stored id', () => {
    writeStorage('some-id');
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    adapter.clear();
    expect(readStorage()).toBeNull();
  });

  it('should not throw when called with no stored id', () => {
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    expect(() => adapter.clear()).not.toThrow();
  });

  it('getOrCreate after clear should generate a fresh id', async () => {
    writeStorage('old-id');
    const crypto = new FakeCrypto();
    const adapter = new AnonIdAdapter(crypto as unknown as CryptoPort);
    adapter.clear();
    const id = await adapter.getOrCreate();
    expect(id).toBe('hash(fake-uuid-1)');
  });
});
