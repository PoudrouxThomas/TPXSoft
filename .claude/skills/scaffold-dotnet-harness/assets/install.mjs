#!/usr/bin/env node
/**
 * Scaffolds a .NET REST API and its agent harness.
 *
 * Everything here is mechanical and idempotent, so it can be re-run after a partial
 * install without producing duplicates. The judgement calls -- what the API is actually
 * for, whether CLAUDE.md is true, which architecture rules this team wants -- belong to
 * the caller, not to this script.
 *
 * Usage:
 *   node install.mjs --root . [--name Orders] [--tf net9.0] [--port 5080]
 *                    [--db orders] [--harness-only] [--force] [--offline]
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const FILES = path.join(HERE, 'files');

// ---------------------------------------------------------------- arguments

const argv = process.argv.slice(2);
const flag = (name, fallback = null) => {
  const i = argv.indexOf('--' + name);
  return i === -1 ? fallback : (argv[i + 1] ?? true);
};
const has = (name) => argv.includes('--' + name);

const root = path.resolve(String(flag('root', '.')));
if (!fs.existsSync(root)) fs.mkdirSync(root, { recursive: true });
process.chdir(root);

const force = has('force');
const harnessOnly = has('harness-only');
const offline = has('offline');
const port = String(flag('port', '5080'));

// Dots are kept: `Acme.Orders` is a normal .NET root namespace and squashing it to
// `AcmeOrders` would be a silent, hard-to-undo rename of every project.
const pascal = (s) =>
  String(s)
    .split('.')
    .map((segment) =>
      segment
        .split(/[^A-Za-z0-9]+/)
        .filter(Boolean)
        .map((p) => p[0].toUpperCase() + p.slice(1))
        .join(''),
    )
    .filter(Boolean)
    .join('.');

const name = pascal(flag('name', path.basename(root)) || 'Api');
if (!/^[A-Za-z][A-Za-z0-9.]*$/.test(name)) {
  console.error('--name must start with a letter and contain only letters, digits and dots.');
  process.exit(1);
}
const nameLower = name.toLowerCase().replace(/\./g, '-');
const db = String(flag('db', nameLower.replace(/-/g, '_')));

const log = [];
const note = (line) => log.push(line);

const runQuiet = (command) =>
  spawnSync(command, {
    shell: true,
    encoding: 'utf8',
    env: { ...process.env, DOTNET_CLI_UI_LANGUAGE: 'en', VSLANG: '1033', DOTNET_NOLOGO: '1' },
  });

// ---------------------------------------------------------------- SDK + versions

const sdks = runQuiet('dotnet --list-sdks')
  .stdout.split(/\r?\n/)
  .map((l) => l.trim().split(' ')[0])
  .filter((v) => /^\d+\.\d+\.\d+/.test(v));

if (sdks.length === 0) {
  console.error('No .NET SDK found on PATH. Install .NET 9 or newer, then re-run.');
  process.exit(1);
}

const majorOf = (v) => Number(v.split('.')[0]);
const bestSdk = sdks.reduce((a, b) => (compareVersions(a, b) >= 0 ? a : b));
const tf = String(flag('tf', 'net' + majorOf(bestSdk) + '.0'));
const major = Number(/net(\d+)\.0/.exec(tf)?.[1] ?? majorOf(bestSdk));

if (major < 9) {
  console.error(
    'This harness targets .NET 9 or newer: the OpenAPI document is produced by the ' +
      'built-in AddOpenApi(), which does not exist before .NET 9. Highest SDK found: ' +
      bestSdk +
      '.',
  );
  process.exit(1);
}

function compareVersions(a, b) {
  const pa = a.split(/[.-]/).map(Number);
  const pb = b.split(/[.-]/).map(Number);
  for (let i = 0; i < 3; i += 1) {
    const d = (pa[i] || 0) - (pb[i] || 0);
    if (d) return d;
  }
  return 0;
}

/**
 * Framework package versions track the target framework, so ask NuGet for the newest
 * stable release in that band rather than shipping a pin that rots. The fallback keeps
 * the installer working on a machine with no network, which is the normal case behind
 * a corporate proxy.
 */
