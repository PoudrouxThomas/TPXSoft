#!/usr/bin/env node
/**
 * The contract gate.
 *
 * The API is code-first: `dotnet build` emits the OpenAPI document from the real
 * endpoints. That emitted file is the truth, and `contracts/openapi.json` is the copy
 * the frontend generates its client from -- so the two drifting apart is the whole
 * failure this guards against. `check` fails when they differ; `accept` promotes the
 * emitted document and prints what the change does to consumers.
 *
 * The breaking-change classification is done here rather than by shelling out to
 * oasdiff so the gate has no download, no version pin and no network.
 *
 * Usage: node tools/harness/openapi.mjs check|accept|show
 */
import fs from 'node:fs';
import path from 'node:path';

const harness = JSON.parse(fs.readFileSync('package.json', 'utf8')).harness ?? {};
const committedPath = harness.openapi?.committed ?? 'contracts/openapi.json';

/**
 * The build names the file after the document when a project defines more than one, so
 * the configured name is a preference rather than a promise -- fall back to whatever
 * single document landed in the directory instead of failing on a rename.
 */
const emittedPath = (() => {
  const configured = harness.openapi?.emitted ?? 'artifacts/openapi/openapi.json';
  if (fs.existsSync(configured)) return configured;
  const dir = path.dirname(configured);
  if (!fs.existsSync(dir)) return configured;
  const found = fs.readdirSync(dir).filter((f) => f.toLowerCase().endsWith('.json'));
  return found.length === 1 ? path.join(dir, found[0]) : configured;
})();

const readJson = (file) => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null);

/** Stable serialisation: key order must never be the reason a diff appears. */
const stable = (value) => {
  if (Array.isArray(value)) return value.map(stable);
  if (value && typeof value === 'object')
    return Object.fromEntries(
      Object.keys(value)
        .sort()
        .map((k) => [k, stable(value[k])]),
    );
  return value;
};
const canonical = (spec) => JSON.stringify(stable(spec), null, 2) + '\n';

// ------------------------------------------------------------------ diffing

const METHODS = ['get', 'put', 'post', 'delete', 'patch', 'head', 'options', 'trace'];

const describe = (schema) => {
  if (!schema || typeof schema !== 'object') return 'unknown';
  if (schema.$ref) return String(schema.$ref).split('/').pop();
  if (schema.type === 'array') return describe(schema.items) + '[]';
  if (schema.type) return String(schema.type);
  return schema.oneOf || schema.anyOf || schema.allOf ? 'composed' : 'unknown';
};

const key = (p) => (p.in ?? 'query') + ':' + p.name;

function diffOperation(label, oldOp, newOp, breaking, additive) {
  const oldParams = new Map((oldOp.parameters ?? []).map((p) => [key(p), p]));
  const newParams = new Map((newOp.parameters ?? []).map((p) => [key(p), p]));

  for (const [id, param] of oldParams) {
    const next = newParams.get(id);
    if (!next) breaking.push(label + ': removed parameter ' + id);
    else if (!param.required && next.required)
      breaking.push(label + ': parameter ' + id + ' is now required');
  }
  for (const [id, param] of newParams)
    if (!oldParams.has(id))
      (param.required ? breaking : additive).push(
        label + ': added ' + (param.required ? 'required ' : '') + 'parameter ' + id,
      );

  if (oldOp.requestBody?.required === false && newOp.requestBody?.required === true)
    breaking.push(label + ': request body is now required');

  const oldCodes = Object.keys(oldOp.responses ?? {});
  const newCodes = new Set(Object.keys(newOp.responses ?? {}));
  for (const code of oldCodes)
    if (!newCodes.has(code) && /^2/.test(code))
      breaking.push(label + ': removed success response ' + code);
  for (const code of newCodes)
    if (!oldCodes.includes(code)) additive.push(label + ': added response ' + code);
}

function diffSchemas(before, after, breaking, additive) {
  for (const name of Object.keys(before)) {
    if (!(name in after)) {
      breaking.push('removed schema ' + name);
      continue;
    }
    const oldProps = before[name].properties ?? {};
    const newProps = after[name].properties ?? {};
    const oldRequired = new Set(before[name].required ?? []);
    const newRequired = new Set(after[name].required ?? []);

    for (const prop of Object.keys(oldProps)) {
      if (!(prop in newProps)) {
        breaking.push(name + '.' + prop + ' removed');
        continue;
      }
      const from = describe(oldProps[prop]);
      const to = describe(newProps[prop]);
      if (from !== to) breaking.push(name + '.' + prop + ' changed type ' + from + ' -> ' + to);
    }
    for (const prop of Object.keys(newProps))
      if (!(prop in oldProps))
        (newRequired.has(prop) ? breaking : additive).push(
          name + '.' + prop + ' added' + (newRequired.has(prop) ? ' (required)' : ''),
        );
    for (const prop of newRequired)
      if (!oldRequired.has(prop) && prop in oldProps)
        breaking.push(name + '.' + prop + ' became required');
  }
  for (const name of Object.keys(after))
    if (!(name in before)) additive.push('added schema ' + name);
}

