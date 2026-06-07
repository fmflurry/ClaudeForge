/**
 * RED tests — Task 14.3: TeamContextStore + TeamContextFacade
 *
 * Expected production files (DO NOT exist yet — tests MUST FAIL):
 *   src/app/features/team-context/application/store/team-context.store.ts
 *   src/app/features/team-context/application/facades/team-context.facade.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // team-context.store.ts
 *   enum TeamContextStoreEnum {
 *     CURRENT_TEAM = 'CURRENT_TEAM',
 *   }
 *   interface TeamContextState {
 *     [TeamContextStoreEnum.CURRENT_TEAM]: ResourceState<string>;
 *   }
 *   @Injectable({ providedIn: 'root' })
 *   class TeamContextStore extends BaseStore<typeof TeamContextStoreEnum, TeamContextState>
 *
 *   // team-context.facade.ts
 *   @Injectable()
 *   class TeamContextFacade {
 *     // Signal getters:
 *     get currentTeam(): Signal<string | undefined>      // the raw team id string or undefined
 *     get teamId(): Signal<string | undefined>           // alias for currentTeam
 *     get hasTeam(): Signal<boolean>                     // true when currentTeam is set
 *     get presets(): Signal<readonly string[]>           // the PRESET_TEAMS constant
 *     get needsInit(): Signal<boolean>                   // true when no team is set AND init has run
 *     get validationError(): Signal<string | undefined>  // last validation error (cleared on success)
 *
 *     // Methods:
 *     init(): void
 *       - reads from TeamContextStoragePort on startup
 *       - if a stored id exists: sets currentTeam (does NOT re-validate stored id)
 *       - after call, needsInit reflects whether a team was found
 *     setTeam(id: string): void
 *       - calls validateTeamId(id)
 *       - on valid: persists to TeamContextStoragePort, updates store, clears validationError
 *       - on invalid: sets validationError, does NOT persist, does NOT update currentTeam
 *     clearTeam(): void
 *       - clears TeamContextStoragePort
 *       - resets currentTeam to undefined
 *       - needsInit becomes true
 *   }
 *
 * Port binding: tests provide InMemoryTeamContextAdapter via DI.
 * Reuses: TeamContextStoragePort, InMemoryTeamContextAdapter (both from Group 11).
 */

import { TestBed } from '@angular/core/testing';
import { TeamContextStore, TeamContextStoreEnum } from './team-context.store';
import type { TeamContextState } from './team-context.store';
import { TeamContextFacade } from '../facades/team-context.facade';
import { TeamContextStoragePort } from '../../../../shared/domain/ports/team-context-storage.port';
import { InMemoryTeamContextAdapter } from '../../../../shared/infrastructure/storage/in-memory-team-context.adapter';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import { PRESET_TEAMS } from '../../domain/rules/team-id-validation.rules';

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

/** Returns fresh TestBed instances with an isolated in-memory port each time. */
function setup(): { store: TeamContextStore; facade: TeamContextFacade; port: InMemoryTeamContextAdapter } {
  TestBed.resetTestingModule();
  const port = new InMemoryTeamContextAdapter();
  TestBed.configureTestingModule({
    providers: [TeamContextStore, TeamContextFacade, { provide: TeamContextStoragePort, useValue: port }],
  });
  return {
    store: TestBed.inject(TeamContextStore),
    facade: TestBed.inject(TeamContextFacade),
    port,
  };
}

/** Pre-seeds the in-memory port before TestBed is configured. */
function setupWithStoredTeam(storedId: string): {
  store: TeamContextStore;
  facade: TeamContextFacade;
  port: InMemoryTeamContextAdapter;
} {
  TestBed.resetTestingModule();
  const port = new InMemoryTeamContextAdapter();
  port.setTeamId(storedId);
  TestBed.configureTestingModule({
    providers: [TeamContextStore, TeamContextFacade, { provide: TeamContextStoragePort, useValue: port }],
  });
  return {
    store: TestBed.inject(TeamContextStore),
    facade: TestBed.inject(TeamContextFacade),
    port,
  };
}

// ---------------------------------------------------------------------------
// TeamContextStore — enum and state
// ---------------------------------------------------------------------------