async function latestStable(id, wantedMajor, fallback) {
  if (offline) return fallback;
  try {
    const res = await fetch(
      'https://api.nuget.org/v3-flatcontainer/' + id.toLowerCase() + '/index.json',
      { signal: AbortSignal.timeout(6000) },
    );
    if (!res.ok) return fallback;
    const { versions } = await res.json();
    const candidates = versions
      .filter((v) => !v.includes('-'))
      .filter((v) => majorOf(v) === wantedMajor);
    return candidates.length ? candidates.reduce((a, b) => (compareVersions(a, b) >= 0 ? a : b)) : fallback;
  } catch {
    return fallback;
  }
}

const fallbackFramework = { 9: '9.0.0', 10: '10.0.0' }[major] ?? major + '.0.0';
const fallbackNpgsql = { 9: '9.0.4', 10: '10.0.0' }[major] ?? major + '.0.0';

const versions = {
  __ASPNET__: await latestStable('Microsoft.AspNetCore.OpenApi', major, fallbackFramework),
  __EF__: await latestStable('Microsoft.EntityFrameworkCore', major, fallbackFramework),
  __NPGSQL__: await latestStable('Npgsql.EntityFrameworkCore.PostgreSQL', major, fallbackNpgsql),
  // Test packages float on their own schedule, so they stay pinned: a surprise xunit
  // major would break the harness on a machine that merely has newer network access.
  __TESTSDK__: '17.12.0',
  __XUNIT__: '2.9.2',
  __XUNITVS__: '2.8.2',
  __NETARCH__: '1.3.2',
  __TESTCONTAINERS__: '4.1.0',
};

// ---------------------------------------------------------------- existing repo?

const layout = {
  domain: 'src/' + name + '.Domain',
  infra: 'src/' + name + '.Infrastructure',
  api: 'src/' + name + '.Api',
  unit: 'tests/' + name + '.UnitTests',
  integration: 'tests/' + name + '.IntegrationTests',
};
const existingProjects = findProjects('.');

// Any project already here is somebody's real code until this installer has said
// otherwise in writing: scaffolding a sample Todo entity into a live API would be a
// genuinely destructive surprise. The flag it leaves in package.json is what makes a
// re-run over its own output fill gaps instead of refusing.
const previous = (() => {
  try {
    return JSON.parse(fs.readFileSync('package.json', 'utf8')).harness ?? {};
  } catch {
    return {};
  }
})();
const scaffold =
  !harnessOnly && (existingProjects.length === 0 || previous.scaffolded === true);

function findProjects(dir, acc = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === 'node_modules' || entry.name === 'bin' || entry.name === 'obj') continue;
    if (entry.name.startsWith('.')) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) findProjects(full, acc);
    else if (entry.name.endsWith('.csproj')) acc.push(full.split('\\').join('/').replace(/^\.\//, ''));
  }
  return acc;
}

const solutionName = (() => {
  const existing = fs.readdirSync('.').find((f) => f.endsWith('.sln') || f.endsWith('.slnx'));
  return existing ?? name + '.sln';
})();

// ---------------------------------------------------------------- substitution

const tokens = {
  __NAME__: name,
  __NAME_LOWER__: nameLower,
  __TFM__: tf,
  __PORT__: port,
  __DB__: db,
  __SOLUTION__: solutionName,
  __VERIFY_SOLUTION__: 'verify.slnf',
  ...versions,
};

const substitute = (text) =>
  Object.entries(tokens).reduce((acc, [k, v]) => acc.split(k).join(String(v)), text);

const written = [];

function copy(from, to, { overwrite = force } = {}) {
  const target = path.join(root, to);
  if (fs.existsSync(target) && !overwrite) {
    note('kept    ' + to + ' (already exists)');
    return false;
  }
  const existed = fs.existsSync(target);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, substitute(fs.readFileSync(path.join(FILES, from), 'utf8')));
  note((existed ? 'updated ' : 'wrote   ') + to);
  written.push(to);
  return true;
}

// ---------------------------------------------------------------- harness files

copy('verify.mjs', 'tools/harness/verify.mjs', { overwrite: true });
copy('openapi.mjs', 'tools/harness/openapi.mjs', { overwrite: true });
copy('diagnostics.mjs', 'tools/harness/diagnostics.mjs', { overwrite: true });
copy('hooks-selftest.mjs', 'tools/harness/hooks-selftest.mjs', { overwrite: true });
for (const hook of ['paths.mjs', 'block-generated.mjs', 'verify-on-save.mjs', 'stop-verify.mjs'])
  copy('hooks/' + hook, 'tools/harness/hooks/' + hook, { overwrite: true });

