import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { Document } from '@tpxsoft/documents-client';
import { PreviewDialog, PreviewDialogData } from './preview-dialog';
import { DocumentsService } from '../../../core/documents/documents.service';

function flushPromises(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
}

function testDocument(overrides: Partial<Document> = {}): Document {
    return {
        id: 'doc-1',
        ownerUserId: 'user-1',
        folderId: null,
        fileName: 'photo.png',
        contentType: 'image/png',
        sizeBytes: 10,
        visibility: 'Private',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        ...overrides,
    };
}

describe('PreviewDialog', () => {
    let documentsServiceStub: { downloadContent: ReturnType<typeof vi.fn> };

    beforeEach(() => {
        URL.createObjectURL = vi.fn(() => 'blob:mock-url');
        URL.revokeObjectURL = vi.fn();
        documentsServiceStub = { downloadContent: vi.fn().mockResolvedValue(new Blob(['bytes'])) };
    });

    function setup(data: PreviewDialogData) {
        TestBed.configureTestingModule({
            imports: [PreviewDialog],
            providers: [
                { provide: MAT_DIALOG_DATA, useValue: data },
                { provide: DocumentsService, useValue: documentsServiceStub },
            ],
        });
        return TestBed.createComponent(PreviewDialog);
    }

    it('loads and shows an image preview for an image content type', async () => {
        const fixture = setup({ document: testDocument({ contentType: 'image/png' }) });
        fixture.detectChanges();
        await flushPromises();
        fixture.detectChanges();

        expect(documentsServiceStub.downloadContent).toHaveBeenCalledWith('doc-1');
        expect(fixture.componentInstance.previewUrl()).not.toBeNull();
        expect(fixture.nativeElement.querySelector('img')).not.toBeNull();
    });

    it('embeds pdf/text/video/audio content in an iframe', async () => {
        const fixture = setup({ document: testDocument({ contentType: 'application/pdf' }) });
        fixture.detectChanges();
        await flushPromises();
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('iframe')).not.toBeNull();
    });

    it('shows a fallback message for an unsupported content type and does not create an object URL', async () => {
        const fixture = setup({ document: testDocument({ contentType: 'application/zip' }) });
        fixture.detectChanges();
        await flushPromises();
        fixture.detectChanges();

        expect(fixture.nativeElement.textContent).toContain('No preview available');
        expect(URL.createObjectURL).not.toHaveBeenCalled();
    });

    it('shows the server error message when the download fails', async () => {
        documentsServiceStub.downloadContent.mockRejectedValue(
            new HttpErrorResponse({ error: { message: 'Boom' }, status: 500 }),
        );
        const fixture = setup({ document: testDocument() });
        fixture.detectChanges();
        await flushPromises();
        fixture.detectChanges();

        expect(fixture.nativeElement.textContent).toContain('Boom');
    });

    it('ngOnDestroy revokes the created object URL', async () => {
        const fixture = setup({ document: testDocument({ contentType: 'image/png' }) });
        fixture.detectChanges();
        await flushPromises();

        fixture.destroy();

        expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:mock-url');
    });

    it('downloadInstead fetches the content again and triggers a save', async () => {
        const fixture = setup({ document: testDocument({ contentType: 'application/zip' }) });
        fixture.detectChanges();
        await flushPromises();
        documentsServiceStub.downloadContent.mockClear();

        const realCreateElement = window.document.createElement.bind(window.document);
        const clickSpy = vi.fn();
        const createElementSpy = vi
            .spyOn(window.document, 'createElement')
            .mockImplementation((tag: string) => {
                if (tag === 'a') {
                    return {
                        click: clickSpy,
                        set href(_: string) {},
                        set download(_: string) {},
                    } as unknown as HTMLAnchorElement;
                }
                return realCreateElement(tag);
            });

        await fixture.componentInstance.downloadInstead();

        expect(documentsServiceStub.downloadContent).toHaveBeenCalledWith('doc-1');
        expect(clickSpy).toHaveBeenCalled();

        createElementSpy.mockRestore();
    });
});
