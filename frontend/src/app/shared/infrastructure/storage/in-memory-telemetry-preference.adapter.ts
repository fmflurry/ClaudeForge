import { TelemetryPreferencePort } from '../../domain/ports/telemetry-preference.port';

/**
 * In-memory fake adapter for TelemetryPreferencePort.
 * Does NOT touch window.localStorage — safe for unit tests.
 * Each instance has its own isolated state.
 */
export class InMemoryTelemetryPreferenceAdapter extends TelemetryPreferencePort {
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
