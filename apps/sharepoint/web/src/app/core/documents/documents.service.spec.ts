import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideApiConfiguration } from '@tpxsoft/documents-client/api-configuration';
import { Document, Folder } from '@tpxsoft/documents-client';
import { DocumentsService } from './documents.service';

const ROOT_URL = 'http://localhost:5082';

/** See auth.service.spec.ts: the generated client bridges Observables to Promises, so tests
 * need a macrotask boundary between "flush a mocked response" and "expect the next request". */
function flushPromises(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
}

function folder(overrides: Partial<Folder> = {}): Folder {
    return {
        id: 'folder-1',
        ownerUserId: 'user-1',
        parentFolderId: null,
        name: 'Reports',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        ...overrides,
    };
}

function document(overrides: Partial<Document> = {}): Document {
    return {
        id: 'doc-1',
        ownerUserId: 'user-1',
        folderId: null,
        fileName: 'report.pdf',
        contentType: 'application/pdf',
        sizeBytes: 1024,
        visibility: 'Private',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        ...overrides,
    };
}

describe('DocumentsService', () => {
    let httpMock: HttpTestingController;

    function setup(): DocumentsService {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                provideApiConfiguration(ROOT_URL),
            ],
        });
        httpMock = TestBed.inject(HttpTestingController);
        return TestBed.inject(DocumentsService);
    }

    afterEach(() => {
        httpMock.verify();
    });

    it('listChildren(null) fetches folders and documents and keeps only root-level rows', async () => {
        const service = setup();

        const promise = service.listChildren(null);
        await flushPromises();

        const foldersReq = httpMock.expectOne(`${ROOT_URL}/folders`);
        foldersReq.flush([
            folder({ id: 'root-folder' }),
            folder({ id: 'nested', parentFolderId: 'root-folder' }),
        ]);

        const documentsReq = httpMock.expectOne(`${ROOT_URL}/documents`);
        documentsReq.flush([
            document({ id: 'root-doc' }),
            document({ id: 'nested-doc', folderId: 'root-folder' }),
        ]);

        const result = await promise;

        expect(result.folders.map((f) => f.id)).toEqual(['root-folder']);
        expect(result.documents.map((d) => d.id)).toEqual(['root-doc']);
    });

    it('listChildren(folderId) calls the dedicated children endpoint', async () => {
        const service = setup();

        const promise = service.listChildren('root-folder');
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/folders/root-folder/children`);
        expect(req.request.method).toBe('GET');
        req.flush({ folders: [folder()], documents: [document()] });

        const result = await promise;
        expect(result.folders).toHaveLength(1);
        expect(result.documents).toHaveLength(1);
    });

    it('listFoldersIn(null) filters to root folders', async () => {
        const service = setup();

        const promise = service.listFoldersIn(null);
        await flushPromises();

        httpMock
            .expectOne(`${ROOT_URL}/folders`)
            .flush([
                folder({ id: 'root-folder' }),
                folder({ id: 'nested', parentFolderId: 'root-folder' }),
            ]);

        const result = await promise;
        expect(result.map((f) => f.id)).toEqual(['root-folder']);
    });

    it('listFoldersIn(parentId) returns only the folders part of the children response', async () => {
        const service = setup();

        const promise = service.listFoldersIn('root-folder');
        await flushPromises();

        httpMock
            .expectOne(`${ROOT_URL}/folders/root-folder/children`)
            .flush({ folders: [folder({ id: 'child' })], documents: [document()] });

        const result = await promise;
        expect(result.map((f) => f.id)).toEqual(['child']);
    });

    it('createFolder posts name and parentFolderId', async () => {
        const service = setup();

        const promise = service.createFolder('Q3 Reports', 'root-folder');
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/folders`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual({ name: 'Q3 Reports', parentFolderId: 'root-folder' });
        req.flush(folder({ id: 'new-folder', name: 'Q3 Reports', parentFolderId: 'root-folder' }));

        const result = await promise;
        expect(result.id).toBe('new-folder');
    });

    it('deleteFolder issues a DELETE to /folders/{id}', async () => {
        const service = setup();

        const promise = service.deleteFolder('folder-1');
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/folders/folder-1`);
        expect(req.request.method).toBe('DELETE');
        req.flush(null, { status: 204, statusText: 'No Content' });

        await expect(promise).resolves.toBeNull();
    });

    it('deleteDocument issues a DELETE to /documents/{id}', async () => {
        const service = setup();

        const promise = service.deleteDocument('doc-1');
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/documents/doc-1`);
        expect(req.request.method).toBe('DELETE');
        req.flush(null, { status: 204, statusText: 'No Content' });

        await expect(promise).resolves.toBeNull();
    });

    it('moveDocument patches folderId', async () => {
        const service = setup();

        const promise = service.moveDocument('doc-1', 'target-folder');
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/documents/doc-1`);
        expect(req.request.method).toBe('PATCH');
        expect(req.request.body).toEqual({ folderId: 'target-folder' });
        req.flush(document({ id: 'doc-1', folderId: 'target-folder' }));

        const result = await promise;
        expect(result.folderId).toBe('target-folder');
    });

    it('moveDocument to root sends an explicit null folderId', async () => {
        const service = setup();

        const promise = service.moveDocument('doc-1', null);
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/documents/doc-1`);
        expect(req.request.body).toEqual({ folderId: null });
        req.flush(document({ id: 'doc-1', folderId: null }));

        await promise;
    });

    it('uploadDocument sends the file (with its own name) and folderId as multipart form data', async () => {
        const service = setup();
        const file = new File(['hello'], 'report_1.pdf', { type: 'application/pdf' });

        const promise = service.uploadDocument(file, 'root-folder');
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/documents`);
        expect(req.request.method).toBe('POST');
        const body = req.request.body as FormData;
        expect(body instanceof FormData).toBe(true);
        const sentFile = body.get('file') as File;
        expect(sentFile.name).toBe('report_1.pdf');
        expect(body.get('folderId')).toBe('root-folder');
        req.flush(document({ id: 'doc-2', fileName: 'report_1.pdf', folderId: 'root-folder' }));

        const result = await promise;
        expect(result.fileName).toBe('report_1.pdf');
    });

    it('downloadContent requests the content endpoint as a blob', async () => {
        const service = setup();

        const promise = service.downloadContent('doc-1');
        await flushPromises();

        const req = httpMock.expectOne(`${ROOT_URL}/documents/doc-1/content`);
        expect(req.request.method).toBe('GET');
        expect(req.request.responseType).toBe('blob');
        req.flush(new Blob(['bytes']));

        const result = await promise;
        expect(result instanceof Blob).toBe(true);
    });
});
