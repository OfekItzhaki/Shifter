import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SelfServiceConfigTab from "../../components/groups/selfService/SelfServiceConfigTab";

const mockGetSelfServiceConfig = vi.fn();
const mockUpdateSelfServiceConfig = vi.fn();

function makeConfig(overrides: Record<string, unknown> = {}) {
  return {
    id: "config-1",
    groupId: "group-1",
    minShiftsPerCycle: 2,
    maxShiftsPerCycle: 5,
    requestWindowOpenOffsetHours: 168,
    requestWindowCloseOffsetHours: 24,
    cancellationCutoffHours: 12,
    maxLateCancellationsPerCycle: 2,
    lateCancellationWindowHours: 24,
    waitlistOfferMinutes: 60,
    cycleDurationDays: 7,
    ...overrides,
  };
}

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    const translations: Record<string, string> = {
      title: "Self-Service Configuration",
      description: "Set the rules that control member self-service.",
      minShiftsPerCycle: "Minimum shifts per cycle",
      maxShiftsPerCycle: "Maximum shifts per cycle",
      requestWindowOpenOffsetHours: "Request window open",
      requestWindowCloseOffsetHours: "Request window close",
      cancellationCutoffHours: "Cancellation cutoff",
      maxLateCancellationsPerCycle: "Late absence limit",
      lateCancellationWindowHours: "Late absence window",
      waitlistOfferMinutes: "Waitlist offer duration",
      cycleDurationDays: "Cycle duration",
      range: `${values?.min}-${values?.max}`,
      "units.shifts": "shifts",
      "units.hours": "hours",
      "units.minutes": "minutes",
      "units.days": "days",
      "units.reports": "reports",
      "summary.shiftLimits": "Shift limits",
      "summary.shiftLimitsValue": `${values?.min}-${values?.max} shifts / ${values?.days} days`,
      "summary.requestWindow": "Request window",
      "summary.requestWindowValue": `Opens ${values?.open}h before, closes ${values?.close}h before`,
      "summary.absence": "Changes and absence",
      "summary.absenceValue": `Cancel until ${values?.cutoff}h, late inside ${values?.lateWindow}h, max ${values?.max}`,
      "summary.waitlist": "Waitlist",
      "summary.waitlistValue": `${values?.minutes}m offer timer`,
      recommended: `Recommended: ${values?.value}`,
      "recommendations.minShiftsPerCycle": `${values?.value} shifts`,
      "recommendations.maxShiftsPerCycle": "Based on team size",
      "recommendations.cycleDurationDays": `${values?.value} days`,
      "recommendations.requestWindowOpenOffsetHours": `${values?.value} hours`,
      "recommendations.requestWindowCloseOffsetHours": `${values?.value} hours`,
      "recommendations.cancellationCutoffHours": `${values?.value} hours`,
      "recommendations.lateCancellationWindowHours": `${values?.value} hours`,
      "recommendations.maxLateCancellationsPerCycle": `${values?.value} reports`,
      "recommendations.waitlistOfferMinutes": `${values?.value} minutes`,
      "insights.title": "Policy impact",
      "insights.balanced": "This policy gives members a normal self-service window.",
      "insights.noMinimum": "No minimum shift requirement is enforced.",
      "insights.noLateReports": "Members cannot submit late absence reports.",
      "insights.strictLateReports": "Only one late absence report is allowed.",
      "insights.requestClosesBeforeCancellation": "Members may still cancel after picking closes.",
      "insights.lateWindowLongerThanCancel": "Some shifts become late immediately after cancellation closes.",
      "insights.shortWaitlistOffer": "Waitlist offers under 30 minutes may expire before members notice them.",
      "sections.shiftLimits.title": "Shift limits",
      "sections.shiftLimits.description": "Control how many shifts members should take.",
      "sections.requestWindow.title": "Request window",
      "sections.requestWindow.description": "Decide when members can pick shifts.",
      "sections.changesAbsence.title": "Changes and absence",
      "sections.changesAbsence.description": "Set absence and cancellation rules.",
      "sections.waitlist.title": "Waitlist",
      "sections.waitlist.description": "Control waitlist offer timing.",
      "descriptions.minShiftsPerCycle": "Members below this count are flagged.",
      "descriptions.maxShiftsPerCycle": "Members cannot pick more than this.",
      "descriptions.cycleDurationDays": "How many days each cycle covers.",
      "descriptions.requestWindowOpenOffsetHours": "How many hours before cycle start picking opens.",
      "descriptions.requestWindowCloseOffsetHours": "How many hours before cycle start picking closes.",
      "descriptions.cancellationCutoffHours": "Approved shifts cannot be cancelled inside this cutoff.",
      "descriptions.lateCancellationWindowHours": "Absence reports inside this window count as late.",
      "descriptions.maxLateCancellationsPerCycle": "Maximum late absence reports per member.",
      "descriptions.waitlistOfferMinutes": "How long an offered shift is held.",
      "validation.minShiftsRange": "Minimum shifts must be between 0 and 100.",
      "validation.maxShiftsRange": "Maximum shifts must be between 1 and 100.",
      "validation.minGreaterThanMax": "Minimum shifts cannot be greater than maximum shifts.",
      "validation.offsetRange": "Offsets must be between 1 and 720 hours.",
      "validation.openCloseOrder": "Request opening must be earlier than request closing.",
      "validation.maxLateRange": "Late absence limit must be between 0 and 100.",
      "validation.waitlistOfferRange": "Waitlist offers must last between 15 and 1440 minutes.",
      "validation.cycleDurationRange": "Cycle duration must be between 1 and 30 days.",
      save: "Save Configuration",
      saving: "Saving...",
      saved: "Configuration saved successfully",
    };
    return translations[key] ?? key;
  },
}));

