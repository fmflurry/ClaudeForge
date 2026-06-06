#!/usr/bin/env node

/**
 * Claude Plugin CLI — entry point.
 *
 * Wires the dispatcher and calls process.exit at the outer layer only.
 */

import { createProgram } from './dispatcher.js';

const program = createProgram();

program.parseAsync(process.argv).then(() => {
  process.exit(0);
}).catch((err: unknown) => {
  const message = err instanceof Error ? err.message : String(err);
  process.stderr.write(`Error: ${message}\n`);
  process.exit(1);
});
