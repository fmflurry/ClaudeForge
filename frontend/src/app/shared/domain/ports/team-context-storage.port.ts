/**
 * Domain port for persisting the active team ID.
 * Zero infrastructure dependencies — implementations live in the
 * infrastructure layer.
 */
export abstract class TeamContextStoragePort {
  static readonly STORAGE_KEY = 'plugin-marketplace:team';

  abstract getTeamId(): string | null;
  abstract setTeamId(id: string): void;
  abstract clear(): void;
}
