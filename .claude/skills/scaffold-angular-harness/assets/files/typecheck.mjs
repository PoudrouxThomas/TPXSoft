#!/usr/bin/env node
/**
 * Type checking, including Angular templates.
 *
 * Plain `tsc --noEmit` does not look inside templates, so a `strictTemplates`
 * violation compiles clean and shows up as a blank page at runtime -- exactly the
 * failure an agent cannot see and will report as done. `ngc` (from
 * @angular/compiler-cli) runs the same check the production build runs, in about
 * the same time as tsc.
 *
 * Usage:
 *   node tools/harness/typecheck.mjs                # every project in angular.json
 *   node tools/harness/typecheck.mjs --for src/app/foo.ts   # only owning project
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

const stripJsonComments = (s) =>
  s.replace(/\\"|"(?:\\"|[^"])*"|(\/\/.*|\/\*[\s\S]*?\*\/)/g, (m, g) => (g ? '' : m));
const readJson = (f) => JSON.parse(stripJsonComments(readFileSync(f, 'utf8')));
const norm = (p) => p.split(path.sep).join('/');

// node_modules/.bin is only on PATH inside an npm script; hooks and CI call us directly.
const binDir = path.resolve('node_modules/.bin');
const env = {
  ...process.env,
  PATH: `${binDir}${path.delimiter}${process.env.PATH ?? ''}`,
  NO_COLOR: '1',
  FORCE_COLOR: '0',
};

const hasNgc = existsSync('node_modules/@angular/compiler-cli');
const compiler = hasNgc ? 'ngc' : 'tsc';

/** @returns {{tsconfig:string, root:string}[]} */
function discoverTargets() {
  const out = [];
  if (existsSync('angular.json')) {
    const ng = readJson('angular.json');
    for (const [name, project] of Object.entries(ng.projects ?? {})) {
      for (const target of ['build', 'test']) {
        const tsconfig = project.architect?.[target]?.options?.tsConfig;
        if (tsconfig && existsSync(tsconfig))
          out.push({ tsconfig, root: project.root ?? '', name });
      }
      // `@angular/build:unit-test` infers its tsconfig; pick up the conventional one.
      const specConfig = path.posix.join(norm(project.root ?? ''), 'tsconfig.spec.json');
      if (existsSync(specConfig) && !out.some((t) => norm(t.tsconfig) === specConfig))
        out.push({ tsconfig: specConfig, root: project.root ?? '', name });
    }
  }
  if (out.length === 0) {
    for (const f of ['tsconfig.app.json', 'tsconfig.spec.json', 'tsconfig.json'])
      if (existsSync(f)) out.push({ tsconfig: f, root: '', name: 'root' });
  }
  return out;
}

let targets = discoverTargets();

const forIndex = process.argv.indexOf('--for');
if (forIndex !== -1 && process.argv[forIndex + 1]) {
  const file = norm(path.relative(process.cwd(), path.resolve(process.argv[forIndex + 1])));
  const owning = targets.filter((t) => t.root && file.startsWith(norm(t.root) + '/'));
  if (owning.length) targets = owning;
}

if (targets.length === 0) {
  console.error('typecheck: no tsconfig found');
  process.exit(1);
}

// ngc ignores NO_COLOR, so strip the escape codes rather than ship them to the model.
const ANSI = new RegExp(String.fromCharCode(27) + '[[]' + '[0-9;]*[A-Za-z]', 'g');
const stripAnsi = (s) => s.replace(ANSI, '');

let failed = 0;
const seen = new Set(); // app and spec tsconfigs overlap -- report each error once

for (const t of targets) {
  // ngc accepts tsc's CLI surface; --noEmit keeps it a check, not a build.
  const res = spawnSync(`${compiler} -p ${t.tsconfig} --noEmit`, {
    shell: true,
    encoding: 'utf8',
    env,
    maxBuffer: 32 * 1024 * 1024,
  });
  if (res.status === 0) continue;
  failed++;
  const fresh = stripAnsi(`${res.stdout ?? ''}\n${res.stderr ?? ''}`)
    .split(/\r?\n/)
    .filter((line) => line.trim() !== '' && !seen.has(line) && seen.add(line));
  if (fresh.length) process.stdout.write(`[${t.tsconfig}]\n${fresh.join('\n')}\n`);
}

if (failed) process.exit(1);
if (!hasNgc)
  console.log('typecheck ok (tsc only -- templates unchecked, @angular/compiler-cli missing)');
