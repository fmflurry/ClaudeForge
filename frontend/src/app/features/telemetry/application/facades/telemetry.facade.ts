import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, EMPTY } from 'rxjs';
import { TelemetryStore, TelemetryStoreEnum } from '../store/telemetry.store';
import { TelemetryPreferencePort } from '../../../../shared/domain/ports/telemetry-preference.port';
import { AnonIdPort } from '../../domain/ports/anon-id.port';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { IngestTelemetryRequestDto } from '../../../../shared/infrastructure/http/api-client.types';

/**
 * Facade for the Telemetry domain.
 * Components interact with this facade only — no direct store, port, or ApiClient access.
 *
 * Key design constraints:
 * - recordEvent is fire-and-forget: HTTP errors are swallowed, never thrown to UX.
 * - When disabled, recordEvent is a no-op — ApiClient is never called.
 * - Download events are NOT auto-posted by the client; only explicit recordEvent calls trigger ingest.
 */
@Injectable()
export class TelemetryFacade {
  private readonly store = inject(TelemetryStore);
  private readonly preferencePort = inject(TelemetryPreferencePort);
  private readonly anonIdPort = inject(AnonIdPort);
  private readonly apiClient = inject(ApiClient);
  private readonly destroyRef = inject(DestroyRef);

  // ---------------------------------------------------------------------------
  // Signal getters
  // ---------------------------------------------------------------------------

  /**
   * True when telemetry is NOT disabled.
   * Defaults to true (telemetry on by default).
   */
  get isEnabled(): Signal<boolean> {
    return computed(() => {
      const state = this.store.get(TelemetryStoreEnum.PREFERENCE)();
      if (state.data === undefined) {
        return true;
      }
      return state.data;
    });
  }

  /**
   * True when the user has opted out.
   */
  get isDisabled(): Signal<boolean> {
    return computed(() => !this.isEnabled());
  }

  /**
   * Current anonymous client ID, or undefined before init() completes.
   */
  get anonId(): Signal<string | undefined> {
    return computed(() => this.store.get(TelemetryStoreEnum.ANON_ID)().data);
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  /**
   * Loads preference and anon-id from their respective ports.
   * Safe to call multiple times — idempotent.
   */
  async init(): Promise<void> {
    const disabled = this.preferencePort.isDisabled();
    this.store.update(TelemetryStoreEnum.PREFERENCE, {
      data: !disabled,
      status: 'Success',
      isLoading: false,
    });

    const id = await this.anonIdPort.getOrCreate();
    this.store.update(TelemetryStoreEnum.ANON_ID, {
      data: id,
      status: 'Success',
      isLoading: false,
    });
  }

  /**
   * Opts the user back in:
   * - Persists enabled state via TelemetryPreferencePort.
   * - Rotates the anon ID (fresh identity on re-enable).
   */
  async enable(): Promise<void> {
    this.preferencePort.setDisabled(false);
    this.store.update(TelemetryStoreEnum.PREFERENCE, {
      data: true,
      status: 'Success',
      isLoading: false,
    });

    const newId = await this.anonIdPort.rotate();
    this.store.update(TelemetryStoreEnum.ANON_ID, {
      data: newId,
      status: 'Success',
      isLoading: false,
    });
  }

  /**
   * Opts the user out.
   * Persists disabled state. Does NOT clear the anon ID.
   */
  disable(): void {
    this.preferencePort.setDisabled(true);
    this.store.update(TelemetryStoreEnum.PREFERENCE, {
      data: false,
      status: 'Success',
      isLoading: false,
    });
  }

  /**
   * Fire-and-forget event ingest.
   * - NO-OP when disabled.
   * - Builds IngestTelemetryRequestDto and subscribes; HTTP errors are swallowed.
   * - The method itself is synchronous; no Promise is returned.
   */
  recordEvent(eventType: string, pluginId: string, version?: string): void {
    if (this.isDisabled()) {
      return;
    }

    const payload: IngestTelemetryRequestDto = {
      eventType,
      pluginId,
      version: version ?? null,
      anonClientId: this.anonId() ?? null,
      clientOs: null,
      clientArch: null,
    };

    this.apiClient
      .postTelemetryEvent(payload)
      .pipe(takeUntilDestroyed(this.destroyRef), catchError(() => EMPTY))
      .subscribe();
  }
}
