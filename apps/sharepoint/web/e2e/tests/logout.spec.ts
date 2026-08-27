import { test, expect } from '@playwright/test';

test('logging out returns to /login, and / then redirects back to /login', async ({ page }) => {
    await page.route('**/auth/login', async (route) => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ accessToken: 'access-token', refreshToken: 'refresh-token' }),
        });
    });
    await page.route('**/auth/me', async (route) => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                id: 'user-1',
                email: 'jane@example.com',
                orgId: 'org-1',
                orgName: 'Acme',
                role: 'Admin',
            }),
        });
    });
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
