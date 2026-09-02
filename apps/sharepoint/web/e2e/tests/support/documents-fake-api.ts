import { Page, Route } from '@playwright/test';

interface FakeFolder {
    id: string;
    ownerUserId: string;
    parentFolderId: string | null;
    name: string;
    createdAt: string;
    updatedAt: string;
}

interface FakeDocument {
    id: string;
    ownerUserId: string;
    folderId: string | null;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    visibility: 'Private';
    createdAt: string;
    updatedAt: string;
}

let nextId = 1;
function generateId(prefix: string): string {
    return `${prefix}-${nextId++}`;
}

/** Minimal multipart/form-data parser -- good enough to recover the `file` part's filename and
 * the `folderId` text part from a real browser-built FormData request, without pulling in a
 * dependency just for the e2e suite. */
function parseMultipart(
    body: Buffer,
    contentType: string,
): { fileName?: string; folderId?: string } {
    const boundaryMatch = /boundary=(?:"([^"]+)"|([^;]+))/.exec(contentType);
    const boundary = boundaryMatch?.[1] ?? boundaryMatch?.[2];
    if (!boundary) {
        return {};
    }

    const text = body.toString('binary');
    const parts = text.split(`--${boundary}`).slice(1, -1);
    const result: { fileName?: string; folderId?: string } = {};

    for (const part of parts) {
        const trimmed = part.replace(/^\r\n/, '');
        const headerEnd = trimmed.indexOf('\r\n\r\n');
        if (headerEnd === -1) {
            continue;
        }
        const headers = trimmed.slice(0, headerEnd);
        const value = trimmed.slice(headerEnd + 4).replace(/\r\n$/, '');

        const nameMatch = /name="([^"]+)"/.exec(headers);
        const filenameMatch = /filename="([^"]*)"/.exec(headers);
        if (!nameMatch) {
            continue;
        }

        if (nameMatch[1] === 'file' && filenameMatch) {
            result.fileName = filenameMatch[1];
        } else if (nameMatch[1] === 'folderId') {
            result.folderId = value;
        }
    }

    return result;
}

/** A tiny in-memory stand-in for the Documents API, wired up through Playwright route
 * interception, so the file explorer's create/navigate/upload/preview/move/delete flows can be
 * exercised end-to-end (real HTTP requests out of the generated client) without a live backend. */
export class DocumentsFakeApi {
    readonly folders: FakeFolder[] = [];
    readonly documents: FakeDocument[] = [];

    constructor(private readonly rootUrl = 'http://localhost:5082') {}

    async install(page: Page): Promise<void> {
        await page.route(`${this.rootUrl}/folders`, (route) => this.handleFolders(route));
        await page.route(`${this.rootUrl}/folders/*`, (route) => this.handleFolderById(route));
        await page.route(`${this.rootUrl}/folders/*/children`, (route) =>
            this.handleFolderChildren(route),
        );
        await page.route(`${this.rootUrl}/documents`, (route) => this.handleDocuments(route));
        await page.route(`${this.rootUrl}/documents/*`, (route) => this.handleDocumentById(route));
        await page.route(`${this.rootUrl}/documents/*/content`, (route) =>
            this.handleDocumentContent(route),
        );
    }

    private idFromUrl(url: string, marker: string): string {
        const parts = new URL(url).pathname.split('/');
        return parts[parts.indexOf(marker) + 1];
    }

