/**
 * RED tests — Task 16.3: TelemetryStore + TelemetryFacade
 *
 * Expected production files (DO NOT exist yet — tests WILL FAIL to compile/resolve):
 *   src/app/features/telemetry/application/store/telemetry.store.ts
 *   src/app/features/telemetry/application/facades/telemetry.facade.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // telemetry.store.ts
 *   export enum TelemetryStoreEnum {
 *     PREFERENCE = 'PREFERENCE',
 *     ANON_ID    = 'ANON_ID',
 *   }
 *
 *   export interface TelemetryState {
 *     [TelemetryStoreEnum.PREFERENCE]: ResourceState<boolean>;  // true = enabled
 *     [TelemetryStoreEnum.ANON_ID]:    ResourceState<string>;
 *   }
 *
 *   @Injectable({ providedIn: 'root' })
 *   export class TelemetryStore extends BaseStore<typeof TelemetryStoreEnum, TelemetryState> { }
 *
 *   // telemetry.facade.ts
 *   @Injectable()
 *   export class TelemetryFacade {
 *     // Signals (readonly):
 *     get isEnabled(): Signal<boolean>   — true when NOT disabled (default: true)
 *     get isDisabled(): Signal<boolean>  — mirrors opt-out flag
 *     get anonId(): Signal<string | undefined>  — current anon-id (undefined = not yet loaded)
 *
 *     // Methods:
 *     init(): void
 *       — Loads preference from TelemetryPreferencePort; loads/creates anonId via AnonIdPort.
 *         Must be called once at app bootstrap (or lazily on first recordEvent).
 *
 *     enable(): void
 *       — Sets TelemetryPreferencePort.setDisabled(false); rotates anon ID via AnonIdPort.rotate().
 *         Updates store. Does NOT throw.
 *
 *     disable(): void
 *       — Sets TelemetryPreferencePort.setDisabled(true). Updates store. Does NOT throw.
 *
 *     recordEvent(eventType: string, pluginId: string, version?: string): void
 *       — Fire-and-forget: if disabled, NO-OP (ApiClient.postTelemetryEvent must NOT be called).
 *         If enabled, calls ApiClient.postTelemetryEvent with an IngestTelemetryRequestDto
 *         containing { eventType, pluginId, version: version ?? null, anonClientId, clientOs: null, clientArch: null }.
 *         Errors from the HTTP call are swallowed (telemetry MUST NOT break UX).
 *         The method itself is synchronous (subscribe internally, no await).
 *
 *     NOTE: The client does NOT auto-POST a 'download' event on download.
 *     Download counting is handled server-side by the download endpoint.
 *     Only explicit recordEvent('install', ...) calls from application code trigger
 *     an ingest POST. This single-increment path prevents double-counting.
 *   }
 *
 *   INJECTION TOKENS required by coder:
 *     TelemetryPreferencePort  — injected via abstract class token (already exists in shared)
 *     AnonIdPort               — injected via abstract class token (defined in telemetry domain)
 *     ApiClient                — injected from shared infrastructure
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { TelemetryStore, TelemetryStoreEnum } from './telemetry.store';
import type { TelemetryState } from './telemetry.store';
import { TelemetryFacade } from '../facades/telemetry.facade';
import { TelemetryPreferencePort } from '../../../../shared/domain/ports/telemetry-preference.port';
import { AnonIdPort } from '../../domain/ports/anon-id.port';
import { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { IngestTelemetryRequestDto } from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

/**
 * In-memory TelemetryPreferencePort — does not touch localStorage.
 */
@Injectable()
class FakeTelemetryPreferencePort extends TelemetryPreferencePort {
  private disabled = false;

  isDisabled(): boolean {
    return this.disabled;
  }
  setDisabled(disabled: boolean): void {
    this.disabled = disabled;
  }
  clear(): void {
    this.disabled = false;
  }
}

const FAKE_ANON_ID_1 = 'aaaaaaaabbbbbbbbccccccccddddddddeeeeeeeeffffffff0000000011111111';
const FAKE_ANON_ID_2 = '2222222233333333444444445555555566666666777777778888888899999999';

/**
 * In-memory AnonIdPort stub.
 */
@Injectable()
class FakeAnonIdPort extends AnonIdPort {
  private stored: string | null = null;
  private queue: string[] = [FAKE_ANON_ID_1, FAKE_ANON_ID_2];

  rotateCalls = 0;
  getOrCreateCalls = 0;

