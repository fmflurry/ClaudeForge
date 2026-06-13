## ADDED Requirements

### Requirement: Add (install/create) an add-on
The CLI MUST install an add-on into the resolved scope per its manifest, writing all managed files.

#### Scenario: Successful install reports type, name, and scope
- **WHEN** an add-on is installed to local scope
- **THEN** the operation succeeds and outputs a message indicating the type, name, and scope of the installed add-on

#### Scenario: Installing over an existing add-on without --force is rejected
- **WHEN** an add-on named "my-skill" is already installed in the target scope, and a second install is attempted without the --force flag
- **THEN** the operation fails with an error message indicating the add-on already exists and suggesting --force to overwrite

#### Scenario: --force flag overwrites an existing add-on
- **WHEN** an add-on named "my-skill" is already installed, and install is called with --force
- **THEN** the existing files are replaced and the operation succeeds

### Requirement: List (read) installed add-ons
The CLI MUST list installed add-ons for a given scope, showing type, name, version, and available stored versions.

#### Scenario: List local scope add-ons
- **WHEN** list is called targeting local scope
- **THEN** all add-ons installed in ./.claude/ are displayed with their type, name, and version

#### Scenario: List global scope add-ons
- **WHEN** list is called targeting global scope
- **THEN** all add-ons installed in ~/.claude/ are displayed with their type, name, and version

#### Scenario: Empty scope reports none
- **WHEN** list is called for a scope containing no add-ons
- **THEN** the operation reports that no add-ons are installed in that scope

#### Scenario: List shows live version and available stored versions
- **WHEN** list is called for a scope containing an updated add-on
- **THEN** each add-on is displayed with its type, name, live version, and the set of stored versions available for rollback

### Requirement: Update an add-on
The CLI MUST update an installed add-on to a newer version, replacing managed files atomically.

#### Scenario: Update to newer version succeeds
- **WHEN** an add-on at version 1.0.0 is updated to version 2.0.0
- **THEN** all managed files are replaced and the operation reports success with the old and new versions

#### Scenario: Update when already current is a no-op with a message
- **WHEN** an add-on at version 1.5.0 is updated and the target version is also 1.5.0
- **THEN** the operation reports that the add-on is already at the current version and skips writing files

#### Scenario: Failed update leaves prior version intact
- **WHEN** an update operation fails partway through (e.g., a required file cannot be written)
- **THEN** all managed files remain at the prior version and no partial state is left on disk

### Requirement: Remove (delete) an add-on
The CLI MUST remove only the files the manifest declares, leaving unrelated files untouched.

#### Scenario: Remove deletes only managed files
- **WHEN** an add-on declares files=["hooks/auth.ts", "config.json"] and remove is called
- **THEN** only those two files are deleted from the scope directory

#### Scenario: Removing a non-installed add-on errors clearly
- **WHEN** remove is called for an add-on that is not installed in the target scope
- **THEN** the operation fails with a clear error message indicating the add-on is not found

#### Scenario: Removal does not touch files not owned by the manifest
- **WHEN** a scope contains both a managed file and an unrelated file, and remove is called
- **THEN** only the managed file is deleted and the unrelated file remains

### Requirement: Hook registration in settings.json
Adding a hook add-on MUST idempotently merge its hook entry into the target scope's settings.json, preserving unrelated settings and writing atomically. Removing it MUST remove exactly that entry.

#### Scenario: Adding a hook merges its entry under the correct event and matcher
- **WHEN** a hook add-on declares event="PreToolUse", matcher="Bash", command="hooks/auth.sh" and is added
- **THEN** settings.json gains a hooks.PreToolUse matcher group for "Bash" containing { type: "command", command: "hooks/auth.sh" }

#### Scenario: Re-adding the same hook does not duplicate the entry
- **WHEN** the same hook add-on is added twice
- **THEN** settings.json contains exactly one matching hook entry

#### Scenario: Merge preserves unrelated settings
- **WHEN** settings.json already contains unrelated keys and unrelated hook events
- **THEN** after a hook add those unrelated keys and events remain unchanged

#### Scenario: Removing a hook unregisters its settings entry
- **WHEN** a previously added hook add-on is removed
- **THEN** its { type, command } entry is removed; an emptied matcher group and an emptied event key are also dropped, and unrelated entries remain

#### Scenario: Malformed settings.json aborts the operation
- **WHEN** the target settings.json contains invalid JSON and a hook add is attempted
- **THEN** the operation fails with a clear error and the existing settings.json is not overwritten

### Requirement: Version snapshot on update
Before replacing an installed add-on, the CLI MUST snapshot the current live version into a version store so it can be restored later.

#### Scenario: Update snapshots the prior version
- **WHEN** an add-on at version 1.0.0 is updated to 2.0.0
- **THEN** version 1.0.0 (its files and, for hooks, its settings entry) is retained in the version store and version 2.0.0 is installed live

#### Scenario: Force-overwrite snapshots the existing version
- **WHEN** an existing add-on is overwritten with --force
- **THEN** the existing version is snapshotted into the version store before the overwrite

### Requirement: Rollback to a stored version
The CLI MUST support rolling an add-on back to a previously stored version, restoring its files and (for hooks) its settings entry atomically.

#### Scenario: Rollback to the most recent prior version
- **WHEN** rollback is invoked for an add-on with stored versions and no --to flag
- **THEN** the most recent prior stored version is installed live and the operation reports the restored version

#### Scenario: Rollback to a specific version
- **WHEN** rollback is invoked with --to=1.0.0 and version 1.0.0 exists in the store
- **THEN** version 1.0.0 is installed live

#### Scenario: Rollback to a missing version errors
- **WHEN** rollback is invoked with --to=9.9.9 and no such stored version exists
- **THEN** the operation fails with a clear error and the live install is unchanged

#### Scenario: Rollback is reversible
- **WHEN** rollback replaces the current live version
- **THEN** the pre-rollback version is itself snapshotted before being replaced

### Requirement: Scaffold a new add-on when no source manifest exists
When add is invoked with a bare name and a --type instead of a source manifest directory, the CLI MUST scaffold starter files (reusing the shared template generator), synthesize an addon.json, then install it through the normal add path.

#### Scenario: Scaffold creates a valid add-on and installs it
- **WHEN** add is invoked with name="my-hook", --type=hook, --scope=local, and no existing addon.json source
- **THEN** the CLI generates starter files and an addon.json, validates it, and installs the add-on into local scope

#### Scenario: Scaffolded plugin defaults to global-only scope
- **WHEN** add scaffolds a new add-on with --type=plugin
- **THEN** the synthesized addon.json declares supportedScopes=["global"]

### Requirement: Idempotent and safe operations
Lifecycle commands MUST be safe to re-run and MUST never partially corrupt the target scope.

#### Scenario: Interrupted operation can be re-run cleanly
- **WHEN** an install operation is interrupted partway, and the same install is run again
- **THEN** the operation either detects the partial state and completes cleanly or reports an error without leaving the scope in a corrupted state
