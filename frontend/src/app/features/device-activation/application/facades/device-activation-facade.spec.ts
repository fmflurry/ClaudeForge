/**
 * RED tests — DeviceActivationFacade
 *
 * Expected production files (DO NOT exist yet — tests will FAIL):
 *   src/app/features/device-activation/application/facades/device-activation.facade.ts
 *   src/app/features/device-activation/domain/ports/device-activation.port.ts  (port)
 *
 * GREEN contract:
 *
 *   export type DeviceActivationStatus =
 *     | 'idle'
 *     | 'submitting'
 *     | 'approved'
 *     | 'error';
 *
 *   export type DeviceActivationErrorReason =
 *     | 'invalid'
 *     | 'not-found'
 *     | 'already-approved'
 *     | 'expired'
 *     | 'unauthorized'
 *     | 'unknown';
 *
 *   @Injectable()
 *   export class DeviceActivationFacade {
 *     // Read-only signal getters:
 *     get status(): Signal<DeviceActivationStatus>
 *       — starts as 'idle'; becomes 'submitting' during approve(); then
 *         'approved' on success, 'error' on any failure.
 *
 *     get errorReason(): Signal<DeviceActivationErrorReason | undefined>
 *       — undefined when idle/submitting/approved;
 *         set to the typed reason on error.
 *
 *     // Public methods (components call ONLY these):
 *     approve(userCode: string): void
 *       — sets status = 'submitting'
 *       — calls DeviceActivationPort.approve(userCode)
 *       — on result { kind: 'Approved' }       → status = 'approved', errorReason = undefined
 *       — on result { kind: 'Invalid' }         → status = 'error', errorReason = 'invalid'
 *       — on result { kind: 'NotFound' }        → status = 'error', errorReason = 'not-found'
 *       — on result { kind: 'AlreadyApproved' } → status = 'error', errorReason = 'already-approved'
 *       — on result { kind: 'Expired' }         → status = 'error', errorReason = 'expired'
 *       — on result { kind: 'Unauthorized' }    → status = 'error', errorReason = 'unauthorized'
 *       — on Observable error (network/etc.)   → status = 'error', errorReason = 'unknown'
 *
 *     reset(): void
 *       — returns status to 'idle' and errorReason to undefined
 *
 *     CONSTRAINTS:
 *     - Inject DeviceActivationPort (not the adapter directly).
 *     - All signal mutations are immutable (no shared mutable objects).
 *     - Components consume DeviceActivationFacade only.
 *   }
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { DeviceActivationFacade } from './device-activation.facade';
import { DeviceActivationPort } from '../../domain/ports/device-activation.port';
import type { DeviceApprovalResult } from '../../domain/ports/device-activation.port';

// ---------------------------------------------------------------------------
// Fake DeviceActivationPort
// ---------------------------------------------------------------------------

@Injectable()
class FakeDeviceActivationPort extends DeviceActivationPort {
  resultToReturn: DeviceApprovalResult = { kind: 'Approved' };
  shouldThrow = false;

  approve(_userCode: string): Observable<DeviceApprovalResult> {
    if (this.shouldThrow) {
      return throwError(() => new Error('Network error'));
    }
    return of(this.resultToReturn);
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

interface TestHarness {
  facade: DeviceActivationFacade;
  port: FakeDeviceActivationPort;
}

function setupHarness(): TestHarness {
  TestBed.resetTestingModule();
  const port = new FakeDeviceActivationPort();
  TestBed.configureTestingModule({
    providers: [
      DeviceActivationFacade,
      { provide: DeviceActivationPort, useValue: port },
    ],
  });
  return {
    facade: TestBed.inject(DeviceActivationFacade),
    port,
  };
}

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — initial signal values', () => {
  it('status should be "idle" initially', () => {
    const { facade } = setupHarness();
    expect(facade.status()).toBe('idle');
  });

  it('errorReason should be undefined initially', () => {
    const { facade } = setupHarness();
    expect(facade.errorReason()).toBeUndefined();
  });

  it('status should be a callable signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.status).toBe('function');
  });

  it('errorReason should be a callable signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.errorReason).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// approve() → Approved
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — approve() success (Approved)', () => {
  it('should set status to "approved" when port returns { kind: "Approved" }', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Approved' };
    facade.approve('VALID-CODE');
    expect(facade.status()).toBe('approved');
  });

  it('should keep errorReason as undefined after Approved', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Approved' };
    facade.approve('VALID-CODE');
    expect(facade.errorReason()).toBeUndefined();
  });

  it('should not throw when approve() succeeds', () => {
    const { facade } = setupHarness();
    expect(() => facade.approve('VALID-CODE')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// approve() → Invalid (400)
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — approve() maps Invalid', () => {
  it('should set status to "error" when port returns { kind: "Invalid" }', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Invalid' };
    facade.approve('bad');
    expect(facade.status()).toBe('error');
  });

  it('should set errorReason to "invalid" when result is Invalid', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Invalid' };
    facade.approve('bad');
    expect(facade.errorReason()).toBe('invalid');
  });
});

// ---------------------------------------------------------------------------
// approve() → NotFound (404)
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — approve() maps NotFound', () => {
  it('should set status to "error" when port returns { kind: "NotFound" }', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'NotFound' };
    facade.approve('UNKNOWN');
    expect(facade.status()).toBe('error');
  });

  it('should set errorReason to "not-found" when result is NotFound', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'NotFound' };
    facade.approve('UNKNOWN');
    expect(facade.errorReason()).toBe('not-found');
  });
});

// ---------------------------------------------------------------------------
// approve() → AlreadyApproved (409)
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — approve() maps AlreadyApproved', () => {
  it('should set status to "error" when port returns { kind: "AlreadyApproved" }', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'AlreadyApproved' };
    facade.approve('USED-CODE');
    expect(facade.status()).toBe('error');
  });

  it('should set errorReason to "already-approved" when result is AlreadyApproved', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'AlreadyApproved' };
    facade.approve('USED-CODE');
    expect(facade.errorReason()).toBe('already-approved');
  });
});

// ---------------------------------------------------------------------------
// approve() → Expired (410)
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — approve() maps Expired', () => {
  it('should set status to "error" when port returns { kind: "Expired" }', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Expired' };
    facade.approve('OLD-CODE');
    expect(facade.status()).toBe('error');
  });

  it('should set errorReason to "expired" when result is Expired', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Expired' };
    facade.approve('OLD-CODE');
    expect(facade.errorReason()).toBe('expired');
  });
});

// ---------------------------------------------------------------------------
// approve() → Unauthorized (401/403)
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — approve() maps Unauthorized', () => {
  it('should set status to "error" when port returns { kind: "Unauthorized" }', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Unauthorized' };
    facade.approve('CODE');
    expect(facade.status()).toBe('error');
  });

  it('should set errorReason to "unauthorized" when result is Unauthorized', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Unauthorized' };
    facade.approve('CODE');
    expect(facade.errorReason()).toBe('unauthorized');
  });
});

// ---------------------------------------------------------------------------
// approve() → network / Observable error → unknown
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — approve() Observable error → unknown', () => {
  it('should set status to "error" on network error', () => {
    const { facade, port } = setupHarness();
    port.shouldThrow = true;
    facade.approve('CODE');
    expect(facade.status()).toBe('error');
  });

  it('should set errorReason to "unknown" on network error', () => {
    const { facade, port } = setupHarness();
    port.shouldThrow = true;
    facade.approve('CODE');
    expect(facade.errorReason()).toBe('unknown');
  });

  it('should not propagate the Observable error (no unhandled throw)', () => {
    const { facade, port } = setupHarness();
    port.shouldThrow = true;
    expect(() => facade.approve('CODE')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// reset()
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — reset()', () => {
  it('reset() should return status to "idle"', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Expired' };
    facade.approve('CODE');
    expect(facade.status()).toBe('error');

    facade.reset();
    expect(facade.status()).toBe('idle');
  });

  it('reset() should clear errorReason to undefined', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'NotFound' };
    facade.approve('CODE');
    expect(facade.errorReason()).toBe('not-found');

    facade.reset();
    expect(facade.errorReason()).toBeUndefined();
  });

  it('reset() should not throw when called from idle state', () => {
    const { facade } = setupHarness();
    expect(() => facade.reset()).not.toThrow();
  });

  it('reset() after approved should return to idle', () => {
    const { facade } = setupHarness();
    facade.approve('VALID-CODE');
    expect(facade.status()).toBe('approved');

    facade.reset();
    expect(facade.status()).toBe('idle');
  });
});

// ---------------------------------------------------------------------------
// Submitting state (sync fake resolves immediately; test the interface contract)
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — submitting state contract', () => {
  it('approve() should be exposed as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.approve).toBe('function');
  });

  it('reset() should be exposed as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.reset).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// Multiple consecutive calls
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — multiple approve() calls', () => {
  it('should allow re-approval after error (reset then approve again)', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'NotFound' };
    facade.approve('BAD');
    expect(facade.status()).toBe('error');

    facade.reset();
    port.resultToReturn = { kind: 'Approved' };
    facade.approve('GOOD');
    expect(facade.status()).toBe('approved');
    expect(facade.errorReason()).toBeUndefined();
  });

  it('second approve() without reset should overwrite prior result', () => {
    const { facade, port } = setupHarness();
    port.resultToReturn = { kind: 'Expired' };
    facade.approve('OLD');
    expect(facade.errorReason()).toBe('expired');

    port.resultToReturn = { kind: 'Approved' };
    facade.approve('NEW');
    expect(facade.status()).toBe('approved');
    expect(facade.errorReason()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('DeviceActivationFacade — architecture boundary', () => {
  it('should be instantiable with only DeviceActivationPort provided (no HttpClient)', () => {
    const { facade } = setupHarness();
    expect(facade).toBeDefined();
  });

  it('should NOT expose DeviceActivationPort as a public property', () => {
    const { facade } = setupHarness();
    const proto = Object.getOwnPropertyNames(Object.getPrototypeOf(facade)).filter(
      (k) => k !== 'constructor',
    );
    expect(proto).not.toContain('port');
  });

  it('should expose status and errorReason as the documented public signals', () => {
    const { facade } = setupHarness();
    const protoKeys = Object.getOwnPropertyNames(Object.getPrototypeOf(facade)).filter(
      (k) => k !== 'constructor',
    );
    expect(protoKeys).toContain('status');
    expect(protoKeys).toContain('errorReason');
    expect(protoKeys).toContain('approve');
    expect(protoKeys).toContain('reset');
  });
});
