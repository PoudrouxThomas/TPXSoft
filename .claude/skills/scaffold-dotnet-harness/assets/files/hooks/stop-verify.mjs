#!/usr/bin/env node
/**
 * Stop hook: the definition of done.
 *
 * Exit 2 is deliberate -- exit 1 is a non-blocking warning the agent never sees, so a
 * red verify would silently become "task complete". `stop_hook_active` stops this from
 * looping forever when the agent stops again on a still-red tree.
 */
import { spawnSync } from 'node:child_process';
import { readHookInput } from './paths.mjs';

const input = readHookInput();
if (input.stop_hook_active) process.exit(0);

const res = spawnSync('node tools/harness/verify.mjs', {
  shell: true,
  encoding: 'utf8',
  maxBuffer: 64 * 1024 * 1024,
});

if (res.status === 0) process.exit(0);

const output = (String(res.stdout ?? '') + '\n' + String(res.stderr ?? ''))
  .split(/\r?\n/)
  .filter((l) => l.trim() !== '')
  .slice(0, 40)
  .join('\n');

console.error('`npm run verify` is red. Fix it before finishing.\n' + output);
process.exit(2);
