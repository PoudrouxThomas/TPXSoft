import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Document } from '@tpxsoft/documents-client';
import { DocumentsService } from '../../../core/documents/documents.service';

export interface PreviewDialogData {
    document: Document;
}

type PreviewKind = 'image' | 'embed' | 'unsupported';

@Component({
    selector: 'app-preview-dialog',
    imports: [MatDialogModule, MatButtonModule, MatProgressSpinnerModule],
    templateUrl: './preview-dialog.html',
    styleUrl: './preview-dialog.scss',
})
export class PreviewDialog implements OnDestroy {
    protected readonly data = inject<PreviewDialogData>(MAT_DIALOG_DATA);
    private readonly documentsService = inject(DocumentsService);
    private readonly sanitizer = inject(DomSanitizer);

    protected readonly loading = signal(true);
    readonly errorMessage = signal<string | null>(null);
    readonly previewUrl = signal<SafeResourceUrl | null>(null);
    protected readonly kind = computed<PreviewKind>(() =>
        this.classify(this.data.document.contentType),
    );

    private objectUrl: string | null = null;

    constructor() {
        void this.loadPreview();
    }

    ngOnDestroy(): void {
        if (this.objectUrl) {
            URL.revokeObjectURL(this.objectUrl);
        }
    }

    async downloadInstead(): Promise<void> {
        try {
            const blob = await this.documentsService.downloadContent(this.data.document.id);
            this.triggerDownload(blob);
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        }
    }

    private async loadPreview(): Promise<void> {
        this.loading.set(true);
        this.errorMessage.set(null);
        try {
            const blob = await this.documentsService.downloadContent(this.data.document.id);
            if (this.kind() === 'unsupported') {
                return;
            }
            this.objectUrl = URL.createObjectURL(blob);
            this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl));
        } catch (e) {
            this.errorMessage.set(this.toErrorMessage(e));
        } finally {
            this.loading.set(false);
        }
    }

    private classify(contentType: string): PreviewKind {
        if (contentType.startsWith('image/')) {
            return 'image';
        }
        if (
            contentType === 'application/pdf' ||
            contentType.startsWith('text/') ||
            contentType.startsWith('video/') ||
            contentType.startsWith('audio/')
        ) {
            return 'embed';
        }
        return 'unsupported';
    }

    private triggerDownload(blob: Blob): void {
        const url = URL.createObjectURL(blob);
        const anchor = window.document.createElement('a');
        anchor.href = url;
        anchor.download = this.data.document.fileName;
        anchor.click();
        URL.revokeObjectURL(url);
    }

    private toErrorMessage(e: unknown): string {
        return e instanceof HttpErrorResponse && e.error?.message
            ? e.error.message
            : 'Something went wrong.';
    }
}
