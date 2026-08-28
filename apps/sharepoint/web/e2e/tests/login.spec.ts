import { test, expect } from '@playwright/test';
import { mockEmptyRoot, mockLogin } from './support/mock-documents';

test('logging in with valid credentials redirects to home and shows the user email', async ({
    page,
}) => {
    await mockLogin(page);
    await mockEmptyRoot(page);

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
