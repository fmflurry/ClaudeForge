/**
 * Domain port for cryptographic operations.
 * Abstracted to allow deterministic fakes in tests.
 */
export abstract class CryptoPort {
  static readonly ANON_ID_STORAGE_KEY = 'plugin-marketplace:anon-id';

  /**
   * Generate a UUID v4 string.
   */
  abstract randomUUID(): string;

  /**
   * SHA-256 hash the input string; returns lowercase 64-char hex.
   */
  abstract sha256Hex(input: string): Promise<string>;
}
