import { InstalledAddOnsStoragePort, InstalledPluginRecord } from '../../domain/ports/installed-plugins-storage.port';

/**
 * In-memory fake adapter for InstalledAddOnsStoragePort.
 * Does NOT touch window.localStorage — safe for unit tests.
 * Each instance has its own isolated state.
 */
export class InMemoryInstalledAddOnsAdapter extends InstalledAddOnsStoragePort {
  private records: InstalledPluginRecord[] = [];

  list(): InstalledPluginRecord[] {
    return [...this.records];
  }

  add(record: InstalledPluginRecord): void {
    this.records = [...this.records, { ...record }];
  }

  remove(name: string): void {
    this.records = this.records.filter((r) => r.name !== name);
  }

  clear(): void {
    this.records = [];
  }
}