  getOrCreate(): Promise<string> {
    this.getOrCreateCalls++;
    if (!this.stored) {
      const next = this.queue.shift() ?? FAKE_ANON_ID_1;
      this.stored = next;
    }
    return Promise.resolve(this.stored);
  }

  rotate(): Promise<string> {
    this.rotateCalls++;
    const next = this.queue.shift() ?? FAKE_ANON_ID_2;
    this.stored = next;
    return Promise.resolve(this.stored);
  }

  clear(): void {
    this.stored = null;
  }
}

/**
 * Fake ApiClient — records calls to postTelemetryEvent.
 */
@Injectable()
class FakeApiClient {
  telemetryPosts: IngestTelemetryRequestDto[] = [];
  shouldError = false;

  postTelemetryEvent(req: IngestTelemetryRequestDto): Observable<void> {
    if (this.shouldError) {
      return throwError(() => new Error('Network error'));
    }
    this.telemetryPosts.push(req);
    return of(undefined);
  }
}

// ---------------------------------------------------------------------------
// Import ApiClient token for DI
// ---------------------------------------------------------------------------

import { ApiClient } from '../../../../shared/infrastructure/http/api-client';

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

interface TestHarness {
  store: TelemetryStore;
  facade: TelemetryFacade;
  prefPort: FakeTelemetryPreferencePort;
  anonIdPort: FakeAnonIdPort;
  apiClient: FakeApiClient;
}

function setupHarness(options: { initiallyDisabled?: boolean } = {}): TestHarness {
  const prefPort = new FakeTelemetryPreferencePort();
  if (options.initiallyDisabled) prefPort.setDisabled(true);
  const anonIdPort = new FakeAnonIdPort();
  const apiClient = new FakeApiClient();

  TestBed.configureTestingModule({
    providers: [
      TelemetryStore,
      TelemetryFacade,
      { provide: TelemetryPreferencePort, useValue: prefPort },
      { provide: AnonIdPort, useValue: anonIdPort },
      { provide: ApiClient, useValue: apiClient },
    ],
  });

  return {
    store: TestBed.inject(TelemetryStore),
    facade: TestBed.inject(TelemetryFacade),
    prefPort,
    anonIdPort,
    apiClient,
  };
}

// ---------------------------------------------------------------------------
// TelemetryStore — enum keys
// ---------------------------------------------------------------------------

describe('TelemetryStore — enum keys', () => {
  it('should have PREFERENCE key', () => {
    expect(TelemetryStoreEnum.PREFERENCE).toBe('PREFERENCE');
  });

  it('should have ANON_ID key', () => {
    expect(TelemetryStoreEnum.ANON_ID).toBe('ANON_ID');
  });
});

