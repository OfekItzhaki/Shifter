import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useSessionBootstrap } from "@/lib/hooks/useSessionBootstrap";
import { useAuthStore } from "@/lib/store/authStore";
import { refreshSession } from "@/lib/api/auth";

vi.mock("@/lib/api/auth", () => ({
  refreshSession: vi.fn(),
}));

const mockedRefreshSession = vi.mocked(refreshSession);

function clearCookie(name: string) {
  document.cookie = `${name}=; path=/; max-age=0`;
}

describe("useSessionBootstrap", () => {
  beforeEach(() => {
    localStorage.clear();
    clearCookie("auth_guard");
    clearCookie("locale");
    mockedRefreshSession.mockReset();
    useAuthStore.setState({
      userId: null,
      displayName: null,
      preferredLocale: "he",
      isAuthenticated: false,
      isPlatformAdmin: false,
      adminGroupId: null,
      timezoneId: "Asia/Jerusalem",
      timezoneOffsetMinutes: 120,
    });
  });

  it("does not call refresh for anonymous browsers without an auth guard cookie", () => {
    renderHook(() => useSessionBootstrap());

    expect(mockedRefreshSession).not.toHaveBeenCalled();
    expect(localStorage.getItem("access_token")).toBeNull();
  });

  it("does not call refresh when an access token already exists", () => {
    document.cookie = "auth_guard=1; path=/";
    localStorage.setItem("access_token", "existing-access-token");

    renderHook(() => useSessionBootstrap());

    expect(mockedRefreshSession).not.toHaveBeenCalled();
    expect(localStorage.getItem("access_token")).toBe("existing-access-token");
  });

  it("hydrates auth state from a valid refresh cookie", async () => {
    document.cookie = "auth_guard=1; path=/";
    mockedRefreshSession.mockResolvedValue({
      accessToken: "new-access-token",
      accessTokenExpiresAt: "2026-06-09T12:00:00Z",
      userId: "user-1",
      displayName: "Ofek",
      preferredLocale: "en",
      isPlatformAdmin: true,
      timezoneId: "Asia/Jerusalem",
      timezoneOffsetMinutes: 180,
    });

    renderHook(() => useSessionBootstrap());

    await waitFor(() => {
      expect(localStorage.getItem("access_token")).toBe("new-access-token");
    });

    expect(useAuthStore.getState()).toMatchObject({
      userId: "user-1",
      displayName: "Ofek",
      preferredLocale: "en",
      isAuthenticated: true,
      isPlatformAdmin: true,
      timezoneId: "Asia/Jerusalem",
      timezoneOffsetMinutes: 180,
    });
    expect(document.cookie).toContain("auth_guard=1");
    expect(document.cookie).toContain("locale=en");
  });
});
