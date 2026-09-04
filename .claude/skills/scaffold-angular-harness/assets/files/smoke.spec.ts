import { expect, test } from '@playwright/test';

// One journey to prove the wiring end to end. Add real journeys here -- login, the
// main create flow, one error path -- not one spec per component.
test('the app boots without console errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text());
  });
  page.on('pageerror', (error) => errors.push(error.message));

  await page.goto('/');
  await expect(page.locator('app-root')).toBeVisible();

  expect(errors).toEqual([]);
});
