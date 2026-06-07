/**
 * RED tests — DeviceActivationPort + DeviceActivationHttpAdapter
 *
 * Expected production files (DO NOT exist yet — tests will FAIL):
 *   src/app/features/device-activation/domain/ports/device-activation.port.ts
 *   src/app/features/device-activation/infrastructure/adapter/device-activation-http.adapter.ts
 *
 * GREEN contract:
 *
 *   // device-activation.port.ts
 *   export type DeviceApprovalResult =
 *     | { kind: 'Approved' }
 *     | { kind: 'Invalid' }
 *     | { kind: 'NotFound' }
 *     | { kind: 'AlreadyApproved' }
 *     | { kind: 'Expired' }
 *     | { kind: 'Unauthorized' };
 *
 *   export abstract class DeviceActivationPort {
 *     abstract approve(userCode: string): Observable<DeviceApprovalResult>;
 *   }
 *
 *   // device-activation-http.adapter.ts
 *   @Injectable()
 *   export class DeviceActivationHttpAdapter extends DeviceActivationPort {
 *     approve(userCode: string): Observable<DeviceApprovalResult>
 *       — POSTs { user_code: userCode } to `${baseUrl}/auth/device/approve`
 *       — withCredentials: true (bearer token flows through the auth interceptor)
 *       — 200 → { kind: 'Approved' }
 *       — 400 → { kind: 'Invalid' }
 *       — 404 → { kind: 'NotFound' }
 *       — 409 → { kind: 'AlreadyApproved' }
 *       — 410 → { kind: 'Expired' }
 *       — 401/403 → { kind: 'Unauthorized' }
 *       — errors are caught with catchError and mapped; no unhandled throws
 *   }
 */

import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../../../core/config/api-config';
import { DeviceActivationPort } from '../../domain/ports/device-activation.port';
import { DeviceActivationHttpAdapter } from './device-activation-http.adapter';
import type { DeviceApprovalResult } from '../../domain/ports/device-activation.port';

const BASE = 'https://api.test';
const ENDPOINT = `${BASE}/auth/device/approve`;

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): {
  adapter: DeviceActivationHttpAdapter;
  http: HttpTestingController;
} {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: BASE },
      { provide: DeviceActivationPort, useClass: DeviceActivationHttpAdapter },
      DeviceActivationHttpAdapter,
    ],
  });
  return {
    adapter: TestBed.inject(DeviceActivationHttpAdapter),
    http: TestBed.inject(HttpTestingController),
  };
}

// ---------------------------------------------------------------------------
// HTTP shape: URL, method, body
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — HTTP shape', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('should POST to /auth/device/approve', () => {
    const { adapter, http } = setup();
    adapter.approve('USER-CODE-1').subscribe();

    const req = http.expectOne(ENDPOINT);
    expect(req.request.method).toBe('POST');
    req.flush({}, { status: 200, statusText: 'OK' });
  });

  it('should send { userCode: userCode } in request body', () => {
    const { adapter, http } = setup();
    adapter.approve('ABCD-1234').subscribe();

    const req = http.expectOne(ENDPOINT);
    expect(req.request.body).toEqual({ userCode: 'ABCD-1234' });
    req.flush({}, { status: 200, statusText: 'OK' });
  });

  it('should send withCredentials: true so the auth interceptor can attach the bearer token', () => {
    const { adapter, http } = setup();
    adapter.approve('CODE-1').subscribe();

    const req = http.expectOne(ENDPOINT);
    expect(req.request.withCredentials).toBe(true);
    req.flush({}, { status: 200, statusText: 'OK' });
  });

  it('should send the exact userCode value passed to approve()', () => {
    const { adapter, http } = setup();
    const code = 'XYZ-9999';
    adapter.approve(code).subscribe();

    const req = http.expectOne(ENDPOINT);
    expect((req.request.body as Record<string, unknown>)['userCode']).toBe(code);
    req.flush({}, { status: 200, statusText: 'OK' });
  });
});

