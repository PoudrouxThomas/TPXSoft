/**
 * Resolves a name collision the way the file explorer's upload flow needs to: if `desiredName`
 * is not already taken, it is returned unchanged; otherwise `_1`, `_2`, ... is appended before
 * the extension until a free name is found. The extension is only the last `.`-delimited segment
 * (matching `FileNameSanitizer.TruncatePreservingExtension` on the Documents API), so
 * `archive.tar.gz` keeps `.gz` and grows a `_1` before it, not before `.tar.gz`.
 */
export function uniqueFileName(desiredName: string, existingNames: ReadonlySet<string>): string {
    if (!existingNames.has(desiredName)) {
        return desiredName;
    }

    const dotIndex = desiredName.lastIndexOf('.');
    const hasExtension = dotIndex > 0;
    const base = hasExtension ? desiredName.slice(0, dotIndex) : desiredName;
    const extension = hasExtension ? desiredName.slice(dotIndex) : '';

    let suffix = 1;
    let candidate = `${base}_${suffix}${extension}`;
    while (existingNames.has(candidate)) {
        suffix += 1;
        candidate = `${base}_${suffix}${extension}`;
    }
    return candidate;
}
