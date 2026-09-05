#!/usr/bin/env node
/**
 * PreToolUse hook: refuse hand-written changes to generated files.
 *
 * Matching Edit/Write alone is not enough. `sed -i`, a heredoc redirect, `cp`, or a
 * one-line python script all walk straight past an Edit-only guard, so Bash is matched
 * too and treated as deny-by-default: a command that touches a protected path is
 * blocked unless every segment of it is a known read-only command, or a generator that
 * is *supposed* to write there.
 */
import { readHookInput, protectedPaths, isProtected, mentionsProtected, block } from './paths.mjs';

const READ_ONLY = new Set([
  'cat', 'bat', 'head', 'tail', 'less', 'more', 'grep', 'rg', 'ag', 'ls', 'dir',
  'find', 'fd', 'wc', 'diff', 'stat', 'file', 'tree', 'echo', 'sort', 'uniq',
  'cut', 'awk', 'jq', 'type', 'which', 'where', 'git', 'npm', 'npx', 'node',
]);

// The generators themselves. `dotnet build` writes the emitted OpenAPI document and
// `dotnet ef migrations add` writes the Designer/snapshot files -- blocking those
// would block the only supported way to change them.
const GENERATORS = new Set(['dotnet']);

// Commands that read in general but write with these subcommands.
const WRITE_SUBCOMMANDS = /\b(checkout|restore|apply|am|rm|mv|clean|reset)\b/;

const input = readHookInput();
const tool = input.tool_name ?? '';
const args = input.tool_input ?? {};
const list = protectedPaths();

const refusal = (target) =>
  'Blocked: ' + target + ' is generated.\n' +
  'Change the C# that produces it and rebuild -- for the contract that is an endpoint ' +
  'or DTO change plus `npm run openapi:accept`, for a migration it is a model change ' +
  'plus `dotnet ef migrations add`. A hand edit here is erased by the next build and ' +
  'hides a real drift until then.';

if (['Edit', 'Write', 'MultiEdit', 'NotebookEdit'].includes(tool)) {
  const target = args.file_path ?? args.notebook_path ?? '';
  if (isProtected(target, list)) block(refusal(target));
  process.exit(0);
}

if (tool === 'Bash') {
  const command = String(args.command ?? '');
  for (const segment of command.split(/&&|\|\||;|\n|\|/g)) {
    const trimmed = segment.trim();
    if (!trimmed) continue;
    if (!mentionsProtected(trimmed, list)) continue;

    const head = (trimmed.match(/^[\w./-]+/) ?? [''])[0].split('/').pop();
    if (GENERATORS.has(head)) continue;

    const readOnly =
      READ_ONLY.has(head) && !WRITE_SUBCOMMANDS.test(trimmed) && !/>|>>/.test(segment);
    if (!readOnly) block(refusal(trimmed.slice(0, 120)));
  }
}

process.exit(0);
