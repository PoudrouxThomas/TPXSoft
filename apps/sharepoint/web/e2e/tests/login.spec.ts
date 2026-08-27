import { test, expect } from '@playwright/test';

test('logging in with valid credentials redirects to home and shows the user email', async ({
    page,
}) => {
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

    await page.goto('/login');

    await page.getByLabel('Email').fill('jane@example.com');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Log in' }).click();

    await expect(page).toHaveURL('/');
    await expect(page.getByText('jane@example.com')).toBeVisible();
});

test('logging in with invalid credentials shows an inline error and stays on /login', async ({
    page,
}) => {
    await page.route('**/auth/login', async (route) => {
        await route.fulfill({
            status: 401,
            contentType: 'application/json',
            body: JSON.stringify({ message: 'Invalid email or password.' }),
        });
    });

    await page.goto('/login');

    await page.getByLabel('Email').fill('jane@example.com');
    await page.getByLabel('Password').fill('wrong-password');
    await page.getByRole('button', { name: 'Log in' }).click();

    await expect(page.getByText('Invalid email or password.')).toBeVisible();
    await expect(page).toHaveURL('/login');
});
