import { Page, test, expect } from '@playwright/test';
import { mockLogin } from './support/mock-documents';
import { DocumentsFakeApi } from './support/documents-fake-api';

async function login(page: Page): Promise<void> {
    await page.goto('/login');
    await page.getByLabel('Email').fill('jane@example.com');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Log in' }).click();
    await expect(page).toHaveURL('/');
}

async function setup(page: Page): Promise<DocumentsFakeApi> {
    await mockLogin(page);
    const api = new DocumentsFakeApi();
    await api.install(page);
    await login(page);
    return api;
}

async function upload(page: Page, name: string, content = 'contents'): Promise<void> {
    await page.locator('input[type="file"]').setInputFiles({
        name,
        mimeType: 'text/plain',
        buffer: Buffer.from(content),
    });
}

test.describe('File explorer', () => {
    test('creating a folder shows it, and navigating in and out works', async ({ page }) => {
        await setup(page);

        await expect(page.getByText('This folder is empty.')).toBeVisible();

        await page.getByRole('button', { name: 'New folder' }).click();
        await page.getByLabel('Folder name').fill('Engineering');
        await page.getByRole('dialog').getByRole('button', { name: 'Create' }).click();

        const folderButton = page.getByRole('button', { name: 'Engineering', exact: true });
        await expect(folderButton).toBeVisible();

        await folderButton.click();
        await expect(page.getByText('This folder is empty.')).toBeVisible();

        await page.getByRole('button', { name: 'My files' }).click();
        await expect(page.getByRole('button', { name: 'Engineering', exact: true })).toBeVisible();
    });

    test('uploading a file with an existing name gets an incrementing suffix', async ({ page }) => {
        await setup(page);

        await upload(page, 'report.pdf', 'first');
        await expect(page.getByText('report.pdf', { exact: true })).toBeVisible();

        await upload(page, 'report.pdf', 'second');
        await expect(page.getByText('report_1.pdf', { exact: true })).toBeVisible();

        await upload(page, 'report.pdf', 'third');
        await expect(page.getByText('report_2.pdf', { exact: true })).toBeVisible();
    });

    test('previewing a document opens a dialog showing its name', async ({ page }) => {
        await setup(page);

        await upload(page, 'notes.txt');
        await expect(page.getByText('notes.txt', { exact: true })).toBeVisible();

        await page.getByRole('button', { name: 'notes.txt', exact: true }).click();
        const dialog = page.getByRole('dialog');
        await expect(dialog.getByRole('heading', { name: 'notes.txt' })).toBeVisible();

        await dialog.getByRole('button', { name: 'Close' }).click();
        await expect(page.getByRole('dialog')).toHaveCount(0);
    });

    test('moving a document into a folder removes it from the current view', async ({ page }) => {
        await setup(page);

        await page.getByRole('button', { name: 'New folder' }).click();
        await page.getByLabel('Folder name').fill('Archive');
        await page.getByRole('dialog').getByRole('button', { name: 'Create' }).click();
        await expect(page.getByRole('button', { name: 'Archive', exact: true })).toBeVisible();

        await upload(page, 'notes.txt');
        await expect(page.getByText('notes.txt', { exact: true })).toBeVisible();

        await page.getByRole('button', { name: 'Document actions for notes.txt' }).click();
        await page.getByRole('menuitem', { name: 'Move' }).click();

        const dialog = page.getByRole('dialog');
        await dialog.getByText('Archive', { exact: true }).click();
        await dialog.getByRole('button', { name: /Move to/ }).click();

        await expect(page.getByText('notes.txt', { exact: true })).not.toBeVisible();

        await page.getByRole('button', { name: 'Archive', exact: true }).click();
        await expect(page.getByText('notes.txt', { exact: true })).toBeVisible();
    });

    test('deleting a document removes it from the list', async ({ page }) => {
        await setup(page);

        await upload(page, 'notes.txt');
        await expect(page.getByText('notes.txt', { exact: true })).toBeVisible();

        await page.getByRole('button', { name: 'Document actions for notes.txt' }).click();
        await page.getByRole('menuitem', { name: 'Delete' }).click();
        await page.getByRole('dialog').getByRole('button', { name: 'Delete', exact: true }).click();

        await expect(page.getByText('notes.txt', { exact: true })).not.toBeVisible();
        await expect(page.getByText('This folder is empty.')).toBeVisible();
    });

    test('deleting an empty folder removes it from the list', async ({ page }) => {
        await setup(page);

        await page.getByRole('button', { name: 'New folder' }).click();
        await page.getByLabel('Folder name').fill('Temp');
        await page.getByRole('dialog').getByRole('button', { name: 'Create' }).click();
        await expect(page.getByRole('button', { name: 'Temp', exact: true })).toBeVisible();

        await page.getByRole('button', { name: 'Folder actions for Temp' }).click();
        await page.getByRole('menuitem', { name: 'Delete' }).click();
        await page.getByRole('dialog').getByRole('button', { name: 'Delete', exact: true }).click();

        await expect(page.getByRole('button', { name: 'Temp', exact: true })).not.toBeVisible();
        await expect(page.getByText('This folder is empty.')).toBeVisible();
    });
});