function diff(before, after) {
  const breaking = [];
  const additive = [];
  const oldPaths = before.paths ?? {};
  const newPaths = after.paths ?? {};

  for (const route of Object.keys(oldPaths)) {
    if (!(route in newPaths)) {
      breaking.push('removed path ' + route);
      continue;
    }
    for (const method of METHODS) {
      const oldOp = oldPaths[route][method];
      const newOp = newPaths[route][method];
      if (oldOp && !newOp) {
        breaking.push('removed operation ' + method.toUpperCase() + ' ' + route);
        continue;
      }
      if (!oldOp || !newOp) continue;
      diffOperation(method.toUpperCase() + ' ' + route, oldOp, newOp, breaking, additive);
    }
  }
  for (const route of Object.keys(newPaths)) {
    if (!(route in oldPaths)) {
      additive.push('added path ' + route);
      continue;
    }
    for (const method of METHODS)
      if (newPaths[route][method] && !oldPaths[route][method])
        additive.push('added operation ' + method.toUpperCase() + ' ' + route);
  }

  diffSchemas(before.components?.schemas ?? {}, after.components?.schemas ?? {}, breaking, additive);
  return { breaking, additive };
}

// ------------------------------------------------------------------ commands

const command = process.argv[2] ?? 'check';
const emitted = readJson(emittedPath);
const committed = readJson(committedPath);

if (!emitted) {
  console.error(
    'No emitted OpenAPI document at ' +
      emittedPath +
      '.\nIt is written by the build, so run `npm run verify build` first. If it stays ' +
      'missing, see references/openapi-contract.md -- build-time emission is the one ' +
      'piece of this harness with a documented fallback.',
  );
  process.exit(1);
}

if (command === 'show') {
  process.stdout.write(canonical(emitted));
  process.exit(0);
}

const write = () => {
  fs.mkdirSync(path.dirname(committedPath), { recursive: true });
  fs.writeFileSync(committedPath, canonical(emitted));
};

if (!committed) {
  if (command === 'accept') {
    write();
    console.log('wrote ' + committedPath + ' (first contract)');
    process.exit(0);
  }
  console.error(committedPath + ' does not exist yet. Run `npm run openapi:accept`.');
  process.exit(1);
}

const same = canonical(committed) === canonical(emitted);

if (command === 'accept') {
  if (same) {
    console.log(committedPath + ' already matches the code.');
    process.exit(0);
  }
  const { breaking, additive } = diff(committed, emitted);
  write();
  console.log(
    'updated ' + committedPath + '  (' + breaking.length + ' breaking, ' + additive.length + ' additive)',
  );
  for (const item of breaking) console.log('  BREAKING  ' + item);
  for (const item of additive.slice(0, 10)) console.log('  +         ' + item);
  if (additive.length > 10) console.log('  +         ...and ' + (additive.length - 10) + ' more');
  if (breaking.length)
    console.log(
      '\nBreaking changes reach every generated client. Say so in the commit message and ' +
        'regenerate the consumers.',
    );
  process.exit(0);
}

if (same) process.exit(0);

const { breaking, additive } = diff(committed, emitted);
const show = (items, prefix, limit) =>
  items
    .slice(0, limit)
    .map((i) => '  ' + prefix + ' ' + i)
    .concat(items.length > limit ? ['  ' + prefix + ' ...and ' + (items.length - limit) + ' more'] : [])
    .join('\n');

console.error(
  'Contract drift: ' +
    committedPath +
    ' no longer matches the code.\n' +
    (breaking.length ? '\n' + show(breaking, 'BREAKING', 8) + '\n' : '') +
    (additive.length ? '\n' + show(additive, '+', 6) + '\n' : '') +
    '\nThe endpoints are the source of truth, so accept the emitted document:\n' +
    '  npm run openapi:accept\n' +
    'Then commit it with the code change -- the frontend generates its client from that file.',
);
process.exit(1);
