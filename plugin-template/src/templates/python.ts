/**
 * Python plugin scaffold template.
 *
 * Returns a GeneratedFileMap containing all files for a Python plugin project.
 */

import type { GeneratedFileMap } from '../generator.js';

interface PythonTemplateOptions {
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
 * Generates the canonical plugin.json manifest content for a Python plugin.
 */
function buildManifest(opts: PythonTemplateOptions): string {
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
 * Generates a Python plugin scaffold file map.
 *
 * @param opts - Template options populated from GeneratorOptions
 * @returns A map of relative file paths to file contents
 */
export function generatePythonTemplate(opts: PythonTemplateOptions): GeneratedFileMap {
  const pluginName = opts.name;
  const pluginVersion = opts.version;

  const pluginJson = buildManifest(opts);

  // Sanitize name for Python module: replace hyphens with underscores
  const moduleName = pluginName.replace(/-/g, '_').replace(/@[^/]+\//, '');

  const pyprojectToml = `[project]
name = "${pluginName}"
version = "${pluginVersion}"
description = "${opts.description}"
authors = [{ name = "${opts.author}" }]
license = { text = "${opts.license}" }
requires-python = ">=3.10"
dependencies = []

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[tool.hatch.build.targets.wheel]
packages = ["src/${moduleName}"]
`;

  const tripleQuote = '"""';
  const srcPlugin = `${tripleQuote}
${pluginName} -- main entrypoint

This module exposes the primary plugin functions.

Example::

    from ${moduleName}.plugin import execute

    result = execute(input_data="hello world")
    print(result["output"])
${tripleQuote}

from __future__ import annotations

from typing import Any


def execute(input_data: str, context: dict[str, Any] | None = None) -> dict[str, Any]:
    ${tripleQuote}Execute the plugin's primary action.

    Args:
        input_data: The input payload for this plugin invocation.
        context: Optional context dictionary with additional parameters.

    Returns:
        A dictionary with keys:
            - output (str): The processed result.
            - success (bool): Whether the operation succeeded.

    Raises:
        ValueError: When input_data is empty.

    Example::

        result = execute(input_data="process this")
        assert result["success"] is True
    ${tripleQuote}
    if not input_data or not input_data.strip():
        raise ValueError("input_data must be a non-empty string")

    output = input_data.strip()
    return {"output": output, "success": True}
`;

  const readmeMd = `# ${pluginName}

${opts.description}

## Overview

**${pluginName}** is a ClaudeForge plugin authored by ${opts.author}.
Version: ${pluginVersion} | License: ${opts.license}

## Installation

Install using pip:

\`\`\`bash
pip install ${pluginName}
\`\`\`

Or with uv:

\`\`\`bash
uv add ${pluginName}
\`\`\`

Or add it to your ClaudeForge configuration:

\`\`\`bash
claude plugin install ${pluginName}
\`\`\`

## Configuration

Configure the plugin by setting environment variables or editing your \`claude.config.json\`:

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

\`\`\`python
from ${moduleName}.plugin import execute

result = execute(input_data="your input here")
print(result["output"])
\`\`\`

## API

### \`execute(input_data: str, context: dict | None = None) -> dict\`

Runs the plugin's primary action.

**Parameters:**
- \`input_data\` — The input payload.
- \`context\` — Optional context dictionary.

**Returns:** A dict with \`output\` (str) and \`success\` (bool).

**Raises:** \`ValueError\` when \`input_data\` is empty.

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
    'pyproject.toml': pyprojectToml,
    [`src/${moduleName}/plugin.py`]: srcPlugin,
    [`src/${moduleName}/__init__.py`]: `"""${pluginName} package."""\n`,
    'README.md': readmeMd,
    'tests/__init__.py': '',
    'tests/test_plugin.py': `"""Tests for ${pluginName}."""\nfrom ${moduleName}.plugin import execute\n\n\ndef test_execute_returns_output() -> None:\n    """Basic smoke test for execute()."""\n    result = execute(input_data="hello")\n    assert result["success"] is True\n    assert result["output"] == "hello"\n`,
    'docs/.gitkeep': '',
    'assets/.gitkeep': '',
  };
}
