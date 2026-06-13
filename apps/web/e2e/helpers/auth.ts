import { Page } from "@playwright/test";

const BASE        = process.env.E2E_BASE_URL   ?? "http://localhost:3000";
const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
const ADMIN_PASS  = process.env.E2E_ADMIN_PASS  ?? "Demo1234!";
const API_URL     = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
const ANALYTICS_CONSENT_KEY = "shifter_analytics_consent";

/**
 * Log in as the demo admin using structural selectors (locale-agnostic).
 * Waits for the space to be selected (skips the /spaces page if needed).
 */
export async function loginAsAdmin(page: Page): Promise<void> {
  await loginAsUser(page, ADMIN_EMAIL, ADMIN_PASS);
}

export async function loginAsUser(
  page: Page,
  email: string,
  password: string = ADMIN_PASS
): Promise<void> {
  await page.context().clearCookies();
  await page.goto(BASE, { waitUntil: "domcontentloaded" });
  await page.evaluate((consentKey) => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    window.localStorage.setItem(consentKey, "declined");
  }, ANALYTICS_CONSENT_KEY);
  await page.goto(`${BASE}/login`);
  await page.locator('input[autocomplete="username"]').waitFor({ state: "visible", timeout: 10000 });
  await page.locator('input[autocomplete="username"]').fill(email);
  await page.locator('input[type="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  // Wait until we leave /login
  await page.waitForFunction(() => !window.location.pathname.startsWith("/login"), { timeout: 20000 });
  await ensureCurrentSpace(page);

  // If redirected to /spaces (space selection), pick the first space
  if (page.url().includes("/spaces")) {
    const firstSpaceBtn = page.locator("button").first();
    if (await firstSpaceBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await firstSpaceBtn.click();
      await page.waitForFunction(() => !window.location.pathname.startsWith("/spaces"), { timeout: 10000 });
      await ensureCurrentSpace(page);
    }
  }

  await page.reload({ waitUntil: "domcontentloaded" });
  await ensureCurrentSpace(page);
}

async function ensureCurrentSpace(page: Page): Promise<void> {
  const hasSpace = await page.evaluate(() => {
    const raw = localStorage.getItem("jobuler-space");
    try {
      return Boolean(raw ? JSON.parse(raw).state?.currentSpaceId : null);
    } catch {
      return false;
    }
  });

  if (hasSpace) return;

  const token = await page.evaluate(() => localStorage.getItem("access_token"));
  if (!token) return;

  const response = await page.request.get(`${API_URL}/spaces`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok()) return;

  const spaces = await response.json() as Array<{ id: string; name: string }>;
  const space = spaces[0];
  if (!space) return;

  await page.evaluate((selectedSpace) => {
    localStorage.setItem("jobuler-space", JSON.stringify({
      state: {
        currentSpaceId: selectedSpace.id,
        currentSpaceName: selectedSpace.name,
      },
      version: 0,
    }));
  }, space);
}

/**
 * Enter admin mode by fetching the first group via the API, navigating to it,
 * then clicking the admin mode toggle button.
 * adminGroupId is NOT persisted in Zustand so this must go through the UI.
 */
export async function enterAdminMode(page: Page): Promise<void> {
  // Grab token + spaceId from localStorage (set during login)
  const { token, spaceId } = await page.evaluate(() => {
    const raw = localStorage.getItem("jobuler-space");
    let spaceId: string | null = null;
    try { spaceId = raw ? JSON.parse(raw).state?.currentSpaceId : null; } catch { /* ignore */ }
    return { token: localStorage.getItem("access_token"), spaceId };
  });

  if (token && spaceId) {
    // Fetch groups directly from the API
    const resp = await page.request.get(`${API_URL}/spaces/${spaceId}/groups`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (resp.ok()) {
      const groups = await resp.json() as Array<{ id: string }>;
      if (groups.length > 0) {
        await page.goto(`${BASE}/groups/${groups[0].id}`);
        await page.waitForURL(/\/groups\/[^/]+$/, { timeout: 10000 });
        // Click the admin mode button — it's the only button with data-admin-toggle
        // or we find it by its position in the settings tab area
        const adminBtn = page.locator("button").filter({ hasText: /admin|ניהול|администрат/i }).first();
        if (await adminBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
          await adminBtn.click();
        }
        return;
      }
    }
  }

  // Fallback: navigate to groups page and click first group card
  await page.goto(`${BASE}/groups`);
  const firstGroup = page.locator("button").filter({ hasText: /חברים|members|участник/i }).first();
  if (await firstGroup.isVisible({ timeout: 8000 }).catch(() => false)) {
    await Promise.all([
      page.waitForURL(/\/groups\/[^/]+$/, { timeout: 10000 }),
      firstGroup.click(),
    ]);
    const adminBtn = page.locator("button").filter({ hasText: /admin|ניהול|администрат/i }).first();
    if (await adminBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await adminBtn.click();
    }
  }
}
