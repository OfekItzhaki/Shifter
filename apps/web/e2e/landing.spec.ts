import { test, expect } from "@playwright/test";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";

test.describe("Landing Page", () => {
  test("shows landing page for unauthenticated users", async ({ page }) => {
    // Clear any stored tokens
    await page.goto(`${BASE}/login`);
    await page.evaluate(() => {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      document.cookie = "locale=en; path=/; max-age=31536000; SameSite=Strict";
    });

    await page.goto(BASE);
    await expect(page.locator("h1")).toContainText("Build fair schedules");
    await expect(page.locator("text=Manual self-service")).toBeVisible();
  });

  test("has sign in and get started buttons", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.evaluate(() => {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      document.cookie = "locale=en; path=/; max-age=31536000; SameSite=Strict";
    });

    await page.goto(BASE);
    await expect(page.locator('a[href="/login"]').first()).toBeVisible();
    await expect(page.locator('a[href="/register"]').first()).toBeVisible();
  });

  test("features section is visible", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.evaluate(() => {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      document.cookie = "locale=en; path=/; max-age=31536000; SameSite=Strict";
    });

    await page.goto(BASE);
    await expect(page.locator("#features")).toBeVisible();
    await expect(page.locator("text=Two scheduling modes")).toBeVisible();
  });

  test("FAQ section has expandable items", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.evaluate(() => {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      document.cookie = "locale=en; path=/; max-age=31536000; SameSite=Strict";
    });

    await page.goto(BASE);
    const faqSection = page.locator("#faq");
    await expect(faqSection).toBeVisible();

    // Click first FAQ item
    const firstQuestion = faqSection.locator("details").first();
    await firstQuestion.locator("summary").click();
    await expect(firstQuestion).toHaveAttribute("open", "");
  });

  test("navigation links scroll to sections", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.evaluate(() => {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      document.cookie = "locale=en; path=/; max-age=31536000; SameSite=Strict";
    });

    await page.goto(BASE);
    await page.locator('a[href="#product"]').first().click();
    await expect(page.locator("#product")).toBeInViewport({ timeout: 3000 });
  });

  test("terms page renders", async ({ page }) => {
    await page.goto(`${BASE}/terms`);
    await expect(page.locator("h1")).toContainText("תנאי שימוש");
  });

  test("privacy page renders", async ({ page }) => {
    await page.goto(`${BASE}/privacy`);
    await expect(page.locator("h1")).toContainText("מדיניות פרטיות");
  });
});

test.describe("Join Group Page", () => {
  test("shows join form", async ({ page }) => {
    await page.goto(`${BASE}/groups/join`);
    await expect(page.locator('input[type="text"]')).toBeVisible();
  });

  test("shows login prompt for unauthenticated users with code", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.evaluate(() => {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
    });

    await page.goto(`${BASE}/groups/join?code=TEST1234`);
    // Should show sign in prompt
    await expect(page.locator("text=Sign In").or(page.locator("text=התחברות"))).toBeVisible({ timeout: 5000 });
  });
});