copy('Directory.Build.props', 'Directory.Build.props');
copy('Directory.Packages.props', 'Directory.Packages.props');
copy('editorconfig', '.editorconfig');
copy('docker-compose.yml', 'docker-compose.yml');
copy('launch.json', '.claude/launch.json');
copy('agent-api-investigator.md', '.claude/agents/api-investigator.md');
copy('workflow-verify.yml', '.github/workflows/verify.yml');

if (fs.existsSync('.claude/settings.json') && !force) {
  copy('settings.json', '.claude/settings.harness.json', { overwrite: true });
  note('note    .claude/settings.json exists -- harness version written alongside, merge by hand');
} else {
  copy('settings.json', '.claude/settings.json', { overwrite: true });
}

if (fs.existsSync('CLAUDE.md') && !force) {
  copy('CLAUDE.md', 'CLAUDE.harness.md', { overwrite: true });
  note('note    CLAUDE.md exists -- harness version written to CLAUDE.harness.md, merge by hand');
} else {
  copy('CLAUDE.md', 'CLAUDE.md', { overwrite: true });
}

// .gitignore is appended rather than replaced: it usually already carries rules that
// matter to this repo and losing them silently is a bad trade for a tidier file.
{
  const wanted = fs.readFileSync(path.join(FILES, 'gitignore'), 'utf8').split(/\r?\n/).filter(Boolean);
  const current = fs.existsSync('.gitignore') ? fs.readFileSync('.gitignore', 'utf8') : '';
  const missing = wanted.filter((line) => !current.split(/\r?\n/).includes(line));
  if (missing.length) {
    fs.writeFileSync(
      '.gitignore',
      (current ? current.replace(/\s*$/, '\n') : '') + missing.join('\n') + '\n',
    );
    note((current ? 'updated ' : 'wrote   ') + '.gitignore (+' + missing.length + ' rules)');
  }
}

// global.json pins the SDK so CI and every developer compile against one compiler.
if (!fs.existsSync('global.json') || force) {
  fs.writeFileSync(
    'global.json',
    JSON.stringify({ sdk: { version: bestSdk, rollForward: 'latestFeature' } }, null, 2) + '\n',
  );
  note('wrote   global.json (SDK ' + bestSdk + ')');
}

// ---------------------------------------------------------------- the API itself

if (scaffold) {
  const { domain, infra, api, unit, integration } = layout;

  copy('src/Domain.csproj.tpl', domain + '/' + name + '.Domain.csproj');
  copy('src/Todo.cs.tpl', domain + '/Todo.cs');
  copy('src/ITodoRepository.cs.tpl', domain + '/ITodoRepository.cs');

  copy('src/Infrastructure.csproj.tpl', infra + '/' + name + '.Infrastructure.csproj');
  copy('src/AppDbContext.cs.tpl', infra + '/AppDbContext.cs');
  copy('src/TodoRepository.cs.tpl', infra + '/TodoRepository.cs');
  copy(
    'src/InfrastructureServiceCollectionExtensions.cs.tpl',
    infra + '/InfrastructureServiceCollectionExtensions.cs',
  );

  copy('src/Api.csproj.tpl', api + '/' + name + '.Api.csproj');
  copy('src/Program.cs.tpl', api + '/Program.cs');
  copy('src/TodoEndpoints.cs.tpl', api + '/Endpoints/TodoEndpoints.cs');
  copy('src/TodoContracts.cs.tpl', api + '/Contracts/TodoContracts.cs');
  copy('src/appsettings.json.tpl', api + '/appsettings.json');
  copy('src/appsettings.Development.json.tpl', api + '/appsettings.Development.json');

  copy('tests/UnitTests.csproj.tpl', unit + '/' + name + '.UnitTests.csproj');
  copy('tests/ArchitectureTests.cs.tpl', unit + '/ArchitectureTests.cs');
  copy('tests/TodoTests.cs.tpl', unit + '/TodoTests.cs');

  copy('tests/IntegrationTests.csproj.tpl', integration + '/' + name + '.IntegrationTests.csproj');
  copy('tests/ApiFactory.cs.tpl', integration + '/ApiFactory.cs');
  copy('tests/TodoApiTests.cs.tpl', integration + '/TodoApiTests.cs');
} else if (!harnessOnly) {
  note(
    'note    ' + existingProjects.length + ' existing project(s) found -- harness only, ' +
      'nothing scaffolded',
  );
}

