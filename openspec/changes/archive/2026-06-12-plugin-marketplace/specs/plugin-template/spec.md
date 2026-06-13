# Plugin Template Specification

## ADDED Requirements

### Requirement: Scaffold Plugin Project Structure

The template generator SHALL create a complete plugin project directory with all required files and subdirectories, following marketplace conventions.

#### Scenario: Generate plugin scaffold from CLI
**WHEN** a plugin author runs `claude plugin scaffold --name my-auth-plugin --language typescript`
**THEN** the scaffold command SHALL:
- Create a project directory named `my-auth-plugin`
- Generate required files: `plugin.json`, `README.md`, `package.json`, `src/index.ts`, `.gitignore`
- Create subdirectories: `src/`, `docs/`, `tests/`, `assets/`
- Pre-populate `plugin.json` with placeholder metadata fields (name, version, type, description, author)
- Generate a basic `README.md` with template sections: Overview, Installation, Configuration, Usage Examples
- Report success with file count and next steps: "Created plugin scaffold in ./my-auth-plugin. Run `cd my-auth-plugin && npm install`"

#### Scenario: Generate in existing directory
**WHEN** a developer runs `claude plugin scaffold --language python` in an existing empty directory
**THEN** the scaffold command SHALL:
- Infer the plugin name from the directory name
- Generate all required files in the current directory
- Adjust the template to match Python conventions (pyproject.toml, src/plugin.py, etc.)

#### Scenario: Scaffold with language-specific template
**WHEN** a plugin author specifies `--language go` or `--language rust`
**THEN** the scaffold command SHALL:
- Load the language-specific template (Go: go.mod, Rust: Cargo.toml)
- Generate appropriate build/entry scripts
- Include language-specific example code and dependencies
- Pre-configure the entrypoint field in plugin.json for that language

### Requirement: Validate Required Metadata Fields

The template and validation system SHALL ensure all mandatory metadata fields are present and correctly formatted before submission to the marketplace.

#### Scenario: Validate complete plugin.json
**WHEN** a developer runs `claude plugin validate` in a plugin directory with all required fields populated
**THEN** the validator SHALL:
- Confirm presence of: name, version, type, description, author, entrypoints, language, dependencies
- Verify semantic versioning format (major.minor.patch)
- Verify entrypoints are valid (non-empty array with at least one item)
- Verify type is one of allowed values (e.g., skill, hook, integration, utility)
- Report "Plugin is valid and ready to publish"
- Exit with code 0

#### Scenario: Validate with missing required field
**WHEN** a developer runs `claude plugin validate` but the plugin.json is missing the "type" field
**THEN** the validator SHALL:
- Report "Missing required field: type"
- List allowed values: skill, hook, integration, utility
- Suggest an example: `"type": "skill"`
- Exit with a non-zero code

#### Scenario: Validate with invalid semantic version
**WHEN** plugin.json contains `"version": "1.2"` (missing patch version)
**THEN** the validator SHALL:
- Report "Invalid version format: 1.2. Must follow semantic versioning (major.minor.patch)"
- Suggest correction: `"version": "1.2.0"`
- Exit with a non-zero code

### Requirement: Capture Plugin Metadata in Manifest

The template and manifest system SHALL collect and store all metadata required by the marketplace and AI ecosystem in a standard plugin.json format.

#### Scenario: Complete plugin.json manifest
**WHEN** a plugin author completes the manifest with all metadata fields
**THEN** the plugin.json file SHALL include:
- Basic: name (string), version (semver), description (string), author (string)
- Type & categorization: type (enum: skill/hook/integration/utility), language (string), use-case-tags (array: e.g., ["auth", "security", "api"])
- Entrypoints: entrypoints (array with name, description, function signature)
- Dependencies: dependencies (object with package-name: version-spec pairs)
- Licensing: license (string, e.g., "MIT")
- Documentation: docs-url (string, optional)
- All fields validated before saving