    private async handleFolders(route: Route): Promise<void> {
        const request = route.request();
        if (request.method() === 'GET') {
            const url = new URL(request.url());
            const parentFolderId = url.searchParams.get('parentFolderId');
            const results = parentFolderId
                ? this.folders.filter((f) => f.parentFolderId === parentFolderId)
                : this.folders;
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify(results),
            });
            return;
        }

        if (request.method() === 'POST') {
            const body = request.postDataJSON() as { name: string; parentFolderId?: string | null };
            const now = new Date().toISOString();
            const folder: FakeFolder = {
                id: generateId('folder'),
                ownerUserId: 'user-1',
                parentFolderId: body.parentFolderId ?? null,
                name: body.name,
                createdAt: now,
                updatedAt: now,
            };
            this.folders.push(folder);
            await route.fulfill({
                status: 201,
                contentType: 'application/json',
                body: JSON.stringify(folder),
            });
            return;
        }

        await route.fallback();
    }

    private async handleFolderById(route: Route): Promise<void> {
        const request = route.request();
        const id = this.idFromUrl(request.url(), 'folders');

        if (request.method() === 'DELETE') {
            const hasChildren =
                this.folders.some((f) => f.parentFolderId === id) ||
                this.documents.some((d) => d.folderId === id);
            if (hasChildren) {
                await route.fulfill({
                    status: 409,
                    contentType: 'application/json',
                    body: JSON.stringify({ message: 'Folder is not empty.' }),
                });
                return;
            }
            const index = this.folders.findIndex((f) => f.id === id);
            if (index !== -1) {
                this.folders.splice(index, 1);
            }
            await route.fulfill({ status: 204, body: '' });
            return;
        }

        if (request.method() === 'GET') {
            const folder = this.folders.find((f) => f.id === id);
            if (!folder) {
                await route.fulfill({
                    status: 404,
                    contentType: 'application/json',
                    body: JSON.stringify({ message: 'Not found.' }),
                });
                return;
            }
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify(folder),
            });
            return;
        }

        await route.fallback();
    }

    private async handleFolderChildren(route: Route): Promise<void> {
        const id = this.idFromUrl(route.request().url(), 'folders');
        const body = {
            folders: this.folders.filter((f) => f.parentFolderId === id),
            documents: this.documents.filter((d) => d.folderId === id),
        };
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(body),
        });
    }

    private async handleDocuments(route: Route): Promise<void> {
        const request = route.request();
        if (request.method() === 'GET') {
            const url = new URL(request.url());
            const folderId = url.searchParams.get('folderId');
            const results = folderId
                ? this.documents.filter((d) => d.folderId === folderId)
                : this.documents;
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify(results),
            });
            return;
        }

        if (request.method() === 'POST') {
            const contentType = request.headers()['content-type'] ?? '';
            const { fileName, folderId } = parseMultipart(
                request.postDataBuffer() ?? Buffer.alloc(0),
                contentType,
            );
            const now = new Date().toISOString();
            const document: FakeDocument = {
                id: generateId('doc'),
                ownerUserId: 'user-1',
                folderId: folderId || null,
                fileName: fileName ?? 'unnamed',
                contentType: 'text/plain',
                sizeBytes: 5,
                visibility: 'Private',
                createdAt: now,
                updatedAt: now,
            };
            this.documents.push(document);
            await route.fulfill({
                status: 201,
                contentType: 'application/json',
                body: JSON.stringify(document),
            });
            return;
        }

        await route.fallback();
    }

    private async handleDocumentById(route: Route): Promise<void> {
        const request = route.request();
        const id = this.idFromUrl(request.url(), 'documents');

        if (request.method() === 'DELETE') {
            const index = this.documents.findIndex((d) => d.id === id);
            if (index !== -1) {
                this.documents.splice(index, 1);
            }
            await route.fulfill({ status: 204, body: '' });
            return;
        }

        if (request.method() === 'PATCH') {
            const document = this.documents.find((d) => d.id === id);
            if (!document) {
                await route.fulfill({
                    status: 404,
                    contentType: 'application/json',
                    body: JSON.stringify({ message: 'Not found.' }),
                });
                return;
            }
            const patch = request.postDataJSON() as { fileName?: string; folderId?: string | null };
            if (patch.fileName !== undefined) {
                document.fileName = patch.fileName;
            }
            if ('folderId' in patch) {
                document.folderId = patch.folderId ?? null;
            }
            document.updatedAt = new Date().toISOString();
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify(document),
            });
            return;
        }

        await route.fallback();
    }

    private async handleDocumentContent(route: Route): Promise<void> {
        await route.fulfill({ status: 200, contentType: 'text/plain', body: 'file contents' });
    }
}
