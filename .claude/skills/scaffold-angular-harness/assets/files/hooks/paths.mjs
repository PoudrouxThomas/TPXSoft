/**
 * Shared helpers for the harness hooks.
 *
 * The protected paths are the generated API client (and anything else listed in
 * package.json > harness.protectedPaths). Hand-editing generated code is how a
 * contract silently stops being the source of truth: the edit survives until the
 * next `npm run gen:api` wipes it, and nothing fails in between.
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
    const configured = pkg.harness?.protectedPaths ?? [];
    return configured.map((p) => normalize(p).replace(/\/+$/, ''));
  } catch {
    return [];
  }
}

/** True when `target` is inside one of the protected directories. */
export function isProtected(target, list) {
  if (!target) return false;
  let rel = normalize(target);
  if (path.isAbsolute(rel)) rel = normalize(path.relative(process.cwd(), rel));
  const hay = process.platform === 'win32' ? rel.toLowerCase() : rel;
  // A path segment literally called `generated` is protected even when unlisted --
  // a new generated directory should be safe by default, not by remembering to opt in.
  if (/(^|\/)generated(\/|$)/.test(hay)) return true;
  return list.some((p) => {
    const needle = process.platform === 'win32' ? p.toLowerCase() : p;
    return hay === needle || hay.startsWith(`${needle}/`);
  });
}

export function block(message) {
  console.error(message);
  process.exit(2); // exit 2 is the code the agent is shown and must act on
}
