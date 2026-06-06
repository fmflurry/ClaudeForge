/**
 * Rust plugin scaffold template.
 *
 * Returns a GeneratedFileMap containing all files for a Rust plugin project.
 */

import type { GeneratedFileMap } from '../generator.js';

interface RustTemplateOptions {
  name: string;
  version: string;
  description: string;
  author: string;
  license: string;
  types: string[];
  languages: string[];
  useCaseTags?: string[] | undefined;
  entrypoints: Array<{ name: string; description: string; signature: string }>;
  dependencies: Record<string, string>;
}

/**
 * Generates the canonical plugin.json manifest content for a Rust plugin.
 */
function buildManifest(opts: RustTemplateOptions): string {
  const manifest: Record<string, unknown> = {
    name: opts.name,
    version: opts.version,
    description: opts.description,
    author: opts.author,
    types: opts.types,
    languages: opts.languages,
    entrypoints: opts.entrypoints,
    dependencies: opts.dependencies,
    license: opts.license,
  };

  if (opts.useCaseTags !== undefined) {
    manifest['useCaseTags'] = opts.useCaseTags;
  }

  return JSON.stringify(manifest, null, 2);
}

/**
 * Generates a Rust plugin scaffold file map.
 *
 * @param opts - Template options populated from GeneratorOptions
 * @returns A map of relative file paths to file contents
 */
export function generateRustTemplate(opts: RustTemplateOptions): GeneratedFileMap {
  const pluginName = opts.name;
  const pluginVersion = opts.version;

  const pluginJson = buildManifest(opts);

  // Sanitize crate name: replace hyphens with underscores for the crate identifier,
  // but keep hyphens in Cargo.toml name (Rust allows hyphens in package names).
  const cratePackageName = pluginName.replace(/@[^/]+\//, '');

  const cargoToml = `[package]
name = "${cratePackageName}"
version = "${pluginVersion}"
edition = "2021"
description = "${opts.description}"
authors = ["${opts.author}"]
license = "${opts.license}"

[lib]
name = "${cratePackageName.replace(/-/g, '_')}"
crate-type = ["cdylib", "rlib"]

[dependencies]
`;

  const srcLibRs = `//! ${pluginName} — ClaudeForge plugin library
//!
//! This crate exposes the primary plugin functions.
//!
//! # Example
//!
//! \`\`\`rust
//! use ${cratePackageName.replace(/-/g, '_')}::execute;
//!
//! let result = execute("hello world").expect("execution failed");
//! assert_eq!(result, "hello world");
//! \`\`\`

/// Executes the plugin's primary action.
///
/// # Arguments
///
/// * \`input\` — The input payload for this plugin invocation.
///
/// # Returns
///
/// Returns \`Ok(String)\` containing the processed output, or
/// \`Err(String)\` with a descriptive error message.
///
/// # Errors
///
/// Returns an error when \`input\` is empty.
pub fn execute(input: &str) -> Result<String, String> {
    if input.trim().is_empty() {
        return Err("input must not be empty".to_string());
    }

    Ok(input.trim().to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_execute_valid_input() {
        let result = execute("hello").unwrap();
        assert_eq!(result, "hello");
    }

    #[test]
    fn test_execute_empty_input_returns_error() {
        assert!(execute("").is_err());
    }
}
`;

  const readmeMd = `# ${pluginName}

${opts.description}

## Overview

**${pluginName}** is a ClaudeForge plugin authored by ${opts.author}.
Version: ${pluginVersion} | License: ${opts.license}

## Installation

Add it to your \`Cargo.toml\`:

\`\`\`toml
[dependencies]
${cratePackageName} = "${pluginVersion}"
\`\`\`

Or with cargo add:

\`\`\`bash
cargo add ${cratePackageName}
\`\`\`

Build:

\`\`\`bash
cargo build
\`\`\`

Run tests:

\`\`\`bash
cargo test
\`\`\`

Or install via ClaudeForge:

\`\`\`bash
claude plugin install ${pluginName}
\`\`\`

## Configuration

Configure the plugin by editing your \`claude.config.json\`:

\`\`\`json
{
  "plugins": {
    "${pluginName}": {
      "enabled": true
    }
  }
}
\`\`\`

## Usage

\`\`\`rust
use ${cratePackageName.replace(/-/g, '_')}::execute;

fn main() {
    let result = execute("your input here").expect("execution failed");
    println!("{}", result);
}
\`\`\`

## API

### \`execute(input: &str) -> Result<String, String>\`

Runs the plugin's primary action.

**Parameters:**
- \`input\` — The input payload.

**Returns:** \`Ok(String)\` with the processed output or \`Err(String)\` with the error message.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

1. Fork the repository
2. Create a feature branch: \`git checkout -b feat/my-feature\`
3. Commit your changes
4. Push and open a pull request.

## License

This project is licensed under the ${opts.license} License.
`;

  return {
    'plugin.json': pluginJson,
    'Cargo.toml': cargoToml,
    'src/lib.rs': srcLibRs,
    'README.md': readmeMd,
    'tests/integration_test.rs': `// Integration tests for ${pluginName}\nuse ${cratePackageName.replace(/-/g, '_')}::execute;\n\n#[test]\nfn test_execute_smoke() {\n    let result = execute("hello").unwrap();\n    assert_eq!(result, "hello");\n}\n`,
    'docs/.gitkeep': '',
    'assets/.gitkeep': '',
  };
}
