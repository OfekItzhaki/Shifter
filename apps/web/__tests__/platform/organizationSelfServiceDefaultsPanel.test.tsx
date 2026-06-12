import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import OrganizationSelfServiceDefaultsPanel from "../../components/platform/OrganizationSelfServiceDefaultsPanel";

const mockSearchPlatformOrganizations = vi.fn();
const mockGetOrganizationSelfServiceDefaults = vi.fn();
const mockUpdateOrganizationSelfServiceDefaults = vi.fn();

const translations: Record<string, string> = {
  "platform.organizationDefaults:title": "Organization self-service defaults",
  "platform.organizationDefaults:description": "Set policy templates.",
  "platform.organizationDefaults:searchLabel": "Find organization",
  "platform.organizationDefaults:searchPlaceholder": "Search by organization",
  "platform.organizationDefaults:search": "Search",
  "platform.organizationDefaults:organizationLabel": "Organization",
  "platform.organizationDefaults:empty": "No organizations found.",
  "platform.organizationDefaults:loadOrganizationsError": "Could not load organizations.",
  "platform:loading": "Loading...",
  "platform:saveSettings": "Save",
  "spaces.selfServiceDefaults:loading": "Loading defaults...",
  "spaces.selfServiceDefaults:loadError": "Failed to load self-service defaults.",
  "spaces.selfServiceDefaults:saveError": "Failed to save self-service defaults.",
  "spaces.selfServiceDefaults:saved": "Self-service defaults saved.",
  "spaces.selfServiceDefaults:validationError": "All numeric values must be whole numbers within their allowed range.",
  "spaces.selfServiceDefaults:minMaxError": "Minimum shifts must be less than or equal to maximum shifts.",
  "spaces.selfServiceDefaults:windowError": "Request window open offset must be greater than the close offset.",
  "spaces.selfServiceDefaults:source.organization": "Organization default",
  "spaces.selfServiceDefaults:source.install": "Install default",
  "spaces.selfServiceDefaults:fields.minShiftsPerCycle": "Minimum shifts per cycle",
  "spaces.selfServiceDefaults:fields.maxShiftsPerCycle": "Maximum shifts per cycle",
  "spaces.selfServiceDefaults:fields.requestWindowOpenOffsetHours": "Request window opens before cycle (hours)",
  "spaces.selfServiceDefaults:fields.requestWindowCloseOffsetHours": "Request window closes before cycle (hours)",
  "spaces.selfServiceDefaults:fields.cancellationCutoffHours": "Cancellation cutoff (hours)",
  "spaces.selfServiceDefaults:fields.maxAbsencesPerCycle": "Absence reports per cycle",
  "spaces.selfServiceDefaults:fields.maxLateCancellationsPerCycle": "Late cancellations per cycle",
  "spaces.selfServiceDefaults:fields.lateCancellationWindowHours": "Late cancellation window (hours)",
  "spaces.selfServiceDefaults:fields.waitlistOfferMinutes": "Waitlist offer duration (minutes)",
  "spaces.selfServiceDefaults:fields.cycleDurationDays": "Cycle duration (days)",
  "spaces.selfServiceDefaults:fields.allowMemberShiftClaims": "Members can claim shifts",
  "spaces.selfServiceDefaults:fields.allowWaitlist": "Waitlist enabled",
  "spaces.selfServiceDefaults:fields.allowShiftChangeRequests": "Shift change requests enabled",
  "spaces.selfServiceDefaults:fields.allowAbsenceReports": "Absence reports enabled",
  "spaces.selfServiceDefaults:fields.allowShiftSwaps": "Shift swaps enabled",
};

const translators = new Map<string, (key: string) => string>();

vi.mock("next-intl", () => ({
  useTranslations: (namespace: string) => {
    if (!translators.has(namespace)) {
      translators.set(namespace, (key: string) => translations[`${namespace}:${key}`] ?? key);
    }
    return translators.get(namespace)!;
  },
}));

vi.mock("../../lib/api/platform", () => ({
  searchPlatformOrganizations: (...args: unknown[]) => mockSearchPlatformOrganizations(...args),
  getOrganizationSelfServiceDefaults: (...args: unknown[]) => mockGetOrganizationSelfServiceDefaults(...args),
  updateOrganizationSelfServiceDefaults: (...args: unknown[]) => mockUpdateOrganizationSelfServiceDefaults(...args),
}));

const defaults = {
  id: "defaults-1",
  source: "organization",
  minShiftsPerCycle: 1,
  maxShiftsPerCycle: 5,
  requestWindowOpenOffsetHours: 72,
  requestWindowCloseOffsetHours: 24,
  cancellationCutoffHours: 12,
  maxAbsencesPerCycle: 3,
  maxLateCancellationsPerCycle: 2,
  lateCancellationWindowHours: 24,
  waitlistOfferMinutes: 45,
  cycleDurationDays: 7,
  allowMemberShiftClaims: true,
  allowWaitlist: true,
  allowShiftChangeRequests: true,
  allowAbsenceReports: true,
  allowShiftSwaps: false,
};

describe("OrganizationSelfServiceDefaultsPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchPlatformOrganizations.mockResolvedValue([
      {
        id: "org-1",
        displayName: "Acme Operations",
        normalizedName: "ACME OPERATIONS",
        primaryOwnerUserId: "user-1",
        primaryOwnerEmail: "owner@example.com",
        primaryOwnerDisplayName: "Owner",
        countryCode: "IL",
        setupTemplate: "general",
        defaultLocale: "he",
        status: "Active",
        disabledAt: null,
        purgeEligibleAt: null,
        dedicatedDeploymentKey: null,
        spaceCount: 2,
        groupCount: 4,
        memberCount: 18,
        createdAt: "2026-06-12T00:00:00Z",
      },
    ]);
    mockGetOrganizationSelfServiceDefaults.mockResolvedValue(defaults);
    mockUpdateOrganizationSelfServiceDefaults.mockResolvedValue({
      ...defaults,
      maxShiftsPerCycle: 6,
      allowShiftSwaps: true,
    });
  });

  it("loads organization defaults and saves edited policy values", async () => {
    render(<OrganizationSelfServiceDefaultsPanel />);

    expect(await screen.findByText("Organization self-service defaults")).toBeInTheDocument();
    expect(await screen.findByText("Acme Operations (2 / 4 / 18)")).toBeInTheDocument();
    expect(await screen.findByText("Organization default")).toBeInTheDocument();
    expect(mockGetOrganizationSelfServiceDefaults).toHaveBeenCalledWith("org-1");

    fireEvent.change(screen.getByLabelText("Maximum shifts per cycle"), {
      target: { value: "6" },
    });
    fireEvent.click(screen.getByLabelText("Shift swaps enabled"));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(mockUpdateOrganizationSelfServiceDefaults).toHaveBeenCalledWith(
        "org-1",
        expect.objectContaining({
          maxShiftsPerCycle: 6,
          allowShiftSwaps: true,
        })
      );
    });
    expect(await screen.findByText("Self-service defaults saved.")).toBeInTheDocument();
  });

  it("validates organization defaults before saving", async () => {
    render(<OrganizationSelfServiceDefaultsPanel />);

    await screen.findByText("Organization default");
    fireEvent.change(screen.getByLabelText("Minimum shifts per cycle"), {
      target: { value: "9" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Minimum shifts must be less than or equal to maximum shifts."
    );
    expect(mockUpdateOrganizationSelfServiceDefaults).not.toHaveBeenCalled();
  });
});
