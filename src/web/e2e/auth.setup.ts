import { expect, test as setup } from '@playwright/test';
import { ADMIN, INTERNAL } from './env';

// Sign-in through the app's sign-in page (the new app's own cookie since Round 4). Each user's cookies are saved for the tests
// that act as that user.
for (const user of [ADMIN, INTERNAL]) {
  setup(`sign in as the ${user.key} user`, async ({ page }) => {
    await page.goto('/user/sign-in');
    await page.locator('input[name="UserNameOrEmail"]').fill(user.userName);
    await page.locator('input[name="Password"]').fill(user.password);
    await page.locator('form[action*="sign-in"] [type="submit"]').click();
    // signed in: the user menu's Log off item (a form that posts since Round 5, S3)
    await expect(page.locator('form[action="/user/sign-out"]').first()).toBeAttached();
    await page.context().storageState({ path: user.stateFile });
  });
}
