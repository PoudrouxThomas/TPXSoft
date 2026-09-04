#!/usr/bin/env node
/**
 * Proves the generated-client guard actually blocks, including through Bash.
 *
 * A PreToolUse hook that matches only Edit/Write feels protective and is not: `sed -i`,
 * a heredoc, or a two-line python script walk straight through it. Run this after
 * install, and again whenever the hook or the protected paths change.
 *
 * Usage: node tools/harness/hooks-selftest.mjs
 */
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';

const pkg = JSON.parse(readFileSync('package.json', 'utf8'));
const target = (pkg.harness?.protectedPaths ?? [])[0];

if (!target) {
  console.error('No package.json > harness.protectedPaths to test against.');
  process.exit(1);
}

const cases = [
  [
    'Write into the client',
    { tool_name: 'Write', tool_input: { file_path: `${target}/api.service.ts` } },
    2,
  ],
  [
    'sed -i',
    { tool_name: 'Bash', tool_input: { command: `sed -i s/a/b/ ${target}/api.service.ts` } },
    2,
  ],
  [
    'heredoc redirect',
    { tool_name: 'Bash', tool_input: { command: `cat > ${target}/x.ts <<EOF` } },
    2,
  ],
  [
    'python one-liner',
    { tool_name: 'Bash', tool_input: { command: `python -c "open('${target}/x.ts','w')"` } },
    2,
  ],
  ['rm -rf', { tool_name: 'Bash', tool_input: { command: `rm -rf ${target}` } }, 2],
  [
    'append in a chain',
    { tool_name: 'Bash', tool_input: { command: `ls && printf x >> ${target}/x.ts` } },
    2,
  ],
  ['reading is allowed', { tool_name: 'Bash', tool_input: { command: `cat ${target}/x.ts` } }, 0],
  [
    'grepping is allowed',
    { tool_name: 'Bash', tool_input: { command: `grep -r foo ${target}` } },
    0,
  ],
  ['regenerating is allowed', { tool_name: 'Bash', tool_input: { command: 'npm run gen:api' } }, 0],
  ['ordinary edits pass', { tool_name: 'Edit', tool_input: { file_path: 'src/app/app.ts' } }, 0],
];

let failed = 0;
for (const [name, payload, expected] of cases) {
  const res = spawnSync(process.execPath, ['tools/harness/hooks/block-generated.mjs'], {
    input: JSON.stringify(payload),
    encoding: 'utf8',
  });
  const ok = res.status === expected;
  if (!ok) failed++;
  console.log(`${ok ? 'pass' : 'FAIL'}  ${name} -> exit ${res.status} (expected ${expected})`);
}

if (failed) {
  console.error(
    `\n${failed} guard case(s) failed -- the generated client is not actually protected.`,
  );
  process.exit(1);
}
console.log(`\nall ${cases.length} guard cases pass`);
