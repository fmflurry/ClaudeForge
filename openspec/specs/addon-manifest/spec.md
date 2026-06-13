# addon-manifest Specification

## Purpose
TBD - created by archiving change claude-plugin-cli. Update Purpose after archive.
## Requirements
### Requirement: Manifest declares add-on identity
The manifest MUST declare name, version (semver), and exactly one type from the closed set {hook, plugin, skill, agent}.

#### Scenario: Valid manifest with all identity fields is accepted
- **WHEN** a manifest declares name="my-hook", version="1.0.0", and type="hook"
- **THEN** the manifest passes identity validation

#### Scenario: Manifest with unknown type is rejected with error
- **WHEN** a manifest declares type="unknown-type"
- **THEN** validation fails with a clear error message naming the unknown type and listing valid types

#### Scenario: Manifest missing a required identity field is rejected
- **WHEN** a manifest omits the name field
- **THEN** validation fails with an error indicating the missing required field

#### Scenario: Manifest with invalid semver version is rejected
- **WHEN** a manifest declares version="1.0"
- **THEN** validation fails with an error indicating version does not follow semver format

### Requirement: Manifest declares managed files
The manifest MUST enumerate the files the add-on owns (relative paths) so install and remove operations are deterministic.

#### Scenario: Manifest lists files that become the install set
- **WHEN** a manifest declares files=["hook.ts", "config.json"]
- **THEN** the install operation writes only those two files to the target scope

#### Scenario: Manifest with no files is rejected
- **WHEN** a manifest declares files=[]
- **THEN** validation fails with an error indicating at least one file is required

#### Scenario: Manifest with relative paths is accepted, absolute paths are rejected
- **WHEN** a manifest declares files=["./hooks/auth.ts"]
- **THEN** validation passes
- **WHEN** a manifest declares files=["/absolute/path/auth.ts"]
- **THEN** validation fails with an error

### Requirement: Manifest declares target scope eligibility
The manifest MUST declare supported scopes as an explicit non-empty array of scope values drawn from {local, global}. The input shorthand string "both" MUST be accepted and expanded to ["local", "global"]. An empty array MUST be rejected.

#### Scenario: supportedScopes array is honored
- **WHEN** a manifest declares supportedScopes=["local", "global"] and an install targets local scope
- **THEN** the operation proceeds

#### Scenario: "both" shorthand expands to local and global
- **WHEN** a manifest declares supportedScopes="both"
- **THEN** validation normalizes it to ["local", "global"] and both scopes are treated as supported

#### Scenario: Empty supportedScopes is rejected
- **WHEN** a manifest declares supportedScopes=[]
- **THEN** validation fails with an error indicating at least one supported scope is required

#### Scenario: Install to unsupported scope is rejected
- **WHEN** a manifest declares supportedScopes=["global"] and an install targets local scope
- **THEN** the operation fails with an error indicating the scope is not supported by this add-on

### Requirement: Plugin add-ons are global-only
A manifest with type="plugin" MUST NOT declare local as a supported scope, because installed plugins live under ~/.claude/plugins/ which has no project-local equivalent.

#### Scenario: Plugin declaring local scope is rejected at validation
- **WHEN** a manifest declares type="plugin" and supportedScopes=["local"]
- **THEN** validation fails with an error indicating plugin add-ons are global-only

#### Scenario: Plugin declaring local within both is rejected
- **WHEN** a manifest declares type="plugin" and supportedScopes=["local", "global"]
- **THEN** validation fails with an error indicating plugin add-ons cannot target local scope

### Requirement: Manifest declares per-type file semantics
The managed files MUST conform to the add-on's type so placement is deterministic: an agent owns exactly one markdown file; a skill owns a directory tree; a hook owns one or more script files plus a declared settings.json hook entry; a plugin owns a bundle directory.

#### Scenario: Agent manifest must declare exactly one file
- **WHEN** a manifest declares type="agent" and files=["a.md", "b.md"]
- **THEN** validation fails with an error indicating an agent declares exactly one markdown file

#### Scenario: Hook manifest must declare a settings.json hook entry
- **WHEN** a manifest declares type="hook" but omits the hook object (event, matcher, command)
- **THEN** validation fails with an error indicating a hook entry is required for hook add-ons

#### Scenario: Hook command must reference a declared file
- **WHEN** a manifest declares type="hook", files=["hooks/auth.sh"], and hook.command="hooks/missing.sh"
- **THEN** validation fails with an error indicating hook.command must reference a managed file

#### Scenario: Non-hook manifest must not declare a hook entry
- **WHEN** a manifest declares type="skill" and also declares a hook object
- **THEN** validation fails with an error indicating the hook entry is only valid for hook add-ons

#### Scenario: Skill manifest accepts a directory tree
- **WHEN** a manifest declares type="skill" and files=["SKILL.md", "scripts/run.sh"]
- **THEN** validation passes and the files are treated as a skill directory tree

### Requirement: Manifest is type-explicit and self-describing
Given only a manifest, the CLI MUST determine the add-on type and how to place it without inspecting file contents.

#### Scenario: CLI categorizes add-on from manifest alone
- **WHEN** a manifest is loaded with type="skill"
- **THEN** the CLI knows to place the add-on under the skills directory without reading any file content

#### Scenario: Manifest structure is sufficient for deterministic placement
- **WHEN** a manifest declares type="agent" and files=["agent.ts"], the CLI MUST know where to place these files without additional inspection
- **THEN** the manifest alone determines placement logic

### Requirement: Manifest validation
The CLI MUST validate a manifest against the schema before any lifecycle operation and fail fast with a clear message.

#### Scenario: Malformed manifest blocks the operation
- **WHEN** a manifest has invalid JSON syntax
- **THEN** validation fails immediately with an error indicating the syntax issue, and no lifecycle operation proceeds

#### Scenario: Valid manifest passes all pre-flight checks
- **WHEN** a manifest is well-formed, complete, and conforms to the schema
- **THEN** validation succeeds and the operation proceeds

