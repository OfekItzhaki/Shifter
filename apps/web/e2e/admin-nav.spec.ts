import { test, expect } from "@playwright/test";
import { loginAsAdmin, enterAdminMode } from "./helpers/auth";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";

test.describe("Admin navigation", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await enterAdminMode(page);
  });

  const adminPages = [
    { path: "/admin/schedule",    label: /schedule management/i },
    { path: "/admin/people",      label: /people/i },
    { path: "/admin/tasks",       label: /task/i },
    { path: "/admin/constraints", label: /constraint/i },
    { path: "/admin/groups",      label: /group/i },
  ];

  for (const { path, label } of adminPages) {
    test(`${path} loads without error`, async ({ page }) => {
      await page.goto(`${BASE}${path}`);
      await expect(page.getByText(label).first()).toBeVisible({ timeout: 8000 });
      // No error boundary should be visible
      await expect(page.getByText(/something went wrong/i)).not.toBeVisible();
    });
  }

  test("notification bell is visible in header", async ({ page }) => {
    await page.goto(`${BASE}/admin/schedule`);
    // Bell button has aria-label="Notifications"
    await expect(page.getByRole("button", { name: /notifications/i })).toBeVisible();
  });

  test("logout works", async ({ page }) => {
    await page.goto(`${BASE}/schedule/today`);
    await page.getByRole("button", { name: /logout/i }).click();
    await expect(page).toHaveURL(/login/, { timeout: 5000 });
  });
});