// ---------------------------------------------------------------------------
// Response mapping: 200 → Approved
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — 200 → Approved', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('should emit { kind: "Approved" } on 200 response', () => {
    const { adapter, http } = setup();
    let result: DeviceApprovalResult | undefined;
    adapter.approve('VALID-CODE').subscribe((r) => (result = r));

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 200, statusText: 'OK' });

    expect(result).toEqual({ kind: 'Approved' });
  });

  it('should complete the observable after Approved', () => {
    const { adapter, http } = setup();
    let completed = false;
    adapter.approve('VALID-CODE').subscribe({ complete: () => (completed = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 200, statusText: 'OK' });

    expect(completed).toBe(true);
  });

  it('should not throw on 200', () => {
    const { adapter, http } = setup();
    let threw = false;
    adapter.approve('CODE').subscribe({ error: () => (threw = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 200, statusText: 'OK' });

    expect(threw).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Response mapping: 400 → Invalid
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — 400 → Invalid', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('should emit { kind: "Invalid" } on 400 response (blank/malformed code)', () => {
    const { adapter, http } = setup();
    let result: DeviceApprovalResult | undefined;
    adapter.approve('').subscribe((r) => (result = r));

    const req = http.expectOne(ENDPOINT);
    req.flush({ error: 'Bad Request' }, { status: 400, statusText: 'Bad Request' });

    expect(result).toEqual({ kind: 'Invalid' });
  });

  it('should not throw on 400 (error is mapped to a result)', () => {
    const { adapter, http } = setup();
    let threw = false;
    adapter.approve('bad').subscribe({ error: () => (threw = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 400, statusText: 'Bad Request' });

    expect(threw).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Response mapping: 404 → NotFound
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — 404 → NotFound', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('should emit { kind: "NotFound" } on 404 response (unknown code)', () => {
    const { adapter, http } = setup();
    let result: DeviceApprovalResult | undefined;
    adapter.approve('UNKNOWN-CODE').subscribe((r) => (result = r));

    const req = http.expectOne(ENDPOINT);
    req.flush({ error: 'Not Found' }, { status: 404, statusText: 'Not Found' });

    expect(result).toEqual({ kind: 'NotFound' });
  });

  it('should not throw on 404', () => {
    const { adapter, http } = setup();
    let threw = false;
    adapter.approve('CODE').subscribe({ error: () => (threw = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 404, statusText: 'Not Found' });

    expect(threw).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Response mapping: 409 → AlreadyApproved
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — 409 → AlreadyApproved', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('should emit { kind: "AlreadyApproved" } on 409 response', () => {
    const { adapter, http } = setup();
    let result: DeviceApprovalResult | undefined;
    adapter.approve('USED-CODE').subscribe((r) => (result = r));

    const req = http.expectOne(ENDPOINT);
    req.flush({ error: 'Conflict' }, { status: 409, statusText: 'Conflict' });

    expect(result).toEqual({ kind: 'AlreadyApproved' });
  });

  it('should not throw on 409', () => {
    const { adapter, http } = setup();
    let threw = false;
    adapter.approve('CODE').subscribe({ error: () => (threw = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 409, statusText: 'Conflict' });

    expect(threw).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Response mapping: 410 → Expired
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — 410 → Expired', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('should emit { kind: "Expired" } on 410 response', () => {
    const { adapter, http } = setup();
    let result: DeviceApprovalResult | undefined;
    adapter.approve('EXPIRED-CODE').subscribe((r) => (result = r));

    const req = http.expectOne(ENDPOINT);
    req.flush({ error: 'Gone' }, { status: 410, statusText: 'Gone' });

    expect(result).toEqual({ kind: 'Expired' });
  });

  it('should not throw on 410', () => {
    const { adapter, http } = setup();
    let threw = false;
    adapter.approve('CODE').subscribe({ error: () => (threw = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 410, statusText: 'Gone' });

    expect(threw).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Response mapping: 401/403 → Unauthorized
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — 401 → Unauthorized', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('should emit { kind: "Unauthorized" } on 401 response', () => {
    const { adapter, http } = setup();
    let result: DeviceApprovalResult | undefined;
    adapter.approve('CODE').subscribe((r) => (result = r));

    const req = http.expectOne(ENDPOINT);
    req.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(result).toEqual({ kind: 'Unauthorized' });
  });

  it('should emit { kind: "Unauthorized" } on 403 response', () => {
    const { adapter, http } = setup();
    let result: DeviceApprovalResult | undefined;
    adapter.approve('CODE').subscribe((r) => (result = r));

    const req = http.expectOne(ENDPOINT);
    req.flush({ error: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });

    expect(result).toEqual({ kind: 'Unauthorized' });
  });

  it('should not throw on 401', () => {
    const { adapter, http } = setup();
    let threw = false;
    adapter.approve('CODE').subscribe({ error: () => (threw = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(threw).toBe(false);
  });

  it('should not throw on 403', () => {
    const { adapter, http } = setup();
    let threw = false;
    adapter.approve('CODE').subscribe({ error: () => (threw = true) });

    const req = http.expectOne(ENDPOINT);
    req.flush({}, { status: 403, statusText: 'Forbidden' });

    expect(threw).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Architecture: extends DeviceActivationPort
// ---------------------------------------------------------------------------

describe('DeviceActivationHttpAdapter — architecture', () => {
  it('should be an instance of DeviceActivationPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(DeviceActivationPort);
  });

  it('should expose approve() as a method', () => {
    const { adapter } = setup();
    expect(typeof adapter.approve).toBe('function');
  });
});