vi.mock("../../lib/api/selfService", () => ({
  getSelfServiceConfig: (...args: unknown[]) => mockGetSelfServiceConfig(...args),
  updateSelfServiceConfig: (...args: unknown[]) => mockUpdateSelfServiceConfig(...args),
}));

describe("SelfServiceConfigTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSelfServiceConfig.mockResolvedValue(makeConfig());
    mockUpdateSelfServiceConfig.mockResolvedValue(makeConfig());
  });

  it("groups policy settings and updates the live summary", async () => {
    render(<SelfServiceConfigTab spaceId="space-1" groupId="group-1" />);

    expect(await screen.findByText("Self-Service Configuration")).toBeInTheDocument();
    expect(screen.getAllByText("Shift limits").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Request window").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Changes and absence").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Waitlist").length).toBeGreaterThan(0);
    expect(screen.getByText("2-5 shifts / 7 days")).toBeInTheDocument();
    expect(screen.getByText("Opens 168h before, closes 24h before")).toBeInTheDocument();
    expect(screen.getByText("Recommended: 1-2 shifts")).toBeInTheDocument();
    expect(screen.getByText("Recommended: 30-60 minutes")).toBeInTheDocument();
    expect(screen.getByText("Policy impact")).toBeInTheDocument();
    expect(screen.getByText("Members may still cancel after picking closes.")).toBeInTheDocument();
    expect(
      screen.getByText("Some shifts become late immediately after cancellation closes."),
    ).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Maximum shifts per cycle"), {
      target: { value: "6" },
    });

    expect(screen.getByText("2-6 shifts / 7 days")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Cancellation cutoff"), {
      target: { value: "36" },
    });

    expect(screen.getByText("This policy gives members a normal self-service window.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Save Configuration" }));

    await waitFor(() => {
      expect(mockUpdateSelfServiceConfig).toHaveBeenCalledWith(
        "space-1",
        "group-1",
        expect.objectContaining({ maxShiftsPerCycle: 6, cancellationCutoffHours: 36 }),
      );
    });
  });

  it("uses the saved server config after saving", async () => {
    mockUpdateSelfServiceConfig.mockResolvedValue(makeConfig({
      maxShiftsPerCycle: 7,
      cancellationCutoffHours: 48,
    }));

    render(<SelfServiceConfigTab spaceId="space-1" groupId="group-1" />);

    expect(await screen.findByText("Self-Service Configuration")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Maximum shifts per cycle"), {
      target: { value: "6" },
    });
    fireEvent.change(screen.getByLabelText("Cancellation cutoff"), {
      target: { value: "36" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save Configuration" }));

    expect(await screen.findByText("Configuration saved successfully")).toBeInTheDocument();
    expect(screen.getByText("2-7 shifts / 7 days")).toBeInTheDocument();
    expect(screen.getByText("Cancel until 48h, late inside 24h, max 2")).toBeInTheDocument();
  });

  it("refreshes current config after a stale save failure", async () => {
    mockGetSelfServiceConfig
      .mockResolvedValueOnce(makeConfig())
      .mockResolvedValueOnce(makeConfig({
        maxShiftsPerCycle: 4,
        cancellationCutoffHours: 18,
      }));
    mockUpdateSelfServiceConfig.mockRejectedValue({
      response: {
        status: 409,
        data: { detail: "Self-service configuration changed. Reload and try again." },
      },
    });

    render(<SelfServiceConfigTab spaceId="space-1" groupId="group-1" />);

    expect(await screen.findByText("Self-Service Configuration")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Maximum shifts per cycle"), {
      target: { value: "6" },
    });
    fireEvent.change(screen.getByLabelText("Cancellation cutoff"), {
      target: { value: "36" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save Configuration" }));

    expect(await screen.findByText("Self-service configuration changed. Reload and try again.")).toBeInTheDocument();
    await waitFor(() => {
      expect(mockGetSelfServiceConfig).toHaveBeenCalledTimes(2);
    });
    expect(screen.getByText("2-4 shifts / 7 days")).toBeInTheDocument();
    expect(screen.getByText("Cancel until 18h, late inside 24h, max 2")).toBeInTheDocument();
  });

  it("blocks invalid policy settings before saving", async () => {
    render(<SelfServiceConfigTab spaceId="space-1" groupId="group-1" />);

    expect(await screen.findByText("Self-Service Configuration")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Minimum shifts per cycle"), {
      target: { value: "6" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save Configuration" }));

    expect(await screen.findByText("Minimum shifts cannot be greater than maximum shifts.")).toBeInTheDocument();
    expect(mockUpdateSelfServiceConfig).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText("Minimum shifts per cycle"), {
      target: { value: "2" },
    });
    fireEvent.change(screen.getByLabelText("Request window open"), {
      target: { value: "12" },
    });
    fireEvent.change(screen.getByLabelText("Request window close"), {
      target: { value: "24" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save Configuration" }));

    expect(await screen.findByText("Request opening must be earlier than request closing.")).toBeInTheDocument();
    expect(mockUpdateSelfServiceConfig).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText("Request window open"), {
      target: { value: "168" },
    });
    fireEvent.change(screen.getByLabelText("Waitlist offer duration"), {
      target: { value: "10" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save Configuration" }));

    expect(await screen.findByText("Waitlist offers must last between 15 and 1440 minutes.")).toBeInTheDocument();
    expect(mockUpdateSelfServiceConfig).not.toHaveBeenCalled();
  });
});
