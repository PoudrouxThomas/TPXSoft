import { uniqueFileName } from './unique-file-name';

describe('uniqueFileName', () => {
    it('returns the name unchanged when there is no collision', () => {
        expect(uniqueFileName('report.pdf', new Set(['other.pdf']))).toBe('report.pdf');
    });

    it('appends _1 before the extension on a single collision', () => {
        expect(uniqueFileName('report.pdf', new Set(['report.pdf']))).toBe('report_1.pdf');
    });

    it('increments the suffix until a free name is found', () => {
        const existing = new Set(['report.pdf', 'report_1.pdf', 'report_2.pdf']);
        expect(uniqueFileName('report.pdf', existing)).toBe('report_3.pdf');
    });

    it('only strips the last extension segment', () => {
        expect(uniqueFileName('archive.tar.gz', new Set(['archive.tar.gz']))).toBe(
            'archive.tar_1.gz',
        );
    });

    it('handles a name with no extension', () => {
        expect(uniqueFileName('README', new Set(['README']))).toBe('README_1');
    });

    it('treats a leading dot as part of the base name, not an extension', () => {
        expect(uniqueFileName('.gitignore', new Set(['.gitignore']))).toBe('.gitignore_1');
    });
});
