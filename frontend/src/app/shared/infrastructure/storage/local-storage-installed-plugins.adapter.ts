import { InstalledAddOnsStoragePort, InstalledPluginRecord } from '../../domain/ports/installed-plugins-storage.port';

/**
 * localStorage-backed adapter for InstalledAddOnsStoragePort.
 * JSON round-trip; returns [] on corruption, missing key, or storage error.
 * list() always returns a NEW array (immutable).
 * SSR-safe: localStorage is not available on the server — try/catch returns []/no-op.
 */
export class LocalStorageInstalledAddOnsAdapter extends InstalledAddOnsStoragePort {
  list(): InstalledPluginRecord[] {
    try {
      const raw = localStorage.getItem(InstalledAddOnsStoragePort.STORAGE_KEY);
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
      localStorage.removeItem(InstalledAddOnsStoragePort.STORAGE_KEY);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }

  private persist(records: InstalledPluginRecord[]): void {
    try {
      localStorage.setItem(InstalledAddOnsStoragePort.STORAGE_KEY, JSON.stringify(records));
    } catch {
      // Storage unavailable — silently ignore.
    }
  }
}
