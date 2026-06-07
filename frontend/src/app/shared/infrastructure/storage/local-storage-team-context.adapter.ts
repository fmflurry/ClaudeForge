import { TeamContextStoragePort } from '../../domain/ports/team-context-storage.port';

/**
 * localStorage-backed adapter for TeamContextStoragePort.
 * JSON round-trip is not needed here (raw string). try/catch guards corruption.
 * SSR-safe: localStorage is not available on the server — try/catch returns null/no-op.
 */
export class LocalStorageTeamContextAdapter extends TeamContextStoragePort {
  getTeamId(): string | null {
    try {
      return localStorage.getItem(TeamContextStoragePort.STORAGE_KEY);
    } catch {
      return null;
    }
  }

  setTeamId(id: string): void {
    try {
      localStorage.setItem(TeamContextStoragePort.STORAGE_KEY, id);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(TeamContextStoragePort.STORAGE_KEY);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }
}
