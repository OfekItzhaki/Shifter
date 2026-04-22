import { test, expect } from "@playwright/test";
import { loginAsAdmin, enterAdminMode } from "./helpers/auth";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";

test.describe("Schedule viewer", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("today schedule page loads", async ({ page }) => {
    await page.goto(`${BASE}/schedule/today`);
    // Either shows assignments or "no assignments" message
    await expect(
      page.getByText(/today|no assignments/i).first()
    ).toBeVisible({ timeout: 8000 });
  });

  test("tomorrow schedule page loads", async ({ page }) => {
    await page.goto(`${BASE}/schedule/tomorrow`);
    await expect(
      page.getByText(/tomorrow|no assignments/i).first()
    ).toBeVisible({ timeout: 8000 });
  });
});

test.describe("Admin schedule management", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await enterAdminMode(page);
  });

  test("admin schedule page loads and shows versions", async ({ page }) => {
    await page.goto(`${BASE}/admin/schedule`);
    await expect(page.getByText(/schedule management/i)).toBeVisible({ timeout: 8000 });
    // Versions panel should be visible
    await expect(page.getByText(/versions/i)).toBeVisible();
  });

  test("trigger solve button is visible", async ({ page }) => {
    await page.goto(`${BASE}/admin/schedule`);
    await expect(page.getByRole("button", { name: /trigger solve/i })).toBeVisible();
  });

  test("export buttons appear when a version is selected", async ({ page }) => {
    await page.goto(`${BASE}/admin/schedule`);
    // Click the first version if any exist
    const firstVersion = page.locator("button").filter({ hasText: /^v\d+/ }).first();
    if (await firstVersion.isVisible()) {
      await firstVersion.click();
      await expect(page.getByRole("button", { name: /↓ csv/i })).toBeVisible({ timeout: 5000 });
      await expect(page.getByRole("button", { name: /↓ pdf/i })).toBeVisible({ timeout: 5000 });
    }
  });
});
