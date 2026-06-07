/**
 * RED tests — Task 16.1: Anon-ID generation, persistence, and rotation
 *
 * Expected production files (DO NOT exist yet — tests WILL FAIL to compile/resolve):
 *   src/app/features/telemetry/domain/ports/crypto.port.ts
 *   src/app/features/telemetry/domain/ports/anon-id.port.ts
 *   src/app/features/telemetry/infrastructure/adapter/anon-id.adapter.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // crypto.port.ts
 *   abstract class CryptoPort {
 *     static readonly ANON_ID_STORAGE_KEY = 'plugin-marketplace:anon-id';
 *     // Generate a UUID v4 string.
 *     abstract randomUUID(): string;
 *     // SHA-256 hash the input string; returns lowercase 64-char hex.
 *     abstract sha256Hex(input: string): Promise<string>;
 *   }
 *
 *   // anon-id.port.ts
 *   abstract class AnonIdPort {
 *     // Return the persisted anon-id, generating a fresh one if absent.
 *     abstract getOrCreate(): Promise<string>;
 *     // Clear stored id and generate a fresh one (rotation).
 *     abstract rotate(): Promise<string>;
 *     // Clear stored id from storage (e.g. on full opt-out clear).
 *     abstract clear(): void;
 *   }
 *
 *   // anon-id.adapter.ts
 *   class AnonIdAdapter extends AnonIdPort {
 *     // Injects CryptoPort (or takes it as a constructor arg for testability).
 *     // Reads/writes localStorage key CryptoPort.ANON_ID_STORAGE_KEY.
 *     getOrCreate(): Promise<string> { ... }
 *     rotate(): Promise<string>      { ... }
 *     clear(): void                  { ... }
 *   }
 *
 *   NOTE ON CRYPTO:
 *   jsdom 27 does NOT expose crypto.subtle, but Node 22 global crypto.subtle IS
 *   available in the vitest test process.  The CryptoPort abstraction allows tests
 *   to inject a deterministic fake while the real WebCryptoAdapter uses
 *   globalThis.crypto.subtle (available in browsers and Node 22+).
 *
 *   // web-crypto.adapter.ts (production impl — injected via DI)
 *   class WebCryptoAdapter extends CryptoPort {
 *     randomUUID(): string { return globalThis.crypto.randomUUID(); }
 *     sha256Hex(input: string): Promise<string> {
 *       return globalThis.crypto.subtle
 *         .digest('SHA-256', new TextEncoder().encode(input))
 *         .then(buf => Array.from(new Uint8Array(buf))
 *           .map(b => b.toString(16).padStart(2, '0')).join(''));
 *     }
 *   }
 */

import { AnonIdPort } from './anon-id.port';
import { CryptoPort } from './crypto.port';
import { AnonIdAdapter } from '../../infrastructure/adapter/anon-id.adapter';

// ---------------------------------------------------------------------------
// Fake CryptoPort for deterministic tests (no real async crypto needed)
// ---------------------------------------------------------------------------

const FAKE_UUID_1 = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
const FAKE_UUID_2 = 'ffffffff-0000-4111-a222-333333333333';

/**
 * SHA-256 of FAKE_UUID_1 as lowercase hex (pre-computed for assertions).
 * We compute this below using Node 22 globalThis.crypto.subtle so the test
 * constant is always in sync.
 */
async function sha256Hex(input: string): Promise<string> {
  const buf = await globalThis.crypto.subtle.digest('SHA-256', new TextEncoder().encode(input));
  return Array.from(new Uint8Array(buf))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('');
}

class FakeCrypto extends CryptoPort {
  private uuidQueue: string[];

  constructor(uuids: string[]) {
    super();
    this.uuidQueue = [...uuids];
  }

  randomUUID(): string {
    const id = this.uuidQueue.shift();
    if (!id) throw new Error('FakeCrypto: UUID queue exhausted');
    return id;
  }

  sha256Hex(input: string): Promise<string> {
    return sha256Hex(input);
  }
}

// ---------------------------------------------------------------------------
// Helper — clear anon-id from localStorage between tests
// ---------------------------------------------------------------------------

function clearAnonIdStorage(): void {
  window.localStorage.removeItem(CryptoPort.ANON_ID_STORAGE_KEY);
}

// ---------------------------------------------------------------------------
// CryptoPort.ANON_ID_STORAGE_KEY constant
// ---------------------------------------------------------------------------

describe('CryptoPort — storage key constant', () => {
  it('ANON_ID_STORAGE_KEY should equal "plugin-marketplace:anon-id"', () => {
    expect(CryptoPort.ANON_ID_STORAGE_KEY).toBe('plugin-marketplace:anon-id');
  });
});

// ---------------------------------------------------------------------------
// AnonIdAdapter — getOrCreate: shape of generated id
// ---------------------------------------------------------------------------

describe('AnonIdAdapter — getOrCreate: id shape', () => {
  let adapter: AnonIdPort;

  beforeEach(() => {
    clearAnonIdStorage();
    adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1]));
  });

  it('should return a 64-character lowercase hex string', async () => {
    const id = await adapter.getOrCreate();
    expect(id).toHaveLength(64);
    expect(id).toMatch(/^[0-9a-f]{64}$/);
  });

  it('should NOT return the raw UUID (must be the SHA-256 hash)', async () => {
    const id = await adapter.getOrCreate();
    expect(id).not.toBe(FAKE_UUID_1);
  });

  it('generated id should equal SHA-256 of the generated UUID', async () => {
    const expectedHash = await sha256Hex(FAKE_UUID_1);
    const id = await adapter.getOrCreate();
    expect(id).toBe(expectedHash);
  });
});

