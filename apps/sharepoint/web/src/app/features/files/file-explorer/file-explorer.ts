import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { Document, Folder } from '@tpxsoft/documents-client';
import { AuthService } from '../../../core/auth/auth.service';
import { DocumentsService } from '../../../core/documents/documents.service';
import { uniqueFileName } from '../../../core/documents/unique-file-name';
import { CreateFolderDialog } from '../create-folder-dialog/create-folder-dialog';
import { ConfirmDialog, ConfirmDialogData } from '../confirm-dialog/confirm-dialog';
import {
    MoveDocumentDialog,
    MoveDocumentDialogData,
} from '../move-document-dialog/move-document-dialog';
import { PreviewDialog, PreviewDialogData } from '../preview-dialog/preview-dialog';

interface Crumb {
    id: string;
    name: string;
}

@Component({
    selector: 'app-file-explorer',
    imports: [
        MatToolbarModule,
        MatButtonModule,
        MatIconModule,
        MatMenuModule,
        MatProgressSpinnerModule,
    ],
    templateUrl: './file-explorer.html',
    styleUrl: './file-explorer.scss',
})
export class FileExplorer implements OnInit {
    private readonly router = inject(Router);
    private readonly dialog = inject(MatDialog);
    private readonly documentsService = inject(DocumentsService);
    protected readonly authService = inject(AuthService);

    @ViewChild('fileInput') private fileInput?: ElementRef<HTMLInputElement>;

    private readonly path = signal<Crumb[]>([]);
    /** Public (not protected) so tests can assert on it directly, same as folders/documents/
     * errorMessage below -- the template only ever needs protected-or-wider anyway. */
    readonly breadcrumbs = this.path.asReadonly();
    private readonly currentFolderId = computed<string | null>(() => {
        const crumbs = this.path();
        return crumbs.length ? crumbs[crumbs.length - 1].id : null;
    });

    readonly folders = signal<Folder[]>([]);
    readonly documents = signal<Document[]>([]);
    protected readonly loading = signal(false);
    protected readonly uploading = signal(false);
    readonly errorMessage = signal<string | null>(null);
    protected readonly isEmpty = computed(
        () => this.folders().length === 0 && this.documents().length === 0,
    );

    ngOnInit(): void {
        void this.load();
    }

    private async load(): Promise<void> {
        this.loading.set(true);
        this.errorMessage.set(null);
        try {
            const children = await this.documentsService.listChildren(this.currentFolderId());
            this.folders.set([...children.folders].sort((a, b) => a.name.localeCompare(b.name)));
            this.documents.set(
                [...children.documents].sort((a, b) => a.fileName.localeCompare(b.fileName)),
            );
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        } finally {
            this.loading.set(false);
        }
    }

    openFolder(folder: Folder): void {
        this.path.update((crumbs) => [...crumbs, { id: folder.id, name: folder.name }]);
        void this.load();
    }

    goToRoot(): void {
        this.path.set([]);
        void this.load();
    }

    goToCrumb(index: number): void {
        this.path.update((crumbs) => crumbs.slice(0, index + 1));
        void this.load();
    }

    async onLogout(): Promise<void> {
        await this.authService.logout();
        await this.router.navigate(['/login']);
    }

    async onCreateFolder(): Promise<void> {
        const dialogRef = this.dialog.open<CreateFolderDialog, unknown, string>(CreateFolderDialog);
        const name = await firstValueFrom(dialogRef.afterClosed());
        if (!name) {
            return;
        }

        this.errorMessage.set(null);
        try {
            await this.documentsService.createFolder(name, this.currentFolderId());
            await this.load();
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        }
    }

    triggerUpload(): void {
        this.fileInput?.nativeElement.click();
    }

    async onFileInputChange(event: Event): Promise<void> {
        const input = event.target as HTMLInputElement;
        const files = input.files;
        // Reset after the upload settles, not synchronously in this handler -- clearing
        // `.value` while the browser is still committing the file selection (as automation
        // tools like Playwright's setInputFiles do) can race and drop the just-selected files.
        await this.uploadFiles(files);
        input.value = '';
    }

    private async uploadFiles(files: FileList | null): Promise<void> {
        if (!files || files.length === 0) {
            return;
        }

        this.uploading.set(true);
        this.errorMessage.set(null);
        const usedNames = new Set(this.documents().map((d) => d.fileName));
        const folderId = this.currentFolderId();
        const uploaded: Document[] = [];

        try {
            for (const file of Array.from(files)) {
                const name = uniqueFileName(file.name, usedNames);
                usedNames.add(name);
                const toUpload =
                    name === file.name ? file : new File([file], name, { type: file.type });
                uploaded.push(await this.documentsService.uploadDocument(toUpload, folderId));
            }
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        } finally {
            if (uploaded.length > 0) {
                this.documents.update((docs) =>
                    [...docs, ...uploaded].sort((a, b) => a.fileName.localeCompare(b.fileName)),
                );
            }
            this.uploading.set(false);
        }
    }

    previewDocument(doc: Document): void {
        this.dialog.open<PreviewDialog, PreviewDialogData>(PreviewDialog, {
            data: { document: doc },
        });
    }

    async moveDocument(doc: Document): Promise<void> {
        const dialogRef = this.dialog.open<
            MoveDocumentDialog,
            MoveDocumentDialogData,
            string | null
        >(MoveDocumentDialog, { data: { documentName: doc.fileName } });
        const targetFolderId = await firstValueFrom(dialogRef.afterClosed());
        if (targetFolderId === undefined) {
            return;
        }

        this.errorMessage.set(null);
        try {
            const updated = await this.documentsService.moveDocument(doc.id, targetFolderId);
            if (targetFolderId === this.currentFolderId()) {
                this.documents.update((docs) =>
                    docs.map((d) => (d.id === updated.id ? updated : d)),
                );
            } else {
                this.documents.update((docs) => docs.filter((d) => d.id !== updated.id));
            }
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        }
    }

    async deleteDocument(doc: Document): Promise<void> {
        const confirmed = await this.confirm({
            title: 'Delete document',
            message: `Delete "${doc.fileName}"? This cannot be undone.`,
        });
        if (!confirmed) {
            return;
        }

        this.errorMessage.set(null);
        try {
            await this.documentsService.deleteDocument(doc.id);
            this.documents.update((docs) => docs.filter((d) => d.id !== doc.id));
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        }
    }

    async deleteFolder(folder: Folder): Promise<void> {
        const confirmed = await this.confirm({
            title: 'Delete folder',
            message: `Delete "${folder.name}"? The folder must be empty.`,
        });
        if (!confirmed) {
            return;
        }

        this.errorMessage.set(null);
        try {
            await this.documentsService.deleteFolder(folder.id);
            this.folders.update((current) => current.filter((f) => f.id !== folder.id));
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        }
    }

    private async confirm(data: ConfirmDialogData): Promise<boolean> {
        const dialogRef = this.dialog.open<ConfirmDialog, ConfirmDialogData, boolean>(
            ConfirmDialog,
            {
                data,
            },
        );
        return (await firstValueFrom(dialogRef.afterClosed())) ?? false;
    }

    private toErrorMessage(e: unknown): string {
        return e instanceof HttpErrorResponse && e.error?.message
            ? e.error.message
            : 'Something went wrong.';
    }
}
