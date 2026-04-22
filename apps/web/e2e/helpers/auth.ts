import { Page } from "@playwright/test";

const BASE         = process.env.E2E_BASE_URL   ?? "http://localhost:3000";
const ADMIN_EMAIL  = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
const ADMIN_PASS   = process.env.E2E_ADMIN_PASS  ?? "Demo1234!";

/**
 * Log in as the demo admin and wait for the app shell to appear.
 * Call this at the start of any test that needs an authenticated session.
 */
export async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto(`${BASE}/login`);
  await page.getByLabel(/email/i).fill(ADMIN_EMAIL);
  await page.getByLabel(/password/i).fill(ADMIN_PASS);
  await page.getByRole("button", { name: /sign in/i }).click();
  // Wait for the app shell nav to appear
  await page.waitForURL(/\/(schedule|spaces)/, { timeout: 10000 });
}

/**
 * Enter admin mode by clicking the "Enter Admin Mode" button.
 */
export async function enterAdminMode(page: Page): Promise<void> {
  const btn = page.getByRole("button", { name: /enter admin mode/i });
  if (await btn.isVisible()) await btn.click();
}
