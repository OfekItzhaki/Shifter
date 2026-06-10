import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

test.describe("Mobile self-service admin", () => {
  test("cycle controls render without page overflow", async ({ page }) => {
    await loginAsAdmin(page);

    const { token, spaceId } = await page.evaluate(() => {
      const raw = localStorage.getItem("jobuler-space");
      let spaceId: string | null = null;
      try {
        spaceId = raw ? JSON.parse(raw).state?.currentSpaceId : null;
      } catch {
        // Ignore malformed persisted state.
      }

      return { token: localStorage.getItem("access_token"), spaceId };
    });

    if (!token || !spaceId) {
      test.skip(true, "No authenticated space was available for E2E.");
      return;
    }

    const response = await page.request.get(`${API_URL}/spaces/${spaceId}/groups`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(response.ok()).toBeTruthy();

    const groups = await response.json() as Array<{ id: string; schedulingMode?: string }>;
    const selfServiceGroup = groups.find((group) => group.schedulingMode === "SelfService");
    if (!selfServiceGroup) {
      test.skip(true, "No self-service group exists in the E2E seed.");
      return;
    }

    await page.evaluate((groupId) => {
      const authRaw = localStorage.getItem("jobuler-auth");
      const authState = authRaw ? JSON.parse(authRaw) : { state: {}, version: 0 };
      authState.state = { ...(authState.state ?? {}), adminGroupId: groupId };
      localStorage.setItem("jobuler-auth", JSON.stringify(authState));

      localStorage.setItem("jobuler-admin-session", JSON.stringify({
        state: {
          isElevated: true,
          elevatedMode: "management",
          elevatedGroupId: groupId,
          timeoutDuration: 15,
          remainingMs: 15 * 60 * 1000,
          lastActivityAt: Date.now(),
        },
        version: 0,
      }));
    }, selfServiceGroup.id);

    await page.goto(`${BASE}/groups/${selfServiceGroup.id}`);
    await page.getByTestId("group-tab-self-service-config").click();

    await expect(page.getByText("Cycle controls")).toBeVisible({ timeout: 15000 });
    await expect(page.getByText("Generate first cycle").or(page.getByText("Generate next cycle"))).toBeVisible();

    const horizontalOverflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth
    );
    expect(horizontalOverflow).toBeLessThanOrEqual(2);
  });
});
