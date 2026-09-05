#!/usr/bin/env node
/**
 * The verification loop: build -> format -> unit + architecture tests -> contract.
 *
 * Two numbers govern every choice in this file: under 60 seconds, and under 15 lines
 * of output when green. MSBuild and VSTest are extremely chatty by default and every
 * line they print lands in the model's context on every run, so output is swallowed
 * and only the first real failure is shown.
 *
 * Build runs before format because `dotnet format` has to load the MSBuild workspace
 * anyway -- doing the restore once and passing --no-restore afterwards is the single
 * biggest saving here -- and because a compile error matters more than whitespace.
 *
 * Usage: node tools/harness/verify.mjs [build|format|test|contract]
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import { clean, diagnostics, outputLines as lines } from './diagnostics.mjs';

const harness = (() => {
  try {
    return JSON.parse(fs.readFileSync('package.json', 'utf8')).harness ?? {};
  } catch {
    return {};
  }
})();

const solution = harness.verifySolution ?? 'verify.slnf';
const config = harness.configuration ?? 'Debug';

// `dotnet` emits diagnostics in the machine's locale. On a French or German Windows
// install that means compiler errors the agent has to translate and greps that break
// per-machine, so the tool language is pinned regardless of who is running it.
const env = {
  ...process.env,
  DOTNET_CLI_UI_LANGUAGE: 'en',
  // VSLANG is the one MSBuild and the Roslyn tooling read; DOTNET_CLI_UI_LANGUAGE alone
  // leaves `dotnet format` speaking the machine's language.
  VSLANG: '1033',
  DOTNET_NOLOGO: '1',
  DOTNET_CLI_TELEMETRY_OPTOUT: '1',
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1',
};

const run = (command) =>
  spawnSync(command, { shell: true, encoding: 'utf8', env, maxBuffer: 64 * 1024 * 1024 });

/** Show the first thing that actually broke, not the whole log. */
function excerpt(result, { pattern, before = 0, max = 20, drop = null }) {
  const all = lines(result)
    .map(clean)
    .filter((l) => !drop || !drop.test(l));
  const hit = pattern ? all.findIndex((l) => pattern.test(l)) : -1;
  const from = hit === -1 ? Math.max(0, all.length - max) : Math.max(0, hit - before);
  return all.slice(from, from + max).join('\n');
}

const steps = {
  build: () => {
    const res = run(`dotnet build ${solution} -c ${config} --nologo -v quiet`);
    if (res.status === 0) return null;
    return diagnostics(res);
  },

  format: () => {
    const res = run(`dotnet format ${solution} --verify-no-changes --no-restore -v quiet`);
    if (res.status === 0) return null;
    return `${excerpt(res, { pattern: /warn |error /i, max: 10 })}\n\nRun \`npm run format\` to fix.`;
  },

  // Named projects rather than the solution, for two reasons. `dotnet test` accepts a
  // .slnf argument and then silently runs nothing -- a green gate that enforces nothing
  // is worse than no gate. And running the solution interleaves output from parallel
  // test projects, which is both longer and harder to read back.
  //
  // The trait filter is belt and braces: the integration project is not in this list
  // either, so nothing here ever starts a container.
  test: () => runTests(harness.testProjects ?? [], 'Category!=Integration'),

  contract: () => {
    const res = run('node tools/harness/openapi.mjs check');
    return res.status === 0 ? null : lines(res).slice(0, 25).join('\n');
  },

  // Deliberately not in DEFAULT: it starts a Postgres container. Runs in CI and on
  // demand via `npm run verify:it`, never from the Stop hook.
  integration: () =>
    runTests(harness.integrationTestProjects ?? [], 'Category=Integration', { build: true }),
};

function runTests(projects, filter, { build = false } = {}) {
  if (projects.length === 0) {
    return (
      'No test projects configured. Add them to package.json > harness.' +
      (build ? 'integrationTestProjects' : 'testProjects') +
      ' -- an empty list here would otherwise pass silently, which is the worst ' +
      'possible outcome for a gate.'
    );
  }

  for (const project of projects) {
    const res = run(
      `dotnet test "${project}" -c ${config} --nologo ${build ? '' : '--no-build --no-restore '}` +
        `--filter "${filter}" --logger "console;verbosity=minimal"`,
    );
    if (res.status !== 0)
      // Framework stack frames are the bulk of a failure report and none of its meaning.
      return excerpt(res, {
        pattern: /^\s*(Failed|X)\s|error|Assert\./i,
        before: 1,
        max: 20,
        drop: /^\s*at (System|Microsoft|Xunit)\./,
      });
  }
  return null;
}

const DEFAULT = ['build', 'format', 'test', 'contract'];

const only = process.argv[2];
const chosen = only ? [only] : DEFAULT;
if (only && !steps[only]) {
  console.error(`Unknown step "${only}". One of: ${Object.keys(steps).join(', ')}`);
  process.exit(1);
}

const timings = [];
const started = Date.now();

for (const name of chosen) {
  const at = Date.now();
  const failure = steps[name]();
  const seconds = ((Date.now() - at) / 1000).toFixed(1);
  if (failure !== null) {
    console.error(`verify FAILED at ${name} (${seconds}s)\n\n${failure}`);
    process.exit(1);
  }
  timings.push(`${name} ${seconds}s`);
}

const total = ((Date.now() - started) / 1000).toFixed(1);
console.log(`verify ok  ${timings.join('  ')}  (total ${total}s)`);
