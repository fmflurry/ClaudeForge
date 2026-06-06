/**
 * Go plugin scaffold template.
 *
 * Returns a GeneratedFileMap containing all files for a Go plugin project.
 */

import type { GeneratedFileMap } from '../generator.js';

interface GoTemplateOptions {
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
 * Generates the canonical plugin.json manifest content for a Go plugin.
 */
function buildManifest(opts: GoTemplateOptions): string {
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
 * Generates a Go plugin scaffold file map.
 *
 * @param opts - Template options populated from GeneratorOptions
 * @returns A map of relative file paths to file contents
 */
export function generateGoTemplate(opts: GoTemplateOptions): GeneratedFileMap {
  const pluginName = opts.name;
  const pluginVersion = opts.version;

  const pluginJson = buildManifest(opts);

  // Sanitize module name: strip @scope/ prefix, keep hyphens (valid in Go module paths)
  const modulePath = `github.com/claudeforge/${pluginName.replace(/@[^/]+\//, '')}`;

  const goMod = `module ${modulePath}

go 1.22

require ()
`;

  const srcHandler = `// Package plugin implements the ${pluginName} ClaudeForge plugin.
package plugin

import "errors"

// ExecuteOptions holds the parameters for a plugin invocation.
type ExecuteOptions struct {
	// Input is the primary payload passed to the plugin.
	Input string
	// Context carries optional key-value metadata.
	Context map[string]interface{}
}

// ExecuteResult is the value returned by Execute.
type ExecuteResult struct {
	// Output holds the processed result string.
	Output string
	// Success indicates whether the operation completed without error.
	Success bool
}

// Execute runs the plugin's primary action.
//
// It accepts an ExecuteOptions value and returns an ExecuteResult.
// An error is returned when the input is empty.
//
// Example usage:
//
//	result, err := Execute(ExecuteOptions{Input: "hello"})
//	if err != nil {
//	    log.Fatal(err)
//	}
//	fmt.Println(result.Output)
func Execute(opts ExecuteOptions) (ExecuteResult, error) {
	if opts.Input == "" {
		return ExecuteResult{}, errors.New("input must not be empty")
	}

	return ExecuteResult{
		Output:  opts.Input,
		Success: true,
	}, nil
}
`;

  const readmeMd = `# ${pluginName}

${opts.description}

## Overview

**${pluginName}** is a ClaudeForge plugin authored by ${opts.author}.
Version: ${pluginVersion} | License: ${opts.license}

Module: \`${modulePath}\`

## Installation

Fetch the module with Go:

\`\`\`bash
go get ${modulePath}
\`\`\`

Or add it to your ClaudeForge configuration:

\`\`\`bash
claude plugin install ${pluginName}
\`\`\`

Build from source:

\`\`\`bash
go build ./...
\`\`\`

Run tests:

\`\`\`bash
go test ./...
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

\`\`\`go
import "github.com/claudeforge/${pluginName}"

result, err := plugin.Execute(plugin.ExecuteOptions{Input: "your input here"})
if err != nil {
    log.Fatal(err)
}
fmt.Println(result.Output)
\`\`\`

## API

### \`Execute(opts ExecuteOptions) (ExecuteResult, error)\`

Runs the plugin's primary action.

**Parameters:**
- \`opts.Input\` (\`string\`) — The input payload.
- \`opts.Context\` (\`map[string]interface{}\`) — Optional context.

**Returns:** \`ExecuteResult\` — \`{ Output string, Success bool }\` and an error.

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
    'go.mod': goMod,
    'src/plugin.go': srcHandler,
    'README.md': readmeMd,
    'tests/plugin_test.go': `// Tests for ${pluginName}\npackage plugin_test\n\nimport (\n\t"testing"\n\n\tplugin "${modulePath}/src"\n)\n\nfunc TestExecute(t *testing.T) {\n\tresult, err := plugin.Execute(plugin.ExecuteOptions{Input: "hello"})\n\tif err != nil {\n\t\tt.Fatalf("unexpected error: %v", err)\n\t}\n\tif !result.Success {\n\t\tt.Error("expected Success to be true")\n\t}\n}\n`,
    'docs/.gitkeep': '',
    'assets/.gitkeep': '',
  };
}
