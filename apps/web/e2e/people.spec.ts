import { test, expect } from "@playwright/test";
import { loginAsAdmin, enterAdminMode } from "./helpers/auth";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";

test.describe("People management", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await enterAdminMode(page);
  });

  test("people list page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/people`);
    await expect(page.getByText(/people/i).first()).toBeVisible({ timeout: 8000 });
  });

  test("can create a new person", async ({ page }) => {
    await page.goto(`${BASE}/admin/people`);

    const uniqueName = `E2E Person ${Date.now()}`;
    await page.getByPlaceholder(/full name/i).fill(uniqueName);
    await page.getByRole("button", { name: /add person/i }).click();

    await expect(page.getByText(uniqueName)).toBeVisible({ timeout: 8000 });
  });

  test("person detail page loads with sections", async ({ page }) => {
    await page.goto(`${BASE}/admin/people`);

    // Click the first person link
    const firstPerson = page.locator("a, button").filter({ hasText: /view|details|→/ }).first();
    if (await firstPerson.isVisible()) {
      await firstPerson.click();
      await expect(page.getByText(/roles/i)).toBeVisible({ timeout: 8000 });
      await expect(page.getByText(/restrictions/i)).toBeVisible();
      await expect(page.getByText(/availability/i)).toBeVisible();
    }
  });
});