// ---------------------------------------------------------------- solution + filter

const projects = findProjects('.');

if (!fs.existsSync(solutionName)) {
  const res = runQuiet('dotnet new sln -n "' + path.basename(solutionName, '.sln') + '"');
  if (res.status !== 0) note('warn    could not create ' + solutionName + ': ' + (res.stderr || '').trim());
  else note('wrote   ' + solutionName);
}

if (fs.existsSync(solutionName) && projects.length) {
  const inSolution = runQuiet('dotnet sln "' + solutionName + '" list').stdout;
  const toAdd = projects.filter((p) => !inSolution.split('\\').join('/').includes(p));
  if (toAdd.length) {
    const res = runQuiet('dotnet sln "' + solutionName + '" add ' + toAdd.map((p) => '"' + p + '"').join(' '));
    if (res.status !== 0) note('warn    dotnet sln add failed: ' + (res.stderr || '').trim());
    else note('updated ' + solutionName + ' (+' + toAdd.length + ' projects)');
  }
}

// The filter starts out holding every project, because a project the loop does not
// compile is a project an agent can leave broken and still be told it is done. Its
// reason to exist is what comes later: when someone adds a worker, a second host or
// anything else that holds a lock on build output, removing it from this list is how
// the loop stops failing with a file-lock error that reads like a compile error.
{
  // Paths must match the .sln byte for byte -- MSBuild compares them as strings, and a
  // forward slash where the solution wrote a backslash fails with MSB5028. So they are
  // taken from `dotnet sln list` rather than rebuilt from the filesystem.
  const listed = runQuiet('dotnet sln "' + solutionName + '" list')
    .stdout.split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l.toLowerCase().endsWith('.csproj'));

  if (listed.length === 0) {
    note('warn    could not read projects from ' + solutionName + '; verify.slnf not written');
  } else {
    fs.writeFileSync(
      'verify.slnf',
      JSON.stringify({ solution: { path: solutionName, projects: listed } }, null, 2) + '\n',
    );
    note('wrote   verify.slnf (' + listed.length + ' projects)');
  }
}

// ---------------------------------------------------------------- package.json

/**
 * `dotnet test` accepts a .slnf and then runs nothing at all, exit code zero -- a gate
 * that enforces nothing while reporting success. So verify names the test projects
 * explicitly, and they are discovered here rather than guessed at run time.
 */
{
  const allProjects = findProjects('.');
  const testProjects = allProjects.filter((p) => /tests?\b|\.tests?\.csproj$/i.test(p));
  const integrationTestProjects = testProjects.filter((p) => /integration|\.e2e\b/i.test(p));
  const unitTestProjects = testProjects.filter((p) => !integrationTestProjects.includes(p));

  if (unitTestProjects.length === 0)
    note('warn    no unit test project found -- add one and list it in package.json > harness.testProjects');

  const template = JSON.parse(substitute(fs.readFileSync(path.join(FILES, 'package.json'), 'utf8')));
  const existed = fs.existsSync('package.json');
  const current = existed ? JSON.parse(fs.readFileSync('package.json', 'utf8')) : {};
  const merged = {
    ...current,
    ...(current.name ? {} : { name: template.name }),
    private: true,
    scripts: { ...current.scripts, ...template.scripts },
    harness: {
      ...current.harness,
      ...template.harness,
      testProjects: unitTestProjects,
      integrationTestProjects,
      scaffolded: scaffold || previous.scaffolded === true,
    },
  };
  fs.writeFileSync('package.json', JSON.stringify(merged, null, 2) + '\n');
  note((existed ? 'updated ' : 'wrote   ') + 'package.json');
}

// ---------------------------------------------------------------- report

console.log(log.join('\n'));
console.log(
  '\nnext:\n' +
    '  dotnet restore\n' +
    '  npm run verify build && npm run openapi:accept\n' +
    '  npm run format          # import order depends on the project name, so do this once\n' +
    '  npm run verify\n' +
    '  npm run hooks:selftest',
);
