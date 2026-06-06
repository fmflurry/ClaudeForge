import { Injectable } from '@angular/core';
import { CryptoPort } from '../../domain/ports/crypto.port';

/**
 * Production CryptoPort implementation using the Web Crypto API.
 * Available in all modern browsers and Node 22+.
 */
@Injectable()
export class WebCryptoAdapter extends CryptoPort {
  randomUUID(): string {
    return globalThis.crypto.randomUUID();
  }

  sha256Hex(input: string): Promise<string> {
    return globalThis.crypto.subtle
      .digest('SHA-256', new TextEncoder().encode(input))
      .then((buf) =>
        Array.from(new Uint8Array(buf))
          .map((b) => b.toString(16).padStart(2, '0'))
          .join(''),
      );
  }
}
