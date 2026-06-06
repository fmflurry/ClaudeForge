/**
 * Tests for src/dispatcher.ts (CLI routing)
 *
 * Production module path: src/dispatcher.ts
 * Purpose: maps `claude-plugin <subcommand> [args]` to the correct command function.
 *
 * Exported functions:
 *   - createProgram(deps?: DispatcherDeps): Command
 *       → returns a configured commander Program (does NOT call parse() itself — caller does)
 *       → deps allows injecting fake command runners so tests stay unit-level
 *   - DispatcherDeps: {
 *       runInstall?: typeof runInstall;
 *       runRemove?: typeof runRemove;
 *       runList?: typeof runList;
 *       runUpdate?: typeof runUpdate;
 *       runSearch?: typeof runSearch;
 *       runPublish?: typeof runPublish;
 *       runScaffold?: typeof runScaffold;
 *       runValidate?: typeof runValidate;
 *       runConfigSet?: typeof runConfigSet;
 *       runConfigShow?: typeof runConfigShow;
 *     }
 *
 * The dispatcher is LIGHT — tests verify:
 *   1. Each subcommand name is registered.
 *   2. Parsing a subcommand routes to the right injected handler.
 *   3. --help does not throw.
 *   4. Unknown subcommands produce a helpful message.
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/dispatcher.ts is created (RED state).
import { createProgram } from '../dispatcher.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const SUBCOMMANDS = [
  'install',
  'remove',
  'list',
  'update',
  'search',
  'publish',
  'scaffold',
  'validate',
  'config',
] as const;

// ---------------------------------------------------------------------------
// Registration
// ---------------------------------------------------------------------------

describe('createProgram – subcommand registration', () => {
  it('registers all 9 subcommands', () => {
    const program = createProgram();
    const names = program.commands.map((c) => c.name());
    for (const sub of SUBCOMMANDS) {
      expect(names).toContain(sub);
    }
  });

  it('program name is claude-plugin', () => {
    const program = createProgram();
    expect(program.name()).toBe('claude-plugin');
  });
});

// ---------------------------------------------------------------------------
// Routing — each subcommand calls the right injected handler
// ---------------------------------------------------------------------------

describe('createProgram – routing', () => {
  it('install subcommand invokes runInstall', async () => {
    const runInstall = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runInstall });
    await program.parseAsync(['node', 'claude-plugin', 'install', '@ns/plugin'], { from: 'user' });
    expect(runInstall).toHaveBeenCalled();
  });

  it('remove subcommand invokes runRemove', async () => {
    const runRemove = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runRemove });
    await program.parseAsync(['node', 'claude-plugin', 'remove', '@ns/plugin'], { from: 'user' });
    expect(runRemove).toHaveBeenCalled();
  });

  it('list subcommand invokes runList', async () => {
    const runList = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runList });
    await program.parseAsync(['node', 'claude-plugin', 'list'], { from: 'user' });
    expect(runList).toHaveBeenCalled();
  });

  it('update subcommand invokes runUpdate', async () => {
    const runUpdate = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runUpdate });
    await program.parseAsync(['node', 'claude-plugin', 'update', '@ns/plugin'], { from: 'user' });
    expect(runUpdate).toHaveBeenCalled();
  });

  it('search subcommand invokes runSearch', async () => {
    const runSearch = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runSearch });
    await program.parseAsync(['node', 'claude-plugin', 'search', 'authentication'], { from: 'user' });
    expect(runSearch).toHaveBeenCalled();
  });

  it('publish subcommand invokes runPublish', async () => {
    const runPublish = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runPublish });
    await program.parseAsync(['node', 'claude-plugin', 'publish'], { from: 'user' });
    expect(runPublish).toHaveBeenCalled();
  });

  it('scaffold subcommand invokes runScaffold', async () => {
    const runScaffold = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runScaffold });
    await program.parseAsync(['node', 'claude-plugin', 'scaffold'], { from: 'user' });
    expect(runScaffold).toHaveBeenCalled();
  });

  it('validate subcommand invokes runValidate', async () => {
    const runValidate = vi.fn().mockResolvedValue({ exitCode: 0, output: '' });
    const program = createProgram({ runValidate });
    await program.parseAsync(['node', 'claude-plugin', 'validate'], { from: 'user' });
    expect(runValidate).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Flags and options
// ---------------------------------------------------------------------------

describe('createProgram – flags', () => {
  it('list --check-updates flag is registered', () => {
    const program = createProgram();
    const listCmd = program.commands.find((c) => c.name() === 'list');
    expect(listCmd).toBeDefined();
    const optionNames = listCmd!.options.map((o) => o.long);
    expect(optionNames).toContain('--check-updates');
  });

  it('search --limit flag is registered', () => {
    const program = createProgram();
    const searchCmd = program.commands.find((c) => c.name() === 'search');
    expect(searchCmd).toBeDefined();
    const optionNames = searchCmd!.options.map((o) => o.long);
    expect(optionNames).toContain('--limit');
  });

  it('scaffold --name flag is registered', () => {
    const program = createProgram();
    const scaffoldCmd = program.commands.find((c) => c.name() === 'scaffold');
    expect(scaffoldCmd).toBeDefined();
    const optionNames = scaffoldCmd!.options.map((o) => o.long);
    expect(optionNames).toContain('--name');
  });

  it('scaffold --language flag is registered', () => {
    const program = createProgram();
    const scaffoldCmd = program.commands.find((c) => c.name() === 'scaffold');
    const optionNames = scaffoldCmd!.options.map((o) => o.long);
    expect(optionNames).toContain('--language');
  });

  it('scaffold --interactive flag is registered', () => {
    const program = createProgram();
    const scaffoldCmd = program.commands.find((c) => c.name() === 'scaffold');
    const optionNames = scaffoldCmd!.options.map((o) => o.long);
    expect(optionNames).toContain('--interactive');
  });
});

// ---------------------------------------------------------------------------
// --help does not throw
// ---------------------------------------------------------------------------

describe('createProgram – --help', () => {
  it('does not throw when outputHelp() is called', () => {
    const program = createProgram();
    expect(() => program.outputHelp()).not.toThrow();
  });
});
