import { TeamContextStoragePort } from '../../domain/ports/team-context-storage.port';

/**
 * In-memory fake adapter for TeamContextStoragePort.
 * Does NOT touch window.localStorage — safe for unit tests.
 * Each instance has its own isolated state.
 */
export class InMemoryTeamContextAdapter extends TeamContextStoragePort {
  private teamId: string | null = null;

  getTeamId(): string | null {
    return this.teamId;
  }

  setTeamId(id: string): void {
    this.teamId = id;
  }

  clear(): void {
    this.teamId = null;
  }
}
