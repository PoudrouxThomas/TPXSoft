import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Folder } from '@tpxsoft/documents-client';
import { DocumentsService } from '../../../core/documents/documents.service';

export interface MoveDocumentDialogData {
    documentName: string;
}

interface Crumb {
    id: string;
    name: string;
}

@Component({
    selector: 'app-move-document-dialog',
    imports: [
        MatDialogModule,
        MatButtonModule,
        MatListModule,
        MatIconModule,
        MatProgressSpinnerModule,
    ],
    templateUrl: './move-document-dialog.html',
})
export class MoveDocumentDialog {
    protected readonly data = inject<MoveDocumentDialogData>(MAT_DIALOG_DATA);
    private readonly dialogRef = inject(MatDialogRef<MoveDocumentDialog, string | null>);
    private readonly documentsService = inject(DocumentsService);

    private readonly path = signal<Crumb[]>([]);
    readonly breadcrumbs = this.path.asReadonly();
    readonly folders = signal<Folder[]>([]);
    protected readonly loading = signal(false);
    readonly errorMessage = signal<string | null>(null);

    protected readonly currentFolderId = computed<string | null>(() => {
        const crumbs = this.path();
        return crumbs.length ? crumbs[crumbs.length - 1].id : null;
    });
    protected readonly currentFolderName = computed(() => {
        const crumbs = this.path();
        return crumbs.length ? crumbs[crumbs.length - 1].name : 'Root';
    });

    constructor() {
        void this.load(null);
    }

    private async load(folderId: string | null): Promise<void> {
        this.loading.set(true);
        this.errorMessage.set(null);
        try {
            this.folders.set(await this.documentsService.listFoldersIn(folderId));
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        } finally {
            this.loading.set(false);
        }
    }

    openFolder(folder: Folder): void {
        this.path.update((crumbs) => [...crumbs, { id: folder.id, name: folder.name }]);
        void this.load(folder.id);
    }

    goToRoot(): void {
        this.path.set([]);
        void this.load(null);
    }

    goToCrumb(index: number): void {
        this.path.update((crumbs) => crumbs.slice(0, index + 1));
        void this.load(this.path()[index].id);
    }

    onCancel(): void {
        this.dialogRef.close();
    }

    onMoveHere(): void {
        this.dialogRef.close(this.currentFolderId());
    }

    private toErrorMessage(e: unknown): string {
        return e instanceof HttpErrorResponse && e.error?.message
            ? e.error.message
            : 'Something went wrong.';
    }
}
