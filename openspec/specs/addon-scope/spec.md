# addon-scope Specification

## Purpose
TBD - created by archiving change claude-plugin-cli. Update Purpose after archive.
## Requirements
### Requirement: Local scope resolution
Local scope MUST resolve to `./.claude/` relative to the current working directory.

#### Scenario: Local install targets CWD .claude directory
- **WHEN** install is called with --scope=local
- **THEN** all managed files are written to ./.claude/ in the current working directory

### Requirement: Global scope resolution
Global scope MUST resolve to `~/.claude/` (the user home).

#### Scenario: Global install targets home .claude directory
- **WHEN** install is called with --scope=global
- **THEN** all managed files are written to ~/.claude/ in the user's home directory

### Requirement: Explicit scope selection
The CLI MUST require an explicit scope (e.g. a flag) OR apply a documented, predictable default, and MUST never silently act on the wrong scope.

#### Scenario: Scope flag is honored
- **WHEN** install is called with --scope=local
- **THEN** the operation targets local scope and not global

#### Scenario: Ambiguous invocation is rejected or uses documented default
- **WHEN** install is called without a --scope flag
- **THEN** the operation either fails with an error requiring explicit scope, OR applies a documented default scope consistently

### Requirement: Scope isolation
Operations in one scope MUST NOT modify the other scope.

#### Scenario: Local remove does not affect global install
- **WHEN** an add-on "my-skill" is installed in global scope, and remove is called with --scope=local for the same add-on
- **THEN** the global installation remains unchanged

#### Scenario: Global operations do not touch local directory
- **WHEN** operations target global scope
- **THEN** the ./.claude/ directory in the current working directory is never modified

### Requirement: Add-on type placement within scope
Within a resolved scope, each add-on type MUST be placed at its authoritative Claude Code location: an agent as a single file agents/<name>.md; a skill as a directory skills/<name>/; a hook as script(s) under hooks/ plus a registered settings.json entry; a plugin as a bundle directory under ~/.claude/plugins/<name>/ (global only).

#### Scenario: Agent installs as a single markdown file
- **WHEN** an add-on with type="agent" and name="reviewer" is installed to local scope
- **THEN** its file is written to ./.claude/agents/reviewer.md and no agents/reviewer/ directory is created

#### Scenario: Skill installs as a directory tree
- **WHEN** an add-on with type="skill" and name="lint" is installed to local scope
- **THEN** its files are written under ./.claude/skills/lint/ preserving their relative structure

#### Scenario: Hook installs scripts and registers a settings entry
- **WHEN** an add-on with type="hook" is installed to local scope
- **THEN** its script files are written under ./.claude/hooks/ AND a corresponding entry is merged into ./.claude/settings.json

#### Scenario: Plugin installs under the global plugins directory only
- **WHEN** an add-on with type="plugin" and name="pack" is installed
- **THEN** its bundle is written under ~/.claude/plugins/pack/

### Requirement: Plugin scope enforcement
The CLI MUST reject any plugin add-on operation targeting local scope before performing I/O.

#### Scenario: --scope local with --type plugin is rejected
- **WHEN** an add command is invoked with --type=plugin and --scope=local
- **THEN** the operation fails immediately with an error indicating plugins are global-only, and nothing is written

### Requirement: Hook settings registration scope
A hook's settings entry MUST be registered in the same scope's settings.json: local hooks in ./.claude/settings.json, global hooks in ~/.claude/settings.json. Registration MUST NOT modify the other scope's settings.

#### Scenario: Local hook registration stays in local settings
- **WHEN** a hook add-on is added with --scope=local
- **THEN** the entry is merged into ./.claude/settings.json and ~/.claude/settings.json is not modified

