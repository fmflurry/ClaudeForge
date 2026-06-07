import { TelemetryPreferencePort } from '../../domain/ports/telemetry-preference.port';

/**
 * localStorage-backed adapter for TelemetryPreferencePort.
 * Stores the string 'true' when telemetry is disabled.
 * Returns false (enabled) on corruption, missing key, or storage error.
 * SSR-safe: localStorage is not available on the server — try/catch returns false/no-op.
 */
export class LocalStorageTelemetryPreferenceAdapter extends TelemetryPreferencePort {
  isDisabled(): boolean {
    try {
      const raw = localStorage.getItem(TelemetryPreferencePort.STORAGE_KEY);
      if (raw === null) return false;
      return raw === 'true';
    } catch {
      return false;
    }
  }

  setDisabled(disabled: boolean): void {
    try {
      localStorage.setItem(TelemetryPreferencePort.STORAGE_KEY, String(disabled));
    } catch {
      // Storage unavailable — silently ignore.
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(TelemetryPreferencePort.STORAGE_KEY);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }
}
