#!/usr/bin/env node
/**
 * Installs the Angular agent harness into an existing Angular workspace.
 *
 * Everything here is mechanical and idempotent, so it can be re-run after a partial
 * install without producing duplicates. The judgement calls -- whether there is an
 * OpenAPI backend, what the layering should be, whether CLAUDE.md is true -- belong
 * to the caller, not to this script.
 *
 * Usage:
 *   node install.mjs --root . [--generated src/app/api/generated]
 *                    [--spec <url-or-path> | --no-api] [--port 4200]
 *                    [--app-name web] [--force] [--no-install]
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
  const i = argv.indexOf(`--${name}`);
  return i === -1 ? fallback : (argv[i + 1] ?? true);
};
const has = (name) => argv.includes(`--${name}`);

const root = path.resolve(String(flag('root', '.')));
const generated = String(flag('generated', 'src/app/api/generated')).replace(/\\/g, '/');
// Forward slashes: this lands inside a JSON string, where a Windows backslash is an escape.
const specFlag = flag('spec', null);
const spec = typeof specFlag === 'string' ? specFlag.split('\\').join('/') : specFlag;
const port = String(flag('port', '4200'));
const force = has('force');
const doInstall = !has('no-install');

process.chdir(root);

const log = [];
const note = (line) => log.push(line);

// ---------------------------------------------------------------- helpers

const readJsonc = (file) => {
  const raw = fs.readFileSync(file, 'utf8');
  const stripped = raw.replace(/\\"|"(?:\\"|[^"])*"|(\/\/.*|\/\*[\s\S]*?\*\/)/g, (m, comment) =>
    comment ? '' : m,
  );
  return JSON.parse(stripped);
};
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
const ensureDir = (dir) => fs.mkdirSync(dir, { recursive: true });

// Filled in once angular.json has been read, below.
let styleConvention = 'CSS, separate template and style files';

const substitute = (text, appName) =>
  text
    .split('__STYLE_CONVENTION__')
    .join(styleConvention)
    .split('__GENERATED__')
    .join(generated)
    .split('__PORT__')
    .join(port)
    .split('__APP_NAME__')
    .join(appName)
    .split('__GEN_CMD__')
    .join(spec ? 'npm run gen:api' : 'the project’s client generation command')
    .split('__SPEC__')
    .join(String(spec ?? ''));

// Bundled files carry this skill's formatting, not the target repo's, and
// `npm run verify` starts with a prettier check -- so everything written here is
// re-formatted with the repo's own config at the end.
const written = [];

function copy(from, to, { appName, overwrite = force }) {
  const target = path.join(root, to);
  if (fs.existsSync(target) && !overwrite) {
    note(`kept    ${to} (already exists)`);
    return false;
  }
  const existed = fs.existsSync(target);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, substitute(fs.readFileSync(path.join(FILES, from), 'utf8'), appName));
  note(`${existed ? 'updated' : 'wrote  '} ${to}`);
  written.push(to);
  return true;
}

// ---------------------------------------------------------------- preflight

if (!fs.existsSync('angular.json')) {
  console.error(
    'No angular.json here. Create the workspace first, for example:\n' +
      '  npx @angular/cli@latest new <name> --style=scss --ssr=false\n' +
      'then run this installer inside it.',
  );
  process.exit(1);
}

const ngJson = readJsonc('angular.json');
const projectNames = Object.keys(ngJson.projects ?? {});
const appName = String(flag('app-name', projectNames[0] ?? 'app'));
const pkg = JSON.parse(fs.readFileSync('package.json', 'utf8'));

const angularMajor = Number(
  String(pkg.dependencies?.['@angular/core'] ?? '')
    .replace(/[^0-9.]/g, '')
    .split('.')[0] || 0,
);
const testBuilder = ngJson.projects?.[appName]?.architect?.test?.builder ?? '';
const usesKarma = /karma/i.test(testBuilder);

const componentSchematics =
  ngJson.projects?.[appName]?.schematics?.['@schematics/angular:component'] ?? {};
styleConvention = `${String(componentSchematics.style ?? 'css').toUpperCase()} styles, ${
  componentSchematics.inlineTemplate ? 'inline templates' : 'separate template and style files'
}`;

// ---------------------------------------------------------------- files

copy('verify.mjs', 'tools/harness/verify.mjs', { appName, overwrite: true });
copy('typecheck.mjs', 'tools/harness/typecheck.mjs', { appName, overwrite: true });
copy('hooks-selftest.mjs', 'tools/harness/hooks-selftest.mjs', { appName, overwrite: true });
for (const hook of ['paths.mjs', 'block-generated.mjs', 'verify-on-save.mjs', 'stop-verify.mjs'])
  copy(`hooks/${hook}`, `tools/harness/hooks/${hook}`, { appName, overwrite: true });

copy('eslint.config.mjs', 'eslint.config.mjs', { appName });
copy('prettierignore', '.prettierignore', { appName, overwrite: true });
if (!fs.existsSync('.prettierrc') && !fs.existsSync('.prettierrc.json'))
  copy('prettierrc.json', '.prettierrc', { appName, overwrite: true });
else note('kept    .prettierrc (already exists)');

copy('launch.json', '.claude/launch.json', { appName });
copy('agent-frontend-investigator.md', '.claude/agents/frontend-investigator.md', { appName });
copy('workflow-verify.yml', '.github/workflows/verify.yml', { appName });
copy('playwright.config.ts', 'playwright.config.ts', { appName });
copy('smoke.spec.ts', 'e2e/smoke.spec.ts', { appName });

if (fs.existsSync('CLAUDE.md') && !force) {
  copy('CLAUDE.md', 'CLAUDE.harness.md', { appName, overwrite: true });
  note('note    CLAUDE.md exists -- harness version written to CLAUDE.harness.md, merge by hand');
} else {
  copy('CLAUDE.md', 'CLAUDE.md', { appName, overwrite: true });
}

if (spec) copy('ng-openapi-gen.json', 'ng-openapi-gen.json', { appName, overwrite: true });

for (const dir of ['src/app/core', 'src/app/shared', 'src/app/features'])
  if (!fs.existsSync(dir)) {
    ensureDir(dir);
    fs.writeFileSync(path.join(dir, '.gitkeep'), '');
    note(`wrote   ${dir}/`);
  }

// ---------------------------------------------------------------- package.json

const testCommand = usesKarma
  ? 'ng test --watch=false --browsers=ChromeHeadless'
  : 'ng test --watch=false';

pkg.scripts = {
  ...pkg.scripts,
  verify: 'node tools/harness/verify.mjs',
  format: 'prettier --write . --cache',
  e2e: 'playwright test',
  ...(spec
    ? { 'gen:api': `ng-openapi-gen && prettier --write ${generated} --log-level warn` }
    : {}),
};
pkg.harness = { protectedPaths: [generated], testCommand };
writeJson('package.json', pkg);
note('patched package.json (scripts: verify, format, e2e' + (spec ? ', gen:api' : '') + ')');

// ---------------------------------------------------------------- tsconfig

const tsconfigPath = 'tsconfig.json';
const tsconfig = readJsonc(tsconfigPath);
tsconfig.compilerOptions = {
  ...tsconfig.compilerOptions,
  strict: true,
  noImplicitOverride: true,
  noPropertyAccessFromIndexSignature: true,
  noImplicitReturns: true,
  noFallthroughCasesInSwitch: true,
  noUnusedLocals: true,
};
tsconfig.angularCompilerOptions = {
  ...tsconfig.angularCompilerOptions,
  strictTemplates: true,
  strictInjectionParameters: true,
  strictInputAccessModifiers: true,
};
writeJson(tsconfigPath, tsconfig);
note('patched tsconfig.json (strict, strictTemplates -- comments dropped by rewrite)');

// ---------------------------------------------------------------- .claude/settings.json

const settingsPath = '.claude/settings.json';
const settings = fs.existsSync(settingsPath) ? readJsonc(settingsPath) : {};
settings.hooks ??= {};

const hookEntry = (matcher, command) => ({
  ...(matcher ? { matcher } : {}),
  hooks: [{ type: 'command', command }],
});
const mergeHook = (event, entry) => {
  settings.hooks[event] ??= [];
  const command = entry.hooks[0].command;
  const already = settings.hooks[event].some((e) =>
    (e.hooks ?? []).some((h) => h.command === command),
  );
  if (!already) settings.hooks[event].push(entry);
};

mergeHook(
  'PreToolUse',
  hookEntry('Edit|Write|MultiEdit', 'node tools/harness/hooks/block-generated.mjs'),
);
mergeHook('PreToolUse', hookEntry('Bash', 'node tools/harness/hooks/block-generated.mjs'));
mergeHook(
  'PostToolUse',
  hookEntry('Edit|Write|MultiEdit', 'node tools/harness/hooks/verify-on-save.mjs'),
);
mergeHook('Stop', hookEntry(null, 'node tools/harness/hooks/stop-verify.mjs'));

settings.permissions ??= {};
const allow = new Set(settings.permissions.allow ?? []);
for (const rule of [
  'Bash(npm run verify:*)',
  'Bash(npm run format)',
  'Bash(npm run build)',
  'Bash(npm run e2e:*)',
  'Bash(npx ng generate:*)',
  ...(spec ? ['Bash(npm run gen:api)'] : []),
  'Bash(git status:*)',
  'Bash(git diff:*)',
])
  allow.add(rule);
settings.permissions.allow = [...allow];

const deny = new Set(settings.permissions.deny ?? []);
// Token hygiene: a single accidental read of a lock file can cost more than a task.
for (const rule of [
  'Read(./node_modules/**)',
  'Read(./.angular/**)',
  'Read(./dist/**)',
  'Read(./package-lock.json)',
  `Read(./${generated}/**)`,
])
  deny.add(rule);
settings.permissions.deny = [...deny];

ensureDir('.claude');
writeJson(settingsPath, settings);
note('patched .claude/settings.json (hooks + permission allow/deny)');

// ---------------------------------------------------------------- .gitignore

if (fs.existsSync('.gitignore')) {
  const current = fs.readFileSync('.gitignore', 'utf8');
  const missing = ['.eslintcache', 'test-results/', 'playwright-report/'].filter(
    (entry) => !current.split(/\r?\n/).includes(entry),
  );
  if (missing.length) {
    fs.appendFileSync('.gitignore', `\n${missing.join('\n')}\n`);
    note(`patched .gitignore (${missing.join(', ')})`);
  }
}

// ---------------------------------------------------------------- dependencies

const devDeps = [
  'eslint',
  'angular-eslint',
  'typescript-eslint',
  '@eslint/js',
  'eslint-plugin-boundaries',
  'prettier',
  '@playwright/test',
  ...(spec ? ['ng-openapi-gen'] : []),
];
const command = `npm i -D ${devDeps.join(' ')}`;
note(doInstall ? `running ${command}` : `todo    ${command}`);

// ---------------------------------------------------------------- report

console.log(log.join('\n'));

if (doInstall) {
  const res = spawnSync(command, { shell: true, stdio: 'inherit' });
  if (res.status !== 0) console.log('WARNING install failed -- run it by hand and re-check');

  // Now that prettier is present, make the files we wrote match this repo's style.
  const formattable = written.filter((f) => /\.(mjs|ts|json|md|ya?ml)$/i.test(f));
  if (formattable.length) {
    const quoted = formattable.map((f) => `"${f}"`).join(' ');
    spawnSync(`npx prettier --write ${quoted} --log-level error`, { shell: true, stdio: 'inherit' });
  }
}
console.log('\nnext:');
if (usesKarma)
  console.log(
    `  ! this workspace still tests with Karma (${testBuilder}). A real browser is too slow for\n` +
      '    the inner loop -- migrate to the Vitest-based @angular/build:unit-test builder (Angular 20+)\n' +
      '    or Jest, then set harness.testCommand in package.json.',
  );
if (angularMajor && angularMajor < 16)
  console.log(`  ! Angular ${angularMajor}: flat ESLint config and ng-openapi-gen expect 16+.`);
console.log('  1. npm run format   (the repo must be prettier-clean before verify can be green)');
console.log('  2. npm run verify   (expect real findings on an existing codebase -- fix them)');
if (spec) console.log('  3. npm run gen:api  (generate the client, then verify again)');
