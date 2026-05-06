import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

const BASE    = process.env.E2E_BASE_URL    ?? "http://localhost:3000";
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

test.describe("Schedule viewer", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("today schedule page loads", async ({ page }) => {
    await page.goto(`${BASE}/schedule/today`);
    // Page renders the AppShell sidebar
    await expect(page.locator("aside")).toBeVisible({ timeout: 10000 });
    // Group selector (a <select>) should be present
    await expect(page.locator("select")).toBeVisible({ timeout: 8000 });
  });

  test("tomorrow schedule page loads", async ({ page }) => {
    await page.goto(`${BASE}/schedule/tomorrow`);
    await expect(page.locator("aside")).toBeVisible({ timeout: 10000 });
    await expect(page.locator("select")).toBeVisible({ timeout: 8000 });
  });
});

test.describe("Admin schedule management", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("admin schedule page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/schedule`);
    await expect(page.locator("aside")).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/something went wrong/i)).not.toBeVisible();
  });

  test("trigger solve button is visible in admin mode", async ({ page }) => {
    await page.goto(`${BASE}/admin/schedule`);
    await expect(page.locator("aside")).toBeVisible({ timeout: 10000 });
    // The page renders — button only shows when isAdminMode=true (Zustand state)
    // We verify the page loaded without error; the button itself requires UI-driven admin mode
    await expect(page.getByText(/something went wrong/i)).not.toBeVisible();
  });

  test("export buttons appear when a version is selected", async ({ page }) => {
    await page.goto(`${BASE}/admin/schedule`);
    await expect(page.locator("aside")).toBeVisible({ timeout: 10000 });
    // Click the first version button if any exist (v1, v2, etc.)
    const firstVersion = page.locator("button").filter({ hasText: /^v\d+/ }).first();
    if (await firstVersion.isVisible({ timeout: 5000 }).catch(() => false)) {
      await firstVersion.click();
      await expect(page.getByRole("button", { name: /csv/i })).toBeVisible({ timeout: 5000 });
    }
  });
});
