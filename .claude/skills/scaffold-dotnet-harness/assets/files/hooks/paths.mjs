/**
 * Shared helpers for the harness hooks.
 *
 * Protected paths are the ones a human edit cannot survive: the committed OpenAPI
 * document (regenerated from the endpoints on every build), EF Core's generated
 * migration metadata, build output, and anything under a `generated` directory. An
 * edit there is erased by the next build and hides a real drift in the meantime,
 * which is exactly the kind of failure nobody notices until a client breaks.
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';

export function readHookInput() {
  let raw = '';
  try {
    raw = readFileSync(0, 'utf8');
  } catch {
    return {};
  }
  try {
    return JSON.parse(raw || '{}');
  } catch {
    return {};
  }
}

const normalize = (p) => String(p).split('\\').join('/').replace(/^\.\//, '');

export function protectedPaths() {
  try {
    const pkg = JSON.parse(readFileSync('package.json', 'utf8'));
    return (pkg.harness?.protectedPaths ?? []).map((p) => normalize(p).replace(/\/+$/, ''));
  } catch {
    return [];
  }
}

// Safe by default rather than by remembering to opt in: a new generated directory,
// a new project's bin/obj, or a fresh EF migration is protected the moment it exists.
const ALWAYS_TEXT = [/(^|\/)generated(\/|$)/, /\.Designer\.cs\b/i, /ModelSnapshot\.cs\b/i];
// bin/obj are only checked against real file paths. Matching them inside a command
// string would fire on `/bin/sh`, `node_modules/.bin/...` and `rm -rf obj`, and a guard
// that cries wolf on cleanup commands is a guard people switch off.
const ALWAYS_FILE = [...ALWAYS_TEXT, /(^|\/)(bin|obj)(\/|$)/];

export function isProtected(target, list) {
  if (!target) return false;
  let rel = normalize(target);
  if (path.isAbsolute(rel)) rel = normalize(path.relative(process.cwd(), rel));
  const hay = process.platform === 'win32' ? rel.toLowerCase() : rel;
  if (ALWAYS_FILE.some((re) => re.test(hay))) return true;
  return list.some((p) => {
    const needle = process.platform === 'win32' ? p.toLowerCase() : p;
    return hay === needle || hay.startsWith(needle + '/');
  });
}

/** True when a raw command string so much as mentions something protected. */
export function mentionsProtected(text, list) {
  const hay = normalize(text);
  const lowered = process.platform === 'win32' ? hay.toLowerCase() : hay;
  if (ALWAYS_TEXT.some((re) => re.test(lowered))) return true;
  return list.some((p) => lowered.includes(process.platform === 'win32' ? p.toLowerCase() : p));
}

export function block(message) {
  console.error(message);
  process.exit(2); // exit 2 is the code the agent is shown and must act on
}