describe('TeamContextStore — enum keys', () => {
  it('should have CURRENT_TEAM key', () => {
    expect(TeamContextStoreEnum.CURRENT_TEAM).toBe('CURRENT_TEAM');
  });
});

describe('TeamContextStore — initial state', () => {
  it('should initialise CURRENT_TEAM with empty non-loading state', () => {
    const { store } = setup();
    const state: ResourceState<string> = store.get(TeamContextStoreEnum.CURRENT_TEAM)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
  });

  it('should be an instance of TeamContextStore', () => {
    const { store } = setup();
    expect(store).toBeInstanceOf(TeamContextStore);
  });

  it('CURRENT_TEAM state type should accept ResourceState<string>', () => {
    const { store } = setup();
    const partial: Partial<TeamContextState[typeof TeamContextStoreEnum.CURRENT_TEAM]> = {
      data: 'Engineering',
      status: 'Success',
    };
    store.update(TeamContextStoreEnum.CURRENT_TEAM, partial);
    expect(store.get(TeamContextStoreEnum.CURRENT_TEAM)().data).toBe('Engineering');
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — initial signal values (before init())
// ---------------------------------------------------------------------------

describe('TeamContextFacade — initial signal values before init()', () => {
  it('currentTeam should return undefined before init()', () => {
    const { facade } = setup();
    expect(facade.currentTeam()).toBeUndefined();
  });

  it('teamId should return undefined before init()', () => {
    const { facade } = setup();
    expect(facade.teamId()).toBeUndefined();
  });

  it('hasTeam should return false before init()', () => {
    const { facade } = setup();
    expect(facade.hasTeam()).toBe(false);
  });

  it('presets should return the PRESET_TEAMS list', () => {
    const { facade } = setup();
    expect(facade.presets()).toEqual(PRESET_TEAMS);
  });

  it('needsInit should return true before init() (no team stored)', () => {
    const { facade } = setup();
    // Before init is called, the facade has not read from storage yet;
    // needsInit=true is the safe default (show onboarding)
    expect(facade.needsInit()).toBe(true);
  });

  it('validationError should return undefined initially', () => {
    const { facade } = setup();
    expect(facade.validationError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — init() with no stored team
// ---------------------------------------------------------------------------

describe('TeamContextFacade — init() with no stored team', () => {
  it('should not throw when called with empty storage', () => {
    const { facade } = setup();
    expect(() => facade.init()).not.toThrow();
  });

  it('currentTeam should remain undefined after init() when nothing is stored', () => {
    const { facade } = setup();
    facade.init();
    expect(facade.currentTeam()).toBeUndefined();
  });

  it('hasTeam should remain false after init() when nothing is stored', () => {
    const { facade } = setup();
    facade.init();
    expect(facade.hasTeam()).toBe(false);
  });

  it('needsInit should be true after init() when no team found', () => {
    const { facade } = setup();
    facade.init();
    expect(facade.needsInit()).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — init() with a stored team
// ---------------------------------------------------------------------------

describe('TeamContextFacade — init() with a stored team', () => {
  it('should load the stored team id into currentTeam', () => {
    const { facade } = setupWithStoredTeam('Engineering');
    facade.init();
    expect(facade.currentTeam()).toBe('Engineering');
  });

  it('teamId should equal the stored team after init()', () => {
    const { facade } = setupWithStoredTeam('QA');
    facade.init();
    expect(facade.teamId()).toBe('QA');
  });

  it('hasTeam should be true after init() when a team is stored', () => {
    const { facade } = setupWithStoredTeam('DevOps');
    facade.init();
    expect(facade.hasTeam()).toBe(true);
  });

  it('needsInit should be false after init() when a team is found', () => {
    const { facade } = setupWithStoredTeam('Product');
    facade.init();
    expect(facade.needsInit()).toBe(false);
  });

  it('calling init() multiple times should be idempotent (last call wins)', () => {
    const { facade } = setupWithStoredTeam('Design');
    facade.init();
    facade.init();
    expect(facade.currentTeam()).toBe('Design');
    expect(facade.hasTeam()).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — setTeam() valid inputs
// ---------------------------------------------------------------------------

describe('TeamContextFacade — setTeam() valid input', () => {
  it('should update currentTeam on valid id', () => {
    const { facade } = setup();
    facade.setTeam('Engineering');
    expect(facade.currentTeam()).toBe('Engineering');
  });

  it('should update teamId (alias) on valid id', () => {
    const { facade } = setup();
    facade.setTeam('Product');
    expect(facade.teamId()).toBe('Product');
  });

  it('hasTeam should become true after setTeam() with valid id', () => {
    const { facade } = setup();
    facade.setTeam('QA');
    expect(facade.hasTeam()).toBe(true);
  });

  it('needsInit should become false after setTeam() with valid id', () => {
    const { facade } = setup();
    facade.setTeam('DevOps');
    expect(facade.needsInit()).toBe(false);
  });

  it('validationError should be cleared after a successful setTeam()', () => {
    const { facade } = setup();
    // first cause a validation error
    facade.setTeam(''); // invalid
    expect(facade.validationError()).toBeTruthy();
    // now fix it
    facade.setTeam('Engineering');
    expect(facade.validationError()).toBeUndefined();
  });

  it('should persist the team id to the storage port', () => {
    const { facade, port } = setup();
    facade.setTeam('Engineering');
    expect(port.getTeamId()).toBe('Engineering');
  });

  it('should set the normalized (trimmed) id, not the raw input', () => {
    const { facade, port } = setup();
    facade.setTeam('  Engineering  ');
    expect(facade.currentTeam()).toBe('Engineering');
    expect(port.getTeamId()).toBe('Engineering');
  });

  it('should allow updating the team from one valid value to another', () => {
    const { facade } = setup();
    facade.setTeam('Engineering');
    facade.setTeam('Design');
    expect(facade.currentTeam()).toBe('Design');
  });

  it('should accept all preset team names', () => {
    for (const preset of PRESET_TEAMS) {
      const { facade } = setup();
      facade.setTeam(preset);
      expect(facade.currentTeam()).toBe(preset);
    }
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — setTeam() invalid inputs
// ---------------------------------------------------------------------------

describe('TeamContextFacade — setTeam() invalid input', () => {
  it('should set validationError when called with empty string', () => {
    const { facade } = setup();
    facade.setTeam('');
    expect(facade.validationError()).toBeTruthy();
  });

  it('should NOT update currentTeam when input is invalid', () => {
    const { facade } = setup();
    facade.setTeam(''); // invalid
    expect(facade.currentTeam()).toBeUndefined();
  });

  it('should NOT persist to storage when input is invalid', () => {
    const { facade, port } = setup();
    facade.setTeam(''); // invalid
    expect(port.getTeamId()).toBeNull();
  });

  it('should set validationError for a too-short team id', () => {
    const { facade } = setup();
    facade.setTeam('X'); // 1 char — below MIN
    expect(facade.validationError()).toBeTruthy();
  });

  it('should set validationError for a team id with special characters', () => {
    const { facade } = setup();
    facade.setTeam('team@invalid');
    expect(facade.validationError()).toBeTruthy();
  });

  it('should NOT update currentTeam when special char input is given', () => {
    const { facade } = setup();
    facade.setTeam('Engineering'); // first set a valid team
    facade.setTeam('team@invalid'); // now try invalid
    // currentTeam should be unchanged
    expect(facade.currentTeam()).toBe('Engineering');
  });

  it('should NOT persist invalid id even after a valid id was stored', () => {
    const { facade, port } = setup();
    facade.setTeam('Engineering');
    facade.setTeam('team@invalid');
    expect(port.getTeamId()).toBe('Engineering');
  });

  it('should set validationError for a too-long team id', () => {
    const { facade } = setup();
    facade.setTeam('A'.repeat(51));
    expect(facade.validationError()).toBeTruthy();
  });

  it('should not throw for any invalid input', () => {
    const { facade } = setup();
    const invalidInputs = ['', ' ', 'X', 'team@name', 'A'.repeat(100), "'; DROP TABLE --"];
    for (const input of invalidInputs) {
      expect(() => facade.setTeam(input)).not.toThrow();
    }
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — clearTeam()
// ---------------------------------------------------------------------------

describe('TeamContextFacade — clearTeam()', () => {
  it('should clear currentTeam', () => {
    const { facade } = setup();
    facade.setTeam('Engineering');
    facade.clearTeam();
    expect(facade.currentTeam()).toBeUndefined();
  });

  it('should clear teamId', () => {
    const { facade } = setup();
    facade.setTeam('QA');
    facade.clearTeam();
    expect(facade.teamId()).toBeUndefined();
  });

  it('hasTeam should become false after clearTeam()', () => {
    const { facade } = setup();
    facade.setTeam('DevOps');
    facade.clearTeam();
    expect(facade.hasTeam()).toBe(false);
  });

  it('needsInit should become true after clearTeam()', () => {
    const { facade } = setup();
    facade.setTeam('Design');
    facade.clearTeam();
    expect(facade.needsInit()).toBe(true);
  });

  it('should clear the storage port', () => {
    const { facade, port } = setup();
    facade.setTeam('Product');
    facade.clearTeam();
    expect(port.getTeamId()).toBeNull();
  });

  it('should not throw when clearTeam() is called before setTeam()', () => {
    const { facade } = setup();
    expect(() => facade.clearTeam()).not.toThrow();
  });

  it('should not throw when clearTeam() is called multiple times', () => {
    const { facade } = setup();
    facade.setTeam('Engineering');
    expect(() => {
      facade.clearTeam();
      facade.clearTeam();
    }).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — persistence round-trip via in-memory port
// ---------------------------------------------------------------------------

describe('TeamContextFacade — persistence round-trip', () => {
  it('stored team id survives a second init() call (simulates page reload)', () => {
    // Session 1: set a team, confirm storage is populated
    const { facade: facade1, port } = setup();
    facade1.setTeam('Engineering');
    expect(port.getTeamId()).toBe('Engineering');

    // Session 2: simulate a page reload by constructing a new facade
    // backed by the same port instance (which already has the value stored).
    // We use setupWithStoredTeam which pre-seeds the same port content.
    const { facade: facade2 } = setupWithStoredTeam('Engineering');
    facade2.init();
    expect(facade2.currentTeam()).toBe('Engineering');
    expect(facade2.hasTeam()).toBe(true);
  });

  it('clear then re-set persists the new value correctly', () => {
    const { facade, port } = setup();
    facade.setTeam('Engineering');
    facade.clearTeam();
    facade.setTeam('Product');
    expect(port.getTeamId()).toBe('Product');
    expect(facade.currentTeam()).toBe('Product');
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — ContextRegistry integration (team-changed event)
// ---------------------------------------------------------------------------

describe('TeamContextFacade — context registry event', () => {
  it('should not throw when setTeam() publishes a context event', () => {
    const { facade } = setup();
    expect(() => facade.setTeam('Engineering')).not.toThrow();
  });

  it('should not throw when clearTeam() publishes a context event', () => {
    const { facade } = setup();
    facade.setTeam('Engineering');
    expect(() => facade.clearTeam()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// TeamContextFacade — public API surface
// ---------------------------------------------------------------------------

describe('TeamContextFacade — public API surface', () => {
  it('should expose currentTeam as a function (signal)', () => {
    const { facade } = setup();
    expect(typeof facade.currentTeam).toBe('function');
  });

  it('should expose teamId as a function (signal)', () => {
    const { facade } = setup();
    expect(typeof facade.teamId).toBe('function');
  });

  it('should expose hasTeam as a function (signal)', () => {
    const { facade } = setup();
    expect(typeof facade.hasTeam).toBe('function');
  });

  it('should expose presets as a function (signal)', () => {
    const { facade } = setup();
    expect(typeof facade.presets).toBe('function');
  });

  it('should expose needsInit as a function (signal)', () => {
    const { facade } = setup();
    expect(typeof facade.needsInit).toBe('function');
  });

  it('should expose validationError as a function (signal)', () => {
    const { facade } = setup();
    expect(typeof facade.validationError).toBe('function');
  });

  it('should expose init as a function', () => {
    const { facade } = setup();
    expect(typeof facade.init).toBe('function');
  });

  it('should expose setTeam as a function', () => {
    const { facade } = setup();
    expect(typeof facade.setTeam).toBe('function');
  });

  it('should expose clearTeam as a function', () => {
    const { facade } = setup();
    expect(typeof facade.clearTeam).toBe('function');
  });
});
