import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { Folder } from '@tpxsoft/documents-client';
import { MoveDocumentDialog, MoveDocumentDialogData } from './move-document-dialog';
import { DocumentsService } from '../../../core/documents/documents.service';

function flushPromises(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
}

function folder(overrides: Partial<Folder> = {}): Folder {
    return {
        id: 'folder-1',
        ownerUserId: 'user-1',
        parentFolderId: null,
        name: 'Archive',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        ...overrides,
    };
}

describe('MoveDocumentDialog', () => {
    let dialogRefStub: { close: ReturnType<typeof vi.fn> };
    let documentsServiceStub: { listFoldersIn: ReturnType<typeof vi.fn> };
    const data: MoveDocumentDialogData = { documentName: 'report.pdf' };

    beforeEach(async () => {
        dialogRefStub = { close: vi.fn() };
        documentsServiceStub = { listFoldersIn: vi.fn().mockResolvedValue([]) };

        await TestBed.configureTestingModule({
            imports: [MoveDocumentDialog],
            providers: [
                { provide: MAT_DIALOG_DATA, useValue: data },
                { provide: MatDialogRef, useValue: dialogRefStub },
                { provide: DocumentsService, useValue: documentsServiceStub },
            ],
        }).compileComponents();
    });

    it('loads root folders on construction', async () => {
        documentsServiceStub.listFoldersIn.mockResolvedValue([folder({ id: 'f1' })]);
        const fixture = TestBed.createComponent(MoveDocumentDialog);
        fixture.detectChanges();
        await flushPromises();

        expect(documentsServiceStub.listFoldersIn).toHaveBeenCalledWith(null);
        expect(fixture.componentInstance.folders()).toHaveLength(1);
    });

    it('opening a folder fetches its children and updates the breadcrumb', async () => {
        const fixture = TestBed.createComponent(MoveDocumentDialog);
        fixture.detectChanges();
        await flushPromises();

        documentsServiceStub.listFoldersIn.mockResolvedValue([
            folder({ id: 'f2', name: 'Nested' }),
        ]);
        fixture.componentInstance.openFolder(folder({ id: 'f1', name: 'Archive' }));
        await flushPromises();

        expect(documentsServiceStub.listFoldersIn).toHaveBeenCalledWith('f1');
        expect(fixture.componentInstance.breadcrumbs()).toEqual([{ id: 'f1', name: 'Archive' }]);
    });

    it('goToRoot resets the breadcrumb and reloads root folders', async () => {
        const fixture = TestBed.createComponent(MoveDocumentDialog);
        fixture.detectChanges();
        await flushPromises();

        fixture.componentInstance.openFolder(folder({ id: 'f1', name: 'Archive' }));
        await flushPromises();

        documentsServiceStub.listFoldersIn.mockClear();
        fixture.componentInstance.goToRoot();
        await flushPromises();

        expect(fixture.componentInstance.breadcrumbs()).toEqual([]);
        expect(documentsServiceStub.listFoldersIn).toHaveBeenCalledWith(null);
    });

    it('onMoveHere closes the dialog with the current folder id', async () => {
        const fixture = TestBed.createComponent(MoveDocumentDialog);
        fixture.detectChanges();
        await flushPromises();

        fixture.componentInstance.openFolder(folder({ id: 'f1', name: 'Archive' }));
        await flushPromises();

        fixture.componentInstance.onMoveHere();

        expect(dialogRefStub.close).toHaveBeenCalledWith('f1');
    });

    it('onMoveHere at root closes with null', async () => {
        const fixture = TestBed.createComponent(MoveDocumentDialog);
        fixture.detectChanges();
        await flushPromises();

        fixture.componentInstance.onMoveHere();

        expect(dialogRefStub.close).toHaveBeenCalledWith(null);
    });

    it('onCancel closes without a value', () => {
        const fixture = TestBed.createComponent(MoveDocumentDialog);

        fixture.componentInstance.onCancel();

        expect(dialogRefStub.close).toHaveBeenCalledWith();
    });

    it('shows an error message when loading folders fails', async () => {
        documentsServiceStub.listFoldersIn.mockRejectedValue(
            new HttpErrorResponse({ error: { message: 'Boom' }, status: 500 }),
        );
        const fixture = TestBed.createComponent(MoveDocumentDialog);
        fixture.detectChanges();
        await flushPromises();

        expect(fixture.componentInstance.errorMessage()).toBe('Boom');
    });
});
