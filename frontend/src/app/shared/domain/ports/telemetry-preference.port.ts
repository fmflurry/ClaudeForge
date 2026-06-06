/**
 * Domain port for persisting the telemetry opt-out preference.
 * Default: enabled (isDisabled() returns false).
 */
export abstract class TelemetryPreferencePort {
  static readonly STORAGE_KEY = 'plugin-marketplace:telemetry-disabled';

  abstract isDisabled(): boolean;
  abstract setDisabled(disabled: boolean): void;
  abstract clear(): void;
}
