import { InstalledPluginRecord, InstalledPluginsStoragePort } from '../../domain/ports/installed-plugins-storage.port';

/**
 * localStorage-backed adapter for InstalledPluginsStoragePort.
 * JSON round-trip; returns [] on corruption, missing key, or storage error.
 * list() always returns a NEW array (immutable).
 */
export class LocalStorageInstalledPluginsAdapter extends InstalledPluginsStoragePort {
  list(): InstalledPluginRecord[] {
    try {
      const raw = window.localStorage.getItem(InstalledPluginsStoragePort.STORAGE_KEY);
      if (raw === null) return [];
      const parsed: unknown = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      return [...(parsed as InstalledPluginRecord[])];
    } catch {
      return [];
    }
  }

  add(record: InstalledPluginRecord): void {
    const current = this.list();
    const updated: InstalledPluginRecord[] = [...current, { ...record }];
    this.persist(updated);
  }

  remove(name: string): void {
    const current = this.list();
    const updated = current.filter((r) => r.name !== name);
    this.persist(updated);
  }

  clear(): void {
    try {
      window.localStorage.removeItem(InstalledPluginsStoragePort.STORAGE_KEY);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }

  private persist(records: InstalledPluginRecord[]): void {
    try {
      window.localStorage.setItem(InstalledPluginsStoragePort.STORAGE_KEY, JSON.stringify(records));
    } catch {
      // Storage unavailable — silently ignore.
    }
  }
}