describe('TelemetryStore — initial state', () => {
  it('PREFERENCE key should start as empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [TelemetryStore] });
    const store = TestBed.inject(TelemetryStore);
    const state: ResourceState<boolean> = store.get(TelemetryStoreEnum.PREFERENCE)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
  });

  it('ANON_ID key should start as empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [TelemetryStore] });
    const store = TestBed.inject(TelemetryStore);
    const state: ResourceState<string> = store.get(TelemetryStoreEnum.ANON_ID)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
  });

  it('TelemetryState type should accept ResourceState<boolean> for PREFERENCE', () => {
    TestBed.configureTestingModule({ providers: [TelemetryStore] });
    const store = TestBed.inject(TelemetryStore);
    const partial: Partial<TelemetryState[typeof TelemetryStoreEnum.PREFERENCE]> = {
      data: true,
      status: 'Success',
    };
    store.update(TelemetryStoreEnum.PREFERENCE, partial);
    expect(store.get(TelemetryStoreEnum.PREFERENCE)().status).toBe('Success');
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — signals: initial values before init()
// ---------------------------------------------------------------------------

describe('TelemetryFacade — initial signal values (before init)', () => {
  it('isEnabled should return true by default (telemetry on by default)', () => {
    const { facade } = setupHarness();
    expect(facade.isEnabled()).toBe(true);
  });

  it('isDisabled should return false by default', () => {
    const { facade } = setupHarness();
    expect(facade.isDisabled()).toBe(false);
  });

  it('anonId should return undefined before init()', () => {
    const { facade } = setupHarness();
    expect(facade.anonId()).toBeUndefined();
  });

  it('isEnabled should reflect initially-disabled preference when init() is called', async () => {
    const { facade } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    expect(facade.isEnabled()).toBe(false);
    expect(facade.isDisabled()).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — init()
// ---------------------------------------------------------------------------

describe('TelemetryFacade — init()', () => {
  it('should set isEnabled to true when preference is not disabled', async () => {
    const { facade } = setupHarness();
    await facade.init();
    expect(facade.isEnabled()).toBe(true);
  });

  it('should set anonId to a 64-char hex string after init()', async () => {
    const { facade } = setupHarness();
    await facade.init();
    expect(facade.anonId()).toBe(FAKE_ANON_ID_1);
  });

  it('should call AnonIdPort.getOrCreate() during init()', async () => {
    const { facade, anonIdPort } = setupHarness();
    await facade.init();
    expect(anonIdPort.getOrCreateCalls).toBeGreaterThan(0);
  });

  it('should not throw when init() is called multiple times', async () => {
    const { facade } = setupHarness();
    await expect(facade.init()).resolves.not.toThrow();
    await expect(facade.init()).resolves.not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — disable()
// ---------------------------------------------------------------------------

describe('TelemetryFacade — disable()', () => {
  it('should set isDisabled to true', async () => {
    const { facade } = setupHarness();
    await facade.init();
    facade.disable();
    expect(facade.isDisabled()).toBe(true);
    expect(facade.isEnabled()).toBe(false);
  });

  it('should persist disabled=true via TelemetryPreferencePort', async () => {
    const { facade, prefPort } = setupHarness();
    await facade.init();
    facade.disable();
    expect(prefPort.isDisabled()).toBe(true);
  });

  it('should not throw even if init() was never called', () => {
    const { facade } = setupHarness();
    expect(() => facade.disable()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — enable() — rotates anon ID
// ---------------------------------------------------------------------------

describe('TelemetryFacade — enable()', () => {
  it('should set isEnabled to true after re-enabling', async () => {
    const { facade } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    expect(facade.isEnabled()).toBe(false);
    await facade.enable();
    expect(facade.isEnabled()).toBe(true);
  });

  it('should set isDisabled to false after re-enabling', async () => {
    const { facade, prefPort } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    await facade.enable();
    expect(facade.isDisabled()).toBe(false);
    expect(prefPort.isDisabled()).toBe(false);
  });

  it('should rotate the anon ID when re-enabling', async () => {
    const { facade, anonIdPort } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    const idBefore = facade.anonId();
    await facade.enable();
    expect(anonIdPort.rotateCalls).toBeGreaterThan(0);
    // The anon id after enable should differ from the one before (rotation)
    expect(facade.anonId()).not.toBe(idBefore);
  });

  it('rotated anon ID should be set in the store after enable()', async () => {
    const { facade } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    await facade.enable();
    expect(facade.anonId()).toBe(FAKE_ANON_ID_2);
  });

  it('should not throw when enable() is called when already enabled', async () => {
    const { facade } = setupHarness();
    await facade.init();
    await expect(facade.enable()).resolves.not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — recordEvent() when ENABLED
// ---------------------------------------------------------------------------

describe('TelemetryFacade — recordEvent when enabled', () => {
  it('should call ApiClient.postTelemetryEvent when enabled', async () => {
    const { facade, apiClient } = setupHarness();
    await facade.init();
    facade.recordEvent('install', 'my-plugin', '1.0.0');
    expect(apiClient.telemetryPosts).toHaveLength(1);
  });

  it('posted payload should contain eventType, pluginId, version', async () => {
    const { facade, apiClient } = setupHarness();
    await facade.init();
    facade.recordEvent('install', 'my-plugin', '1.0.0');
    const posted = apiClient.telemetryPosts[0];
    expect(posted.eventType).toBe('install');
    expect(posted.pluginId).toBe('my-plugin');
    expect(posted.version).toBe('1.0.0');
  });

  it('posted payload should include the current anonClientId', async () => {
    const { facade, apiClient } = setupHarness();
    await facade.init();
    facade.recordEvent('install', 'my-plugin', '1.0.0');
    expect(apiClient.telemetryPosts[0].anonClientId).toBe(FAKE_ANON_ID_1);
  });

  it('posted payload should have null clientOs and clientArch (no fingerprinting)', async () => {
    const { facade, apiClient } = setupHarness();
    await facade.init();
    facade.recordEvent('install', 'my-plugin', '1.0.0');
    expect(apiClient.telemetryPosts[0].clientOs).toBeNull();
    expect(apiClient.telemetryPosts[0].clientArch).toBeNull();
  });

  it('version should be null when not provided', async () => {
    const { facade, apiClient } = setupHarness();
    await facade.init();
    facade.recordEvent('install', 'my-plugin');
    expect(apiClient.telemetryPosts[0].version).toBeNull();
  });

  it('should not throw and should not block when the HTTP call errors', async () => {
    const { facade, apiClient } = setupHarness();
    apiClient.shouldError = true;
    await facade.init();
    expect(() => facade.recordEvent('install', 'my-plugin')).not.toThrow();
  });

  it('HTTP errors from recordEvent must be swallowed (fire-and-forget)', async () => {
    const { facade, apiClient } = setupHarness();
    apiClient.shouldError = true;
    await facade.init();
    // No unhandled rejection should propagate
    facade.recordEvent('install', 'my-plugin');
    // If we reach here without an error, fire-and-forget is working
    expect(true).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — recordEvent() when DISABLED (critical: MUST be NO-OP)
// ---------------------------------------------------------------------------

describe('TelemetryFacade — recordEvent when disabled (CRITICAL: no HTTP)', () => {
  it('should NOT call ApiClient.postTelemetryEvent when disabled', async () => {
    const { facade, apiClient } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    facade.recordEvent('install', 'my-plugin', '1.0.0');
    expect(apiClient.telemetryPosts).toHaveLength(0);
  });

  it('should NOT call postTelemetryEvent after calling disable()', async () => {
    const { facade, apiClient } = setupHarness();
    await facade.init();
    facade.disable();
    facade.recordEvent('install', 'my-plugin');
    expect(apiClient.telemetryPosts).toHaveLength(0);
  });

  it('should not throw when recordEvent is called in disabled state', async () => {
    const { facade } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    expect(() => facade.recordEvent('install', 'my-plugin')).not.toThrow();
  });

  it('no HTTP after multiple recordEvent calls when disabled', async () => {
    const { facade, apiClient } = setupHarness({ initiallyDisabled: true });
    await facade.init();
    facade.recordEvent('install', 'plugin-a');
    facade.recordEvent('install', 'plugin-b');
    facade.recordEvent('usage', 'plugin-a', '2.0.0');
    expect(apiClient.telemetryPosts).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — download event is NOT auto-posted by client
// ---------------------------------------------------------------------------

describe('TelemetryFacade — no auto-download event (design §5 single-increment path)', () => {
  it('client should NOT auto-post a "download" event — downloads are counted server-side', async () => {
    // This test verifies the design constraint: the client only posts events
    // via explicit recordEvent calls. There must be no internal wiring that
    // auto-fires on "download". The TelemetryFacade has no "onDownload" method.
    const { facade } = setupHarness();
    await facade.init();

    // Verify TelemetryFacade exposes NO method that auto-fires download events
    expect(typeof (facade as unknown as Record<string, unknown>)['onDownload']).not.toBe('function');
    expect(typeof (facade as unknown as Record<string, unknown>)['recordDownload']).not.toBe('function');
    expect(typeof (facade as unknown as Record<string, unknown>)['autoRecordDownload']).not.toBe('function');
  });

  it('recordEvent with "install" eventType is the only explicit ingest path', async () => {
    const { facade, apiClient } = setupHarness();
    await facade.init();
    facade.recordEvent('install', 'some-plugin', '3.0.0');
    expect(apiClient.telemetryPosts).toHaveLength(1);
    expect(apiClient.telemetryPosts[0].eventType).toBe('install');
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — public API surface
// ---------------------------------------------------------------------------

describe('TelemetryFacade — public API surface', () => {
  it('should expose isEnabled as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.isEnabled).toBe('function');
  });

  it('should expose isDisabled as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.isDisabled).toBe('function');
  });

  it('should expose anonId as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.anonId).toBe('function');
  });

  it('should expose init as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.init).toBe('function');
  });

  it('should expose enable as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.enable).toBe('function');
  });

  it('should expose disable as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.disable).toBe('function');
  });

  it('should expose recordEvent as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.recordEvent).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// TelemetryFacade — architecture boundary
// ---------------------------------------------------------------------------

describe('TelemetryFacade — architecture boundary', () => {
  it('should NOT require ApiClient directly in TestBed beyond the useValue stub', () => {
    // If the test harness passes and the module does not throw for missing
    // HttpClient, the facade correctly depends only on the abstract ports.
    const { facade } = setupHarness();
    expect(facade).toBeDefined();
  });
});
