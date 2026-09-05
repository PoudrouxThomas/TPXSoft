/**
 * Turning MSBuild output into something worth putting in a model's context.
 *
 * MSBuild repeats every diagnostic in its summary, prefixes each with an absolute path
 * and suffixes it with the project file and a documentation URL. Left alone, one broken
 * build is a few thousand tokens of the same sentence three times over -- which is the
 * single largest avoidable cost in a .NET agent loop.
 */
const here = process.cwd();

export const clean = (line) =>
  line
    .split(here + '\\')
    .join('')
    .split(here + '/')
    .join('')
    .replace(/ \(https:\/\/[^)]*\)/g, '')
    .replace(/ \[[^\]]*\.(csproj|slnf|sln)\]\s*$/, '')
    .trim();

export const outputLines = (result) =>
  `${result.stdout ?? ''}\n${result.stderr ?? ''}`.split(/\r?\n/).filter((l) => l.trim() !== '');

/** The compiler and analyser diagnostics, deduplicated, first few only. */
export function diagnostics(result, max = 8) {
  const seen = new Set();
  for (const line of outputLines(result)) {
    if (!/: (error|warning) [A-Za-z]+\d+/.test(line)) continue;
    seen.add(clean(line));
  }
  const all = [...seen];
  if (all.length === 0) return outputLines(result).slice(-max).map(clean).join('\n');
  const shown = all.slice(0, max);
  if (all.length > max) shown.push(`...and ${all.length - max} more`);
  return shown.join('\n');
}
