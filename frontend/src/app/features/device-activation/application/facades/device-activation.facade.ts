/**
 * DeviceActivationFacade — the ONLY entry point for device-activation state in components.
 *
 * CONSTRAINTS:
 * - Injects DeviceActivationPort (never the adapter directly).
 * - All signal mutations use immutable patterns (signal.set with new values).
 * - Components consume DeviceActivationFacade only — never port or adapter.
 * - DeviceActivationPort is NOT exposed as a public property.
 */

import { inject, Injectable, Signal, signal } from '@angular/core';
import { catchError, Observable, of } from 'rxjs';
import { DeviceActivationPort } from '../../domain/ports/device-activation.port';
import type {
  DeviceActivationErrorReason,
  DeviceActivationStatus,
  DeviceApprovalResult,
} from '../../domain/ports/device-activation.port';

// Sentinel value used internally to distinguish Observable errors (network/unknown)
// from mapped domain results — never exposed outside this file.
const NETWORK_ERROR_SENTINEL = '__networkError' as const;
type NetworkErrorSentinel = typeof NETWORK_ERROR_SENTINEL;
type InternalResult = DeviceApprovalResult | { kind: NetworkErrorSentinel };

function mapResultToErrorReason(result: DeviceApprovalResult): DeviceActivationErrorReason | undefined {
  switch (result.kind) {
    case 'Approved':
      return undefined;
    case 'Invalid':
      return 'invalid';
    case 'NotFound':
      return 'not-found';
    case 'AlreadyApproved':
      return 'already-approved';
    case 'Expired':
      return 'expired';
    case 'Unauthorized':
      return 'unauthorized';
  }
}

@Injectable()
export class DeviceActivationFacade {
  private readonly port = inject(DeviceActivationPort);

  private readonly _status = signal<DeviceActivationStatus>('idle');
  private readonly _errorReason = signal<DeviceActivationErrorReason | undefined>(undefined);

  // ---------------------------------------------------------------------------
  // Signal getters (read-only)
  // ---------------------------------------------------------------------------

  get status(): Signal<DeviceActivationStatus> {
    return this._status.asReadonly();
  }

  get errorReason(): Signal<DeviceActivationErrorReason | undefined> {
    return this._errorReason.asReadonly();
  }

  // ---------------------------------------------------------------------------
  // Public methods
  // ---------------------------------------------------------------------------

  approve(userCode: string): void {
    this._status.set('submitting');
    this._errorReason.set(undefined);

    const networkErrorResult: InternalResult = { kind: NETWORK_ERROR_SENTINEL };
    const safe$: Observable<InternalResult> = (this.port.approve(userCode) as Observable<InternalResult>).pipe(
      catchError((): Observable<InternalResult> => of(networkErrorResult)),
    );

    safe$.subscribe((result: InternalResult) => {
      if (result.kind === NETWORK_ERROR_SENTINEL) {
        this._status.set('error');
        this._errorReason.set('unknown');
        return;
      }

      if (result.kind === 'Approved') {
        this._status.set('approved');
        this._errorReason.set(undefined);
      } else {
        this._status.set('error');
        this._errorReason.set(mapResultToErrorReason(result));
      }
    });
  }

  reset(): void {
    this._status.set('idle');
    this._errorReason.set(undefined);
  }
}
