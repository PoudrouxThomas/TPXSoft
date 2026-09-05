#!/usr/bin/env node
/**
 * Proves the PreToolUse guard actually guards.
 *
 * A broken hook reports success and enforces nothing, which is worse than no hook at
 * all because everyone believes it works. These are the ways a guard that only matches
 * Edit/Write gets walked through, plus the false positives that make people disable it.
 *
 * Usage: node tools/harness/hooks-selftest.mjs
 */
import { spawnSync } from 'node:child_process';

const HOOK = 'node tools/harness/hooks/block-generated.mjs';

const edit = (file) => ({ tool_name: 'Edit', tool_input: { file_path: file } });
const bash = (command) => ({ tool_name: 'Bash', tool_input: { command } });

const cases = [
  ['Edit of the committed contract', edit('contracts/openapi.json'), 'block'],
  ['Write into build output', { tool_name: 'Write', tool_input: { file_path: 'src/Api/obj/x.cs' } }, 'block'],
  ['Edit of migration metadata', edit('src/Infrastructure/Migrations/20240101_Init.Designer.cs'), 'block'],
  ['Edit of the model snapshot', edit('src/Infrastructure/Migrations/AppDbContextModelSnapshot.cs'), 'block'],
  ['sed -i on the contract', bash("sed -i 's/a/b/' contracts/openapi.json"), 'block'],
  ['heredoc redirect', bash("cat > contracts/openapi.json <<'JSON'\n{}\nJSON"), 'block'],
  ['python one-liner', bash('python -c "open(\'contracts/openapi.json\',\'w\').write(\'{}\')"'), 'block'],
  ['rm of the contract', bash('rm -rf contracts/openapi.json'), 'block'],
  ['cp over the contract', bash('cp /tmp/other.json contracts/openapi.json'), 'block'],
  ['echo redirect', bash("echo '{}' > contracts/openapi.json"), 'block'],
  ['git checkout of the contract', bash('git checkout -- contracts/openapi.json'), 'block'],
  ['chained write after a read', bash('cat package.json && sed -i s/x/y/ contracts/openapi.json'), 'block'],

  ['ordinary source edit', edit('src/Api/Program.cs'), 'allow'],
  ['reading the contract', bash('cat contracts/openapi.json'), 'allow'],
  ['grepping the contract', bash('grep -n paths contracts/openapi.json'), 'allow'],
  ['the generator itself', bash('dotnet build -p:OpenApiDocumentsDirectory=contracts'), 'allow'],
  ['a path that merely contains bin', bash('ls node_modules/.bin'), 'allow'],
  ['cleaning build output', bash('rm -rf src/Api/obj'), 'allow'],
];

let failed = 0;
for (const [name, payload, expected] of cases) {
  const res = spawnSync(HOOK, {
    shell: true,
    input: JSON.stringify(payload),
    encoding: 'utf8',
  });
  const actual = res.status === 2 ? 'block' : 'allow';
  if (actual !== expected) {
    failed += 1;
    console.error(`FAIL  ${name}: expected ${expected}, got ${actual} (exit ${res.status})`);
  }
}

if (failed) {
  console.error(`\n${failed}/${cases.length} hook self-tests failed.`);
  process.exit(1);
}
console.log(`hooks ok  ${cases.length}/${cases.length}`);
