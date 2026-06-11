import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProviderHealthPanel from "../../components/platform/ProviderHealthPanel";

const mockGetProviderHealthReport = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    const translations: Record<string, string> = {
      title: "Provider Health",
      description: "Live readiness for providers.",
      loading: "Checking providers...",
      refresh: "Refresh",
      refreshing: "Refreshing...",
      loadError: "Could not load provider health.",
      summary: `${values?.healthy ?? 0}/${values?.total ?? 0} healthy, ${values?.unhealthy ?? 0} unhealthy, ${values?.skipped ?? 0} skipped`,
      updated: `Updated ${values?.time ?? ""}`,
      responseTime: `Response ${values?.time ?? ""}`,
      "status.healthy": "Healthy",
      "status.degraded": "Degraded",
      "status.unhealthy": "Unhealthy",
      "status.skipped": "Skipped",
    };
    return translations[key] ?? key;
  },
}));

vi.mock("../../lib/api/platform", () => ({
  getProviderHealthReport: (...args: unknown[]) => mockGetProviderHealthReport(...args),
}));

describe("ProviderHealthPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetProviderHealthReport.mockResolvedValue({
      overallStatus: "degraded",
      version: "1.0.0",
      timestamp: "2026-06-11T12:00:00Z",
      checks: [
        {
          serviceName: "postgres",
          status: "healthy",
          errorMessage: null,
          responseTime: "00:00:00.0100000",
        },
        {
          serviceName: "resend",
          status: "unhealthy",
          errorMessage: "HTTP 401 Unauthorized",
          responseTime: "00:00:00.1000000",
        },
        {
          serviceName: "ai",
          status: "skipped",
          errorMessage: null,
          responseTime: null,
        },
      ],
    });
  });

  it("shows degraded provider health and can refresh", async () => {
    render(<ProviderHealthPanel />);

    expect(await screen.findByText("Provider Health")).toBeInTheDocument();
    expect(screen.getByText("Degraded")).toBeInTheDocument();
    expect(screen.getByText("1/3 healthy, 1 unhealthy, 1 skipped")).toBeInTheDocument();
    expect(screen.getByText("postgres")).toBeInTheDocument();
    expect(screen.getByText("resend")).toBeInTheDocument();
    expect(screen.getByText("HTTP 401 Unauthorized")).toBeInTheDocument();
    expect(screen.getByText("ai")).toBeInTheDocument();
    expect(screen.getAllByText("Skipped").length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole("button", { name: "Refresh" }));

    await waitFor(() => {
      expect(mockGetProviderHealthReport).toHaveBeenCalledTimes(2);
    });
  });
});
