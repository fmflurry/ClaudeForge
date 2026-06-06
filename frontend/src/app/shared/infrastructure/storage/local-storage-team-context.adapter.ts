import { TeamContextStoragePort } from '../../domain/ports/team-context-storage.port';

/**
 * localStorage-backed adapter for TeamContextStoragePort.
 * JSON round-trip is not needed here (raw string). try/catch guards corruption.
 */
export class LocalStorageTeamContextAdapter extends TeamContextStoragePort {
  getTeamId(): string | null {
    try {
      return window.localStorage.getItem(TeamContextStoragePort.STORAGE_KEY);
    } catch {
      return null;
    }
  }

  setTeamId(id: string): void {
    try {
      window.localStorage.setItem(TeamContextStoragePort.STORAGE_KEY, id);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }

  clear(): void {
    try {
      window.localStorage.removeItem(TeamContextStoragePort.STORAGE_KEY);
    } catch {
      // Storage unavailable — silently ignore.
    }
  }
}
