#!/usr/bin/env node
/**
 * PostToolUse hook: type-check the project that owns the file just edited.
 *
 * Narrow on purpose. The full loop runs on Stop; this one exists so a type or
 * template error is fed back within seconds of the edit that caused it, while the
 * agent still has the context to fix it cheaply.
 */
import { spawnSync } from 'node:child_process';
import { readHookInput, protectedPaths, isProtected } from './paths.mjs';

const input = readHookInput();
const file = input.tool_input?.file_path ?? '';

if (!/\.(ts|html)$/i.test(file)) process.exit(0);
if (isProtected(file, protectedPaths())) process.exit(0);

const res = spawnSync(`node tools/harness/typecheck.mjs --for "${file}"`, {
  shell: true,
  encoding: 'utf8',
  maxBuffer: 32 * 1024 * 1024,
});

if (res.status === 0) process.exit(0);

const lines = `${res.stdout ?? ''}\n${res.stderr ?? ''}`
  .split(/\r?\n/)
  .filter((l) => l.trim() !== '')
  .slice(0, 25);

console.error(
  `Type check failed after editing ${file}:\n${lines.join('\n')}\n` +
    'If this is a half-finished multi-file change, keep going -- the Stop hook is the gate.',
);
process.exit(2);
