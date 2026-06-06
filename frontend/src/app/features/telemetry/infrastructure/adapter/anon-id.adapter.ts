import { AnonIdPort } from '../../domain/ports/anon-id.port';
import { CryptoPort } from '../../domain/ports/crypto.port';

/**
 * localStorage-backed implementation of AnonIdPort.
 * Reads/writes under CryptoPort.ANON_ID_STORAGE_KEY.
 *
 * The constructor accepts a CryptoPort instance to keep the adapter
 * testable without Angular DI (tests pass a FakeCrypto directly).
 * When wired via Angular DI, AnonIdAdapter uses inject(CryptoPort) instead.
 */
export class AnonIdAdapter extends AnonIdPort {
  private readonly crypto: CryptoPort;

  constructor(crypto: CryptoPort) {
    super();
    this.crypto = crypto;
  }

  async getOrCreate(): Promise<string> {
    const stored = window.localStorage.getItem(CryptoPort.ANON_ID_STORAGE_KEY);
    if (stored !== null) {
      return stored;
    }
    return this.generate();
  }

  async rotate(): Promise<string> {
    this.clear();
    return this.generate();
  }

  clear(): void {
    window.localStorage.removeItem(CryptoPort.ANON_ID_STORAGE_KEY);
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  private async generate(): Promise<string> {
    const uuid = this.crypto.randomUUID();
    const hash = await this.crypto.sha256Hex(uuid);
    window.localStorage.setItem(CryptoPort.ANON_ID_STORAGE_KEY, hash);
    return hash;
  }
}
