#!/usr/bin/env node
/**
 * PreToolUse hook: refuse writes to generated API clients.
 *
 * Matching Edit/Write alone is not enough. `sed -i`, a heredoc, `cp`, or a one-line
 * python script all walk straight past an Edit-only guard, so Bash is matched too
 * and treated as deny-by-default: a command that touches a protected path is blocked
 * unless every one of its segments is a known read-only command.
 */
import { readHookInput, protectedPaths, isProtected, block } from './paths.mjs';

const READ_ONLY = new Set([
  'cat',
  'bat',
  'head',
  'tail',
  'less',
  'more',
  'grep',
  'rg',
  'ag',
  'ls',
  'dir',
  'find',
  'fd',
  'wc',
  'diff',
  'stat',
  'file',
  'tree',
  'echo',
  'sort',
  'uniq',
  'cut',
  'awk',
  'jq',
  'node',
  'npm',
  'npx',
  'pnpm',
  'yarn',
  'git',
  'type',
  'which',
  'where',
]);

// Commands that are read-only in general but not with these subcommands/flags.
const WRITE_SUBCOMMANDS = /\b(checkout|restore|apply|am|rm|mv|clean|reset)\b/;

const input = readHookInput();
const tool = input.tool_name ?? '';
const args = input.tool_input ?? {};
const list = protectedPaths();

const refusal = (target) =>
  `Blocked: ${target} is generated code.\n` +
  'Edit the OpenAPI contract and run `npm run gen:api` instead -- a hand edit here is ' +
  'erased by the next generation and hides a real contract drift in the meantime.';

if (['Edit', 'Write', 'MultiEdit', 'NotebookEdit'].includes(tool)) {
  const target = args.file_path ?? args.notebook_path ?? '';
  if (isProtected(target, list)) block(refusal(target));
  process.exit(0);
}

if (tool === 'Bash') {
  const command = String(args.command ?? '');
  const segments = command.split(/&&|\|\||;|\n|\|/g);
  for (const segment of segments) {
    const trimmed = segment.trim();
    if (!trimmed) continue;
    const mentions =
      list.some((p) => trimmed.replace(/\\/g, '/').includes(p)) ||
      /(^|[\s"'/])generated([\s"'/]|$)/.test(trimmed.replace(/\\/g, '/'));
    if (!mentions) continue;
    const head = (trimmed.match(/^[\w./-]+/) ?? [''])[0].split('/').pop();
    const readOnly =
      READ_ONLY.has(head) && !WRITE_SUBCOMMANDS.test(trimmed) && !/>|>>/.test(segment);
    if (!readOnly) block(refusal(trimmed.slice(0, 120)));
  }
}

process.exit(0);
