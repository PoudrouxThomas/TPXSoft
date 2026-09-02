import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { CreateFolderDialog } from './create-folder-dialog';

describe('CreateFolderDialog', () => {
    let dialogRefStub: { close: ReturnType<typeof vi.fn> };

    beforeEach(async () => {
        dialogRefStub = { close: vi.fn() };

        await TestBed.configureTestingModule({
            imports: [CreateFolderDialog],
            providers: [{ provide: MatDialogRef, useValue: dialogRefStub }],
        }).compileComponents();
    });

    it('does not close when the name is empty', () => {
        const fixture = TestBed.createComponent(CreateFolderDialog);
        fixture.detectChanges();

        fixture.componentInstance.onSubmit();

        expect(dialogRefStub.close).not.toHaveBeenCalled();
        expect(fixture.componentInstance.form.controls.name.touched).toBe(true);
    });

    it('closes with the trimmed name on submit', () => {
        const fixture = TestBed.createComponent(CreateFolderDialog);
        fixture.detectChanges();

        fixture.componentInstance.form.controls.name.setValue('  Engineering  ');
        fixture.componentInstance.onSubmit();

        expect(dialogRefStub.close).toHaveBeenCalledWith('Engineering');
    });

    it('onCancel closes without a value', () => {
        const fixture = TestBed.createComponent(CreateFolderDialog);
        fixture.componentInstance.onCancel();

        expect(dialogRefStub.close).toHaveBeenCalledWith();
    });
});
