/**
 * Domain model for a single installed-plugin record.
 */
export interface InstalledPluginRecord {
  name: string;
  version: string;
  installedAt: string; // ISO 8601 date string
}

/**
 * Domain port for persisting the list of installed plugins.
 * list() always returns a new array (immutable).
 */
export abstract class InstalledPluginsStoragePort {
  static readonly STORAGE_KEY = 'plugin-marketplace:installed';

  abstract list(): InstalledPluginRecord[];
  abstract add(record: InstalledPluginRecord): void;
  abstract remove(name: string): void;
  abstract clear(): void;
}
