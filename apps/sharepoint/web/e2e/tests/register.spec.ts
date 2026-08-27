import { test, expect } from '@playwright/test';

test('registering with valid details redirects to home and shows the user email', async ({
    page,
}) => {
    await page.route('**/auth/register', async (route) => {
        await route.fulfill({
            status: 201,
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

    await page.goto('/register');

    await page.getByLabel('Email').fill('jane@example.com');
    await page.getByLabel('Password').fill('password123');
    await page.getByLabel('Organization name').fill('Acme');
    await page.getByRole('button', { name: 'Register' }).click();

    await expect(page).toHaveURL('/');
    await expect(page.getByText('jane@example.com')).toBeVisible();
});
