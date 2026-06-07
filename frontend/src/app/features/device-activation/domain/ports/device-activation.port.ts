/**
 * Abstract port for the DeviceActivation domain.
 * Infrastructure adapters implement this interface.
 * The facade depends on this abstract class, not the adapter directly.
 */

import { Observable } from 'rxjs';

// ---------------------------------------------------------------------------
// Discriminated result union
// ---------------------------------------------------------------------------

export type DeviceApprovalResult =
  | { kind: 'Approved' }
  | { kind: 'Invalid' }
  | { kind: 'NotFound' }
  | { kind: 'AlreadyApproved' }
  | { kind: 'Expired' }
  | { kind: 'Unauthorized' };

// ---------------------------------------------------------------------------
// Status / error-reason types (consumed by facade and component)
// ---------------------------------------------------------------------------

export type DeviceActivationStatus = 'idle' | 'submitting' | 'approved' | 'error';

export type DeviceActivationErrorReason =
  | 'invalid'
  | 'not-found'
  | 'already-approved'
  | 'expired'
  | 'unauthorized'
  | 'unknown';

// ---------------------------------------------------------------------------
// Abstract port
// ---------------------------------------------------------------------------

export abstract class DeviceActivationPort {
  abstract approve(userCode: string): Observable<DeviceApprovalResult>;
}
