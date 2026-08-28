import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { User } from '@tpxsoft/auth-client';
import { Document, Folder } from '@tpxsoft/documents-client';
import { FileExplorer } from './file-explorer';
import { AuthService } from '../../../core/auth/auth.service';
import { DocumentsService } from '../../../core/documents/documents.service';

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

describe('FileExplorer', () => {
    const testUser: User = {
        id: 'user-1',
        email: 'jane@example.com',
        orgId: 'org-1',
        orgName: 'Acme',
        role: 'Admin',
    };

    let authServiceStub: {
        currentUser: ReturnType<typeof signal<User | null>>;
        logout: ReturnType<typeof vi.fn>;
    };
    let documentsServiceStub: {
        listChildren: ReturnType<typeof vi.fn>;
        listFoldersIn: ReturnType<typeof vi.fn>;
        createFolder: ReturnType<typeof vi.fn>;
        deleteFolder: ReturnType<typeof vi.fn>;
        deleteDocument: ReturnType<typeof vi.fn>;
        moveDocument: ReturnType<typeof vi.fn>;
        uploadDocument: ReturnType<typeof vi.fn>;
        downloadContent: ReturnType<typeof vi.fn>;
    };
    let dialogStub: { open: ReturnType<typeof vi.fn> };
    let router: Router;

    beforeEach(async () => {
        authServiceStub = { currentUser: signal(testUser), logout: vi.fn() };
        documentsServiceStub = {
            listChildren: vi.fn().mockResolvedValue({ folders: [], documents: [] }),
            listFoldersIn: vi.fn().mockResolvedValue([]),
            createFolder: vi.fn(),
            deleteFolder: vi.fn(),
            deleteDocument: vi.fn(),
            moveDocument: vi.fn(),
            uploadDocument: vi.fn(),
            downloadContent: vi.fn(),
        };
        dialogStub = { open: vi.fn() };

        await TestBed.configureTestingModule({
            imports: [FileExplorer],
            providers: [
                provideRouter([]),
                { provide: AuthService, useValue: authServiceStub },
                { provide: DocumentsService, useValue: documentsServiceStub },
                { provide: MatDialog, useValue: dialogStub },
            ],
        }).compileComponents();

        router = TestBed.inject(Router);
        vi.spyOn(router, 'navigate').mockResolvedValue(true);
    });

    it('loads the root folder on init and renders folders and documents', async () => {
        documentsServiceStub.listChildren.mockResolvedValue({
            folders: [folder({ id: 'f1', name: 'Engineering' })],
            documents: [document({ id: 'd1', fileName: 'notes.txt' })],
        });

        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();
        fixture.detectChanges();

        expect(documentsServiceStub.listChildren).toHaveBeenCalledWith(null);
        const text = fixture.nativeElement.textContent as string;
        expect(text).toContain('Engineering');
        expect(text).toContain('notes.txt');
    });

    it('shows an empty-folder message when there is nothing to list', async () => {
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();
        fixture.detectChanges();

        expect(fixture.nativeElement.textContent).toContain('This folder is empty.');
    });

    it('shows the server error message when loading fails', async () => {
        documentsServiceStub.listChildren.mockRejectedValue(
            new HttpErrorResponse({ error: { message: 'Boom' }, status: 500 }),
        );

        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();
        fixture.detectChanges();

        expect(fixture.nativeElement.textContent).toContain('Boom');
    });

    it('opening a folder requests its children and pushes a breadcrumb', async () => {
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        documentsServiceStub.listChildren.mockClear();
        documentsServiceStub.listChildren.mockResolvedValue({ folders: [], documents: [] });

        fixture.componentInstance.openFolder(folder({ id: 'f1', name: 'Engineering' }));
        await flushPromises();

        expect(documentsServiceStub.listChildren).toHaveBeenCalledWith('f1');
        expect(fixture.componentInstance.breadcrumbs()).toEqual([
            { id: 'f1', name: 'Engineering' },
        ]);
    });

    it('going back to root clears the breadcrumb trail', async () => {
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        fixture.componentInstance.openFolder(folder({ id: 'f1', name: 'Engineering' }));
        await flushPromises();

        fixture.componentInstance.goToRoot();
        await flushPromises();

        expect(fixture.componentInstance.breadcrumbs()).toEqual([]);
        expect(documentsServiceStub.listChildren).toHaveBeenLastCalledWith(null);
    });

    it('logout calls AuthService.logout and navigates to /login', async () => {
        authServiceStub.logout.mockResolvedValue(undefined);
        const fixture = TestBed.createComponent(FileExplorer);

        await fixture.componentInstance.onLogout();

        expect(authServiceStub.logout).toHaveBeenCalled();
        expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('uploading a file whose name already exists in the folder renames it before uploading', async () => {
        documentsServiceStub.listChildren.mockResolvedValue({
            folders: [],
            documents: [document({ id: 'existing', fileName: 'report.pdf' })],
        });
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        documentsServiceStub.uploadDocument.mockResolvedValue(
            document({ id: 'new', fileName: 'report_1.pdf' }),
        );

        const file = new File(['content'], 'report.pdf', { type: 'application/pdf' });
        const event = { target: { files: [file], value: '' } } as unknown as Event;
        await fixture.componentInstance.onFileInputChange(event);

        expect(documentsServiceStub.uploadDocument).toHaveBeenCalledTimes(1);
        const [uploadedFile, folderId] = documentsServiceStub.uploadDocument.mock.calls[0];
        expect((uploadedFile as File).name).toBe('report_1.pdf');
        expect(folderId).toBeNull();
    });

    it('uploading a file with a unique name uploads it unchanged', async () => {
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        documentsServiceStub.uploadDocument.mockResolvedValue(
            document({ id: 'new', fileName: 'unique.pdf' }),
        );

        const file = new File(['content'], 'unique.pdf', { type: 'application/pdf' });
        const event = { target: { files: [file], value: '' } } as unknown as Event;
        await fixture.componentInstance.onFileInputChange(event);

        const [uploadedFile] = documentsServiceStub.uploadDocument.mock.calls[0];
        expect(uploadedFile as File).toBe(file);
    });

    it('create folder: uses the dialog result to call createFolder and reloads', async () => {
        dialogStub.open.mockReturnValue({ afterClosed: () => of('New folder') });
        documentsServiceStub.createFolder.mockResolvedValue(
            folder({ id: 'new', name: 'New folder' }),
        );
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        await fixture.componentInstance.onCreateFolder();

        expect(documentsServiceStub.createFolder).toHaveBeenCalledWith('New folder', null);
    });

    it('create folder: does nothing when the dialog is cancelled', async () => {
        dialogStub.open.mockReturnValue({ afterClosed: () => of(undefined) });
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        await fixture.componentInstance.onCreateFolder();

        expect(documentsServiceStub.createFolder).not.toHaveBeenCalled();
    });

    it('delete document: removes it from the list on confirmation', async () => {
        documentsServiceStub.listChildren.mockResolvedValue({
            folders: [],
            documents: [document({ id: 'doc-1', fileName: 'report.pdf' })],
        });
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        dialogStub.open.mockReturnValue({ afterClosed: () => of(true) });
        documentsServiceStub.deleteDocument.mockResolvedValue(undefined);

        await fixture.componentInstance.deleteDocument(document({ id: 'doc-1' }));

        expect(documentsServiceStub.deleteDocument).toHaveBeenCalledWith('doc-1');
        expect(fixture.componentInstance.documents()).toEqual([]);
    });

    it('delete document: does nothing when not confirmed', async () => {
        dialogStub.open.mockReturnValue({ afterClosed: () => of(false) });
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        await fixture.componentInstance.deleteDocument(document({ id: 'doc-1' }));

        expect(documentsServiceStub.deleteDocument).not.toHaveBeenCalled();
    });

    it('delete folder: removes it from the list on confirmation', async () => {
        documentsServiceStub.listChildren.mockResolvedValue({
            folders: [folder({ id: 'folder-1', name: 'Old' })],
            documents: [],
        });
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        dialogStub.open.mockReturnValue({ afterClosed: () => of(true) });
        documentsServiceStub.deleteFolder.mockResolvedValue(undefined);

        await fixture.componentInstance.deleteFolder(folder({ id: 'folder-1' }));

        expect(documentsServiceStub.deleteFolder).toHaveBeenCalledWith('folder-1');
        expect(fixture.componentInstance.folders()).toEqual([]);
    });

    it('delete folder: shows the server error when the folder is not empty', async () => {
        documentsServiceStub.listChildren.mockResolvedValue({
            folders: [folder({ id: 'folder-1', name: 'Old' })],
            documents: [],
        });
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        dialogStub.open.mockReturnValue({ afterClosed: () => of(true) });
        documentsServiceStub.deleteFolder.mockRejectedValue(
            new HttpErrorResponse({ error: { message: 'Folder is not empty.' }, status: 409 }),
        );

        await fixture.componentInstance.deleteFolder(folder({ id: 'folder-1' }));

        expect(fixture.componentInstance.folders()).toHaveLength(1);
        expect(fixture.componentInstance.errorMessage()).toBe('Folder is not empty.');
    });

    it('move document: removes it from view when moved into another folder', async () => {
        documentsServiceStub.listChildren.mockResolvedValue({
            folders: [],
            documents: [document({ id: 'doc-1', fileName: 'report.pdf', folderId: null })],
        });
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        dialogStub.open.mockReturnValue({ afterClosed: () => of('target-folder') });
        documentsServiceStub.moveDocument.mockResolvedValue(
            document({ id: 'doc-1', fileName: 'report.pdf', folderId: 'target-folder' }),
        );

        await fixture.componentInstance.moveDocument(
            document({ id: 'doc-1', fileName: 'report.pdf' }),
        );

        expect(documentsServiceStub.moveDocument).toHaveBeenCalledWith('doc-1', 'target-folder');
        expect(fixture.componentInstance.documents()).toEqual([]);
    });

    it('move document: does nothing when the dialog is cancelled', async () => {
        const fixture = TestBed.createComponent(FileExplorer);
        fixture.detectChanges();
        await flushPromises();

        dialogStub.open.mockReturnValue({ afterClosed: () => of(undefined) });

        await fixture.componentInstance.moveDocument(document({ id: 'doc-1' }));

        expect(documentsServiceStub.moveDocument).not.toHaveBeenCalled();
    });
});
