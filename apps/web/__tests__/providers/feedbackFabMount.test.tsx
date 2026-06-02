import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Providers } from "@/app/providers";

let authenticated = false;

vi.mock("@/lib/store/authStore", () => ({
  useAuthStore: Object.assign(
    (selector?: (state: any) => any) => {
      const state = { isAuthenticated: authenticated, userId: authenticated ? "user-1" : null };
      return selector ? selector(state) : state;
    },
    {
      persist: {
        hasHydrated: () => true,
        onFinishHydration: () => vi.fn(),
      },
    }
  ),
}));

vi.mock("@/lib/query/queryClient", () => ({
  queryClient: {
    getQueryData: vi.fn(),
    setQueryData: vi.fn(),
    removeQueries: vi.fn(),
    clear: vi.fn(),
    invalidateQueries: vi.fn(),
    mount: vi.fn(),
    unmount: vi.fn(),
    subscribe: vi.fn(() => vi.fn()),
    getOptions: vi.fn(() => ({})),
  },
}));

vi.mock("@/components/ThemeProvider", () => ({
  default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock("@/components/shell/OfflineBanner", () => ({
  default: () => null,
}));

vi.mock("@/components/admin/AdminSessionGuard", () => ({
  default: () => null,
}));

vi.mock("@/components/shell/FeedbackFab", () => ({
  default: () => <div data-testid="feedback-fab" />,
}));

vi.mock("@/lib/analytics/posthog", () => ({
  initPostHog: vi.fn(),
}));

vi.mock("@/lib/api/client", () => ({
  initConnectivity: vi.fn(() => vi.fn()),
}));

vi.mock("@/lib/cache/backgroundRefresh", () => ({
  initBackgroundRefresh: vi.fn(() => vi.fn()),
}));

vi.mock("@/lib/hooks/useCacheLifecycle", () => ({
  useCacheLifecycle: vi.fn(),
}));

describe("Providers feedback FAB mount", () => {
  beforeEach(() => {
    authenticated = false;
    localStorage.clear();
  });

  it("renders the feedback FAB when the auth store is authenticated", async () => {
    authenticated = true;

    render(
      <Providers>
        <div>content</div>
      </Providers>
    );

    expect(await screen.findByTestId("feedback-fab")).toBeInTheDocument();
  });

  it("renders the feedback FAB when an access token exists but auth state is stale", async () => {
    localStorage.setItem("access_token", "token");

    render(
      <Providers>
        <div>content</div>
      </Providers>
    );

    await waitFor(() => {
      expect(screen.getByTestId("feedback-fab")).toBeInTheDocument();
    });
  });

  it("does not render the feedback FAB without auth state or an access token", async () => {
    render(
      <Providers>
        <div>content</div>
      </Providers>
    );

    expect(screen.queryByTestId("feedback-fab")).not.toBeInTheDocument();
  });
});
