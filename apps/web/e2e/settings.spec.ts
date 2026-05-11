import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";

test.describe("User Settings", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("profile page renders with notification preferences", async ({ page }) => {
    await page.goto(`${BASE}/profile`);
    await expect(page.locator("text=Notification Preferences").or(page.locator("text=העדפות התראות"))).toBeVisible({ timeout: 10000 });
  });

  test("dark mode toggle exists in sidebar", async ({ page }) => {
    await page.goto(`${BASE}/profile`);
    // The dark mode toggle shows moon/sun emojis
    await expect(page.locator("text=🌙")).toBeVisible({ timeout: 10000 });
  });

  test("notification preference toggles work", async ({ page }) => {
    await page.goto(`${BASE}/profile`);
    // Find a toggle switch
    const toggles = page.locator('button[role="switch"]');
    await expect(toggles.first()).toBeVisible({ timeout: 10000 });

    // Click a toggle
    const firstToggle = toggles.first();
    const wasChecked = await firstToggle.getAttribute("aria-checked");
    await firstToggle.click();
    const nowChecked = await firstToggle.getAttribute("aria-checked");
    expect(nowChecked).not.toBe(wasChecked);
  });
});