#### Scenario: Manifest with use-case targeting
**WHEN** a plugin author wants to target specific use cases (e.g., "dev teams", "PMs", "DevOps")
**THEN** the plugin.json SHALL accept a `"use-case-tags"` array containing any combination of:
- "dev-team", "product-owner", "product-manager", "devops", "security", "data-analyst"
- The marketplace dashboard SHALL filter/search by these tags
- The AI discovery service SHALL use these tags for recommendations

### Requirement: Guided Creation Flow

The template system SHALL provide an interactive command to guide plugin authors through populating all metadata fields with validation at each step.

#### Scenario: Interactive scaffold with prompts
**WHEN** a plugin author runs `claude plugin scaffold --interactive`
**THEN** the command SHALL:
- Present prompts in sequence: name, description, type, language, author, use-case-tags
- Validate each input immediately (e.g., enforce semver for version)
- Allow backtracking with arrow keys or "previous" command
- Pre-fill defaults from package.json or git config where possible (e.g., author from git user)
- Generate plugin.json automatically upon completion
- Report: "Created plugin manifest. Run `claude plugin validate` to check for errors"

#### Scenario: Skip optional fields in guided flow
**WHEN** an author is prompted for optional fields (e.g., docs-url, license)
**THEN** the interactive flow SHALL:
- Mark optional fields clearly ("Optional: docs-url")
- Allow pressing Enter to skip
- Provide sensible defaults (license defaults to "MIT" if skipped)
- Save the manifest with only required + provided optional fields

#### Scenario: Resume interrupted creation
**WHEN** a developer runs `claude plugin scaffold --interactive` in a directory with an existing partial plugin.json
**THEN** the command SHALL:
- Load existing values and pre-fill the prompts
- Show which fields are complete vs incomplete
- Allow editing existing fields or skipping to the next incomplete field
- Update the manifest file upon completion

### Requirement: Template Documentation

The scaffolded plugin SHALL include embedded documentation (README, JSDoc/docstrings) guiding authors on implementing and configuring the plugin correctly.

#### Scenario: Generated README with sections
**WHEN** a plugin is scaffolded, the README.md file SHALL include:
- Overview section (auto-populated with plugin description)
- Installation section (how users install the plugin)
- Configuration section (environment variables, config files)
- Usage examples (sample code showing how to call the plugin)
- API reference (documented entrypoints)
- Contributing guidelines (encouraging external contributions)
- License information

#### Scenario: Entrypoint documentation in code
**WHEN** a TypeScript plugin is scaffolded, the src/index.ts file SHALL include:
- JSDoc comments for each exported function/entrypoint
- Type definitions for parameters and return values
- Example usage in comments
- Error handling documentation

### Requirement: Dependency Declaration and Validation

The template system SHALL allow authors to declare plugin dependencies and validate compatibility before publication.

#### Scenario: Declare dependencies
**WHEN** a plugin author populates the `dependencies` field in plugin.json
**THEN** the manifest SHALL accept:
- Package name with version specifier (e.g., "lodash": "^4.17.0", "axios": ">=1.0")
- The validator SHALL parse version specifiers and warn of conflicts
- The marketplace SHALL display declared dependencies on the plugin detail page

#### Scenario: Validate dependency compatibility
**WHEN** a developer runs `claude plugin validate` with declared dependencies
**THEN** the validator SHALL:
- Check if declared dependencies are available and public
- Warn if any dependency is deprecated or unmaintained
- Report conflicting version constraints (e.g., two dependencies requiring incompatible versions of the same transitive dependency)
- Suggest resolution: "Consider using lodash-es instead of lodash for tree-shaking"

#### Scenario: Dependencies auto-installed on plugin install
**WHEN** the CLI installs a plugin with declared dependencies
**THEN** the CLI SHALL:
- Fetch the plugin's dependency list from plugin.json
- Install each dependency in the plugin's node_modules (or equivalent)
- Report "Installed @namespace/plugin with 3 dependencies"
- Warn if any dependency installation fails, and offer to skip optional dependencies
