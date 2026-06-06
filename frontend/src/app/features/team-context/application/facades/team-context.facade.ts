import { Injectable, Signal, computed, signal } from '@angular/core';
import { inject } from '@angular/core';
import { TeamContextStore, TeamContextStoreEnum } from '../store/team-context.store';
import { TeamContextStoragePort } from '../../../../shared/domain/ports/team-context-storage.port';
import { contextRegistry } from '../../../../core/context/context-registry';
import {
  validateTeamId,
  PRESET_TEAMS,
} from '../../domain/rules/team-id-validation.rules';

@Injectable()
export class TeamContextFacade {
  private readonly store = inject(TeamContextStore);
  private readonly storagePort = inject(TeamContextStoragePort);

  private readonly _needsInit = signal<boolean>(true);
  private readonly _validationError = signal<string | undefined>(undefined);

  /** The raw team ID string from the store, or undefined if not set. */
  get currentTeam(): Signal<string | undefined> {
    return computed(() => this.store.get(TeamContextStoreEnum.CURRENT_TEAM)().data);
  }

  /** Alias for currentTeam. */
  get teamId(): Signal<string | undefined> {
    return this.currentTeam;
  }

  /** True when a team ID is currently set. */
  get hasTeam(): Signal<boolean> {
    return computed(() => this.store.get(TeamContextStoreEnum.CURRENT_TEAM)().data !== undefined);
  }

  /** The ordered list of preset team names. */
  get presets(): Signal<readonly string[]> {
    return signal<readonly string[]>(PRESET_TEAMS);
  }

  /**
   * True when the facade has been initialised (init() was called) but no
   * team was found — the UI should prompt the user to select or enter one.
   * Also true before init() is called (safe default: show onboarding).
   */
  get needsInit(): Signal<boolean> {
    return this._needsInit.asReadonly();
  }

  /** The last validation error from setTeam(), or undefined when clear. */
  get validationError(): Signal<string | undefined> {
    return this._validationError.asReadonly();
  }

  /**
   * Reads the persisted team ID from storage on startup.
   * If a stored ID exists it is loaded into the store without re-validation.
   * After calling, needsInit reflects whether a team was found.
   */
  init(): void {
    const stored = this.storagePort.getTeamId();
    if (stored !== null && stored.length > 0) {
      this.store.update(TeamContextStoreEnum.CURRENT_TEAM, {
        data: stored,
        status: 'Success',
      });
      this._needsInit.set(false);
    } else {
      this._needsInit.set(true);
    }
  }

  /**
   * Validates `id` and, when valid, persists it and updates the store.
   * On invalid input only the validationError signal is updated.
   */
  setTeam(id: string): void {
    const result = validateTeamId(id);
    if (!result.valid) {
      this._validationError.set(result.error);
      return;
    }

    const normalized = result.normalized!;
    this.storagePort.setTeamId(normalized);
    this.store.update(TeamContextStoreEnum.CURRENT_TEAM, {
      data: normalized,
      status: 'Success',
    });
    this._validationError.set(undefined);
    this._needsInit.set(false);
    contextRegistry.publish('team:changed', { teamId: normalized });
  }

  /**
   * Clears the current team from both storage and the store.
   * Sets needsInit back to true so the onboarding UI re-appears.
   */
  clearTeam(): void {
    this.storagePort.clear();
    this.store.clear(TeamContextStoreEnum.CURRENT_TEAM);
    this._needsInit.set(true);
    contextRegistry.publish('team:changed', { teamId: null });
  }
}
