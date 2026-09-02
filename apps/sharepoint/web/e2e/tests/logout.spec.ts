import { test, expect } from '@playwright/test';
import { mockEmptyRoot, mockLogin } from './support/mock-documents';

test('logging out returns to /login, and / then redirects back to /login', async ({ page }) => {
    await mockLogin(page);
    await mockEmptyRoot(page);
    await page.route('**/auth/logout', async (route) => {
        await route.fulfill({ status: 204, body: '' });
    });

    await page.goto('/login');
    await page.getByLabel('Email').fill('jane@example.com');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Log in' }).click();
    await expect(page).toHaveURL('/');

    await page.getByRole('button', { name: 'Log out' }).click();
    await expect(page).toHaveURL('/login');

    await page.goto('/');
    await expect(page).toHaveURL('/login');
});
