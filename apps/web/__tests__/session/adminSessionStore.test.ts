import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useAdminSessionStore } from "../../lib/store/adminSessionStore";

function resetStore() {
  useAdminSessionStore.setState({
    isElevated: false,
    elevatedMode: null,
    elevatedGroupId: null,
    timeoutDuration: 15,
    remainingMs: 0,
    lastActivityAt: 0,
    isPromptVisible: false,
    promptCountdownMs: 0,
    lastExitContext: null,
  });
  useAdminSessionStore.persist.clearStorage();
}

describe("adminSessionStore persistence", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-06-05T10:00:00.000Z"));
    localStorage.clear();
    resetStore();
  });

  afterEach(() => {
    resetStore();
    vi.useRealTimers();
  });

  it("persists elevated mode with the configured timeout window", () => {
    useAdminSessionStore.getState().enterElevatedMode("management", "group-1", 30);

    const persisted = JSON.parse(
      localStorage.getItem("jobuler-admin-session") ?? "{}"
    ) as {
      state?: {
        isElevated?: boolean;
        elevatedMode?: string;
        elevatedGroupId?: string;
        timeoutDuration?: number;
        lastActivityAt?: number;
      };
    };

    expect(persisted.state?.isElevated).toBe(true);
    expect(persisted.state?.elevatedMode).toBe("management");
    expect(persisted.state?.elevatedGroupId).toBe("group-1");
    expect(persisted.state?.timeoutDuration).toBe(30);
    expect(persisted.state?.lastActivityAt).toBe(Date.now());
  });

  it("rehydrates an unexpired elevated session", async () => {
    const lastActivityAt = Date.now() - 5 * 60 * 1000;
    localStorage.setItem(
      "jobuler-admin-session",
      JSON.stringify({
        state: {
          isElevated: true,
          elevatedMode: "management",
          elevatedGroupId: "group-1",
          timeoutDuration: 15,
          remainingMs: 15 * 60 * 1000,
          lastActivityAt,
        },
        version: 0,
      })
    );

    await useAdminSessionStore.persist.rehydrate();

    const state = useAdminSessionStore.getState();
    expect(state.isElevated).toBe(true);
    expect(state.elevatedMode).toBe("management");
    expect(state.elevatedGroupId).toBe("group-1");
    expect(state.remainingMs).toBe(10 * 60 * 1000);
  });

  it("drops an expired elevated session during rehydration", async () => {
    localStorage.setItem(
      "jobuler-admin-session",
      JSON.stringify({
        state: {
          isElevated: true,
          elevatedMode: "management",
          elevatedGroupId: "group-1",
          timeoutDuration: 15,
          remainingMs: 15 * 60 * 1000,
          lastActivityAt: Date.now() - 16 * 60 * 1000,
        },
        version: 0,
      })
    );

    await useAdminSessionStore.persist.rehydrate();

    const state = useAdminSessionStore.getState();
    expect(state.isElevated).toBe(false);
    expect(state.elevatedMode).toBeNull();
    expect(state.elevatedGroupId).toBeNull();
  });
});
