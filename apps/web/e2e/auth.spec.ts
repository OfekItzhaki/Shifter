import { test, expect } from "@playwright/test";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";
const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
const ADMIN_PASS  = process.env.E2E_ADMIN_PASS  ?? "Demo1234!";

test.describe("Authentication", () => {
  test("login page renders", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await expect(page.getByRole("button", { name: /sign in/i })).toBeVisible();
  });

  test("invalid credentials shows error", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.getByLabel(/email/i).fill("wrong@example.com");
    await page.getByLabel(/password/i).fill("wrongpassword");
    await page.getByRole("button", { name: /sign in/i }).click();
    await expect(page.getByText(/invalid credentials/i)).toBeVisible({ timeout: 5000 });
  });

  test("valid credentials redirects to schedule", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.getByLabel(/email/i).fill(ADMIN_EMAIL);
    await page.getByLabel(/password/i).fill(ADMIN_PASS);
    await page.getByRole("button", { name: /sign in/i }).click();
    // Should land on today's schedule or spaces page
    await expect(page).toHaveURL(/\/(schedule|spaces)/, { timeout: 8000 });
  });
});