// ---------------------------------------------------------------------------
// AnonIdAdapter — getOrCreate: persistence (reuse stored id)
// ---------------------------------------------------------------------------

describe('AnonIdAdapter — getOrCreate: persistence', () => {
  it('should persist the generated id in localStorage under ANON_ID_STORAGE_KEY', async () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1]));
    const id = await adapter.getOrCreate();
    expect(window.localStorage.getItem(CryptoPort.ANON_ID_STORAGE_KEY)).toBe(id);
  });

  it('should return the same id on a second call (reuse persisted)', async () => {
    clearAnonIdStorage();
    // First adapter creates the id
    const adapter1 = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1]));
    const id1 = await adapter1.getOrCreate();

    // Second adapter instance reads from the same localStorage
    const adapter2 = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_2]));
    const id2 = await adapter2.getOrCreate();

    expect(id2).toBe(id1);
  });

  it('should NOT call crypto.randomUUID a second time when id is already persisted', async () => {
    clearAnonIdStorage();
    let uuidCallCount = 0;
    class CountingCrypto extends CryptoPort {
      randomUUID(): string {
        uuidCallCount++;
        return FAKE_UUID_1;
      }
      sha256Hex(input: string): Promise<string> {
        return sha256Hex(input);
      }
    }
    const adapter = new AnonIdAdapter(new CountingCrypto());
    await adapter.getOrCreate();
    await adapter.getOrCreate(); // second call — must NOT regenerate
    expect(uuidCallCount).toBe(1);
  });

  it('should generate a fresh id when localStorage has been cleared', async () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1, FAKE_UUID_2]));
    const id1 = await adapter.getOrCreate();
    // Simulate the user clearing browser storage
    clearAnonIdStorage();
    const id2 = await adapter.getOrCreate();
    expect(id2).not.toBe(id1);
  });
});

// ---------------------------------------------------------------------------
// AnonIdAdapter — clear
// ---------------------------------------------------------------------------

describe('AnonIdAdapter — clear', () => {
  it('should remove the stored id from localStorage', async () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1]));
    await adapter.getOrCreate();
    adapter.clear();
    expect(window.localStorage.getItem(CryptoPort.ANON_ID_STORAGE_KEY)).toBeNull();
  });

  it('should not throw when clear is called with nothing stored', () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1]));
    expect(() => adapter.clear()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// AnonIdAdapter — rotate: produces a different id
// ---------------------------------------------------------------------------

describe('AnonIdAdapter — rotate', () => {
  it('should return a new 64-char hex id after rotation', async () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1, FAKE_UUID_2]));
    await adapter.getOrCreate();
    const rotated = await adapter.rotate();
    expect(rotated).toHaveLength(64);
    expect(rotated).toMatch(/^[0-9a-f]{64}$/);
  });

  it('rotated id should differ from the original id', async () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1, FAKE_UUID_2]));
    const original = await adapter.getOrCreate();
    const rotated = await adapter.rotate();
    expect(rotated).not.toBe(original);
  });

  it('rotated id should equal SHA-256 of the new UUID', async () => {
    clearAnonIdStorage();
    const expectedRotated = await sha256Hex(FAKE_UUID_2);
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1, FAKE_UUID_2]));
    await adapter.getOrCreate();
    const rotated = await adapter.rotate();
    expect(rotated).toBe(expectedRotated);
  });

  it('should persist the rotated id in localStorage', async () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1, FAKE_UUID_2]));
    await adapter.getOrCreate();
    const rotated = await adapter.rotate();
    expect(window.localStorage.getItem(CryptoPort.ANON_ID_STORAGE_KEY)).toBe(rotated);
  });

  it('subsequent getOrCreate after rotate should return rotated id (no new UUID)', async () => {
    clearAnonIdStorage();
    let uuidCallCount = 0;
    class CountingCrypto extends CryptoPort {
      private uuids = [FAKE_UUID_1, FAKE_UUID_2];
      randomUUID(): string {
        uuidCallCount++;
        const id = this.uuids.shift();
        if (!id) throw new Error('exhausted');
        return id;
      }
      sha256Hex(input: string): Promise<string> {
        return sha256Hex(input);
      }
    }
    const adapter = new AnonIdAdapter(new CountingCrypto());
    await adapter.getOrCreate(); // call 1
    await adapter.rotate(); // call 2
    await adapter.getOrCreate(); // should NOT call randomUUID again
    expect(uuidCallCount).toBe(2);
  });

  it('should work correctly even when rotate is called before any getOrCreate', async () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1]));
    const rotated = await adapter.rotate();
    expect(rotated).toHaveLength(64);
    expect(rotated).toMatch(/^[0-9a-f]{64}$/);
  });
});

// ---------------------------------------------------------------------------
// AnonIdPort — structural contract
// ---------------------------------------------------------------------------

describe('AnonIdPort — structural contract', () => {
  it('AnonIdAdapter should extend AnonIdPort', () => {
    clearAnonIdStorage();
    const adapter = new AnonIdAdapter(new FakeCrypto([FAKE_UUID_1]));
    expect(adapter).toBeInstanceOf(AnonIdAdapter);
    expect(typeof adapter.getOrCreate).toBe('function');
    expect(typeof adapter.rotate).toBe('function');
    expect(typeof adapter.clear).toBe('function');
  });
});
