import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";

test.describe("Admin navigation", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  const adminPages = [
    "/admin/schedule",
    "/admin/people",
    "/admin/tasks",
    "/admin/constraints",
    "/admin/groups",
  ];

  for (const path of adminPages) {
    test(`${path} loads without error`, async ({ page }) => {
      await page.goto(`${BASE}${path}`);
      await expect(page.locator("aside")).toBeVisible({ timeout: 12000 });
      await expect(page.getByText(/something went wrong/i)).not.toBeVisible();
    });
  }

  test("notification bell is visible in sidebar", async ({ page }) => {
    await page.goto(`${BASE}/groups`);
    await expect(page.locator('button[aria-label="Notifications"]')).toBeVisible({ timeout: 8000 });
  });

  test("logout button is visible and works", async ({ page }) => {
    await page.goto(`${BASE}/schedule/today`);
    const logoutBtn = page.locator('[data-testid="logout-btn"]');
    await expect(logoutBtn).toBeVisible({ timeout: 8000 });
    // Force click to bypass any overlay issues
    await logoutBtn.click({ force: true });
    await expect(page).toHaveURL(/login/, { timeout: 12000 });
  });
});
