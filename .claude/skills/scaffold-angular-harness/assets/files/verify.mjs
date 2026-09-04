#!/usr/bin/env node
/**
 * The verification loop. Every consumer -- npm scripts, Claude hooks, CI, humans --
 * calls this one command, never `ng` / `eslint` / `vitest` directly. That indirection
 * is what makes the underlying tools cheap to swap later.
 *
 * Contract:
 *   - stops at the first failing check
 *   - one line of output on success
 *   - on failure, prints only the failing check's output, capped
 *   - exit 0 green, exit 1 red
 *
 * Usage: node tools/harness/verify.mjs [format|lint|types|test ...]
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

const MAX_LINES = 40;
const pkg = JSON.parse(readFileSync('package.json', 'utf8'));
const harness = pkg.harness ?? {};

const hasEslint = [
  'eslint.config.mjs',
  'eslint.config.js',
  'eslint.config.ts',
  '.eslintrc.json',
].some((f) => existsSync(f));

const ALL = [
  { id: 'format', cmd: 'prettier --check . --cache --log-level warn', tail: false },
  {
    id: 'lint',
    cmd: 'eslint . --max-warnings 0 --cache --no-warn-ignored',
    tail: false,
    skip: !hasEslint,
    skipNote: 'no eslint config',
  },
  { id: 'types', cmd: 'node tools/harness/typecheck.mjs', tail: false },
  { id: 'test', cmd: harness.testCommand ?? 'ng test --watch=false', tail: true },
];

const wanted = process.argv.slice(2).filter((a) => !a.startsWith('-'));
const steps = wanted.length ? ALL.filter((s) => wanted.includes(s.id)) : ALL;

// node_modules/.bin is only on PATH inside an npm script; hooks and CI call us directly.
const binDir = path.resolve('node_modules/.bin');
const childEnv = {
  ...process.env,
  PATH: `${binDir}${path.delimiter}${process.env.PATH ?? ''}`,
  CI: 'true',
  NO_COLOR: '1',
  FORCE_COLOR: '0',
};

const stripAnsi = (s) => s.replace(/\u001B\[[0-9;]*[A-Za-z]/g, '');

function report(step, out, code, seconds) {
  const lines = stripAnsi(out)
    .split(/\r?\n/)
    .filter((l) => l.trim() !== '');
  const shown = step.tail ? lines.slice(-MAX_LINES) : lines.slice(0, MAX_LINES);
  console.error(`verify FAILED: ${step.id} (exit ${code}, ${seconds}s)`);
  console.error(shown.join('\n'));
  if (lines.length > MAX_LINES)
    console.error(`... ${lines.length - MAX_LINES} more lines suppressed`);
  if (step.id === 'format') console.error('fix: npm run format');
}

const timings = [];
const started = Date.now();

for (const step of steps) {
  if (step.skip) {
    timings.push(`${step.id} skipped(${step.skipNote})`);
    continue;
  }
  const t0 = Date.now();
  const res = spawnSync(step.cmd, {
    shell: true,
    encoding: 'utf8',
    env: childEnv,
    maxBuffer: 32 * 1024 * 1024,
  });
  const seconds = ((Date.now() - t0) / 1000).toFixed(1);
  if (res.status !== 0) {
    report(step, `${res.stdout ?? ''}\n${res.stderr ?? ''}`, res.status, seconds);
    process.exit(1);
  }
  timings.push(`${step.id} ${seconds}s`);
}

const total = ((Date.now() - started) / 1000).toFixed(1);
console.log(`verify ok  ${timings.join('  ')}  total ${total}s`);
