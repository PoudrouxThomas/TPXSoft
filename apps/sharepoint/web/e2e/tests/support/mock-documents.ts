import { Page } from '@playwright/test';

/** Mocks a logged-in user (jane@example.com) and an empty Documents root, the baseline every
 * e2e test needs since the file explorer is now the '/' page and fetches on load. */
export async function mockLogin(page: Page): Promise<void> {
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
}

export async function mockEmptyRoot(page: Page): Promise<void> {
    await page.route('**/folders', async (route) => {
        if (route.request().method() === 'GET') {
            await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
            return;
        }
        await route.fallback();
    });
    await page.route('**/documents', async (route) => {
        if (route.request().method() === 'GET') {
            await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
            return;
        }
        await route.fallback();
    });
}

export async function loginAndLand(page: Page): Promise<void> {
    await mockLogin(page);
    await mockEmptyRoot(page);

    await page.goto('/login');
    await page.getByLabel('Email').fill('jane@example.com');
    await page.getByLabel('Password').fill('password123');
    await page.getByRole('button', { name: 'Log in' }).click();
    await page.waitForURL('/');
}
