import { TelemetryPreferencePort } from '../../domain/ports/telemetry-preference.port';

/**
 * localStorage-backed adapter for TelemetryPreferencePort.
 * Stores the string 'true' when telemetry is disabled.
 * Returns false (enabled) on corruption, missing key, or storage error.
 */
export class LocalStorageTelemetryPreferenceAdapter extends TelemetryPreferencePort {
  isDisabled(): boolean {
    try {
      const raw = window.localStorage.getItem(TelemetryPreferencePort.STORAGE_KEY);
      if (raw === null) return false;
      return raw === 'true';
    } catch {
      return false;
    }
  }

  setDisabled(disabled: boolean): void {
    try {
      window.localStorage.setItem(TelemetryPreferencePort.STORAGE_KEY, String(disabled));
    } catch {
      // Storage unavailable — silently ignore.
    }
  }

  clear(): void {
    try {
      window.localStorage.removeItem(TelemetryPreferencePort.STORAGE_KEY);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }
}
