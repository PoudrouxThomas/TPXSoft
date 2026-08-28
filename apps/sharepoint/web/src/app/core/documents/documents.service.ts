import { Injectable, inject } from '@angular/core';
import { ApiService, Document, Folder, FolderChildren } from '@tpxsoft/documents-client';

/**
 * Thin wrapper over the generated Documents client. `GET /folders` and `GET /documents` cannot
 * express "root only" (no `parentFolderId`/`folderId` means "every level", not "top level" — see
 * modules/documents/documentation/02-virtual-folders.md and 07-manage-folders.md), so root
 * listings fetch everything visible to the caller and filter to the null-parent rows here.
 */
@Injectable({ providedIn: 'root' })
export class DocumentsService {
    private readonly api = inject(ApiService);

    async listChildren(folderId: string | null): Promise<FolderChildren> {
        if (folderId) {
            return this.api.listFolderChildren({ id: folderId });
        }

        const [allFolders, allDocuments] = await Promise.all([
            this.api.listFolders({}),
            this.api.listDocuments({}),
        ]);
        return {
            folders: allFolders.filter((f) => f.parentFolderId === null),
            documents: allDocuments.filter((d) => d.folderId === null),
        };
    }

    async listFoldersIn(parentFolderId: string | null): Promise<Folder[]> {
        if (parentFolderId) {
            const children = await this.api.listFolderChildren({ id: parentFolderId });
            return children.folders;
        }

        const allFolders = await this.api.listFolders({});
        return allFolders.filter((f) => f.parentFolderId === null);
    }

    createFolder(name: string, parentFolderId: string | null): Promise<Folder> {
        return this.api.createFolder({ body: { name, parentFolderId } });
    }

    deleteFolder(id: string): Promise<void> {
        return this.api.deleteFolder({ id });
    }

    deleteDocument(id: string): Promise<void> {
        return this.api.deleteDocument({ id });
    }

    moveDocument(id: string, folderId: string | null): Promise<Document> {
        return this.api.updateDocument({ id, body: { folderId } });
    }

    uploadDocument(file: File, folderId: string | null): Promise<Document> {
        return this.api.uploadDocument({ body: { file, folderId } });
    }

    downloadContent(id: string): Promise<Blob> {
        return this.api.downloadDocumentContent({ id });
    }
}
