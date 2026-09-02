import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ConfirmDialog, ConfirmDialogData } from './confirm-dialog';

describe('ConfirmDialog', () => {
    let dialogRefStub: { close: ReturnType<typeof vi.fn> };
    const data: ConfirmDialogData = { title: 'Delete document', message: 'Are you sure?' };

    beforeEach(async () => {
        dialogRefStub = { close: vi.fn() };

        await TestBed.configureTestingModule({
            imports: [ConfirmDialog],
            providers: [
                { provide: MAT_DIALOG_DATA, useValue: data },
                { provide: MatDialogRef, useValue: dialogRefStub },
            ],
        }).compileComponents();
    });

    it('renders the title and message', () => {
        const fixture = TestBed.createComponent(ConfirmDialog);
        fixture.detectChanges();

        const text = fixture.nativeElement.textContent as string;
        expect(text).toContain('Delete document');
        expect(text).toContain('Are you sure?');
    });

    it('onConfirm closes the dialog with true', () => {
        const fixture = TestBed.createComponent(ConfirmDialog);
        fixture.componentInstance.onConfirm();

        expect(dialogRefStub.close).toHaveBeenCalledWith(true);
    });

    it('onCancel closes the dialog with false', () => {
        const fixture = TestBed.createComponent(ConfirmDialog);
        fixture.componentInstance.onCancel();

        expect(dialogRefStub.close).toHaveBeenCalledWith(false);
    });
});
