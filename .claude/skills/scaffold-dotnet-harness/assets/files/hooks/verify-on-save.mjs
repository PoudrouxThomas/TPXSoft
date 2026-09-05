#!/usr/bin/env node
/**
 * PostToolUse hook: build only the project that owns the file just edited.
 *
 * Narrow on purpose. The full loop runs on Stop; this one exists so a compile error
 * comes back within seconds of the edit that caused it, while the agent still has the
 * context to fix it cheaply. Building the whole solution here would turn every edit
 * into a multi-second tax and the agent would start batching edits to avoid it.
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { readHookInput, protectedPaths, isProtected } from './paths.mjs';
import { diagnostics } from '../diagnostics.mjs';

const input = readHookInput();
const file = input.tool_input?.file_path ?? '';

if (!/\.(cs|csproj|props|targets)$/i.test(file)) process.exit(0);
if (isProtected(file, protectedPaths())) process.exit(0);
if (!fs.existsSync(file)) process.exit(0);

/** Walk up from the edited file to the .csproj that owns it. */
function ownerProject(from) {
  let dir = path.dirname(path.resolve(from));
  const root = path.parse(dir).root;
  while (dir !== root) {
    const hit = fs.readdirSync(dir).find((f) => f.toLowerCase().endsWith('.csproj'));
    if (hit) return path.join(dir, hit);
    dir = path.dirname(dir);
  }
  return null;
}

const project = ownerProject(file);
if (!project) process.exit(0);

const res = spawnSync('dotnet build "' + project + '" --nologo -v quiet --no-restore', {
  shell: true,
  encoding: 'utf8',
  maxBuffer: 32 * 1024 * 1024,
  env: { ...process.env, DOTNET_CLI_UI_LANGUAGE: 'en', VSLANG: '1033', DOTNET_NOLOGO: '1' },
});

if (res.status === 0) process.exit(0);

console.error(
  path.basename(project) + ' does not compile after editing ' + file + ':\n' +
    diagnostics(res, 6) +
    '\nWarnings are errors here by design. If this is a half-finished multi-file change, ' +
    'keep going -- the Stop hook is the real gate.',
);
process.exit(2);
