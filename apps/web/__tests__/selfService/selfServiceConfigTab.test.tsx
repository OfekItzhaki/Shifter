import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SelfServiceConfigTab from "../../components/groups/selfService/SelfServiceConfigTab";

const mockGetSelfServiceConfig = vi.fn();
const mockUpdateSelfServiceConfig = vi.fn();

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
    mockGetSelfServiceConfig.mockResolvedValue({
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
    });
    mockUpdateSelfServiceConfig.mockResolvedValue({});
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

    fireEvent.change(screen.getByLabelText("Maximum shifts per cycle"), {
      target: { value: "6" },
    });

    expect(screen.getByText("2-6 shifts / 7 days")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Save Configuration" }));

    await waitFor(() => {
      expect(mockUpdateSelfServiceConfig).toHaveBeenCalledWith(
        "space-1",
        "group-1",
        expect.objectContaining({ maxShiftsPerCycle: 6 }),
      );
    });
  });
});
