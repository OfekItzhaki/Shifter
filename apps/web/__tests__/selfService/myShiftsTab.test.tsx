import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import MyShiftsTab from "../../components/groups/selfService/MyShiftsTab";

const mockGetMyShiftRequests = vi.fn();
const mockGetMySpecialLeaveRequests = vi.fn();
const mockGetMyShiftChangeRequests = vi.fn();
const mockGetMyAbsenceReports = vi.fn();
const mockGetMyWaitlistEntries = vi.fn();
const mockGetAvailableSlots = vi.fn();
const mockCancelShiftRequest = vi.fn();
const mockReportCannotAttend = vi.fn();
const mockSubmitShiftChangeRequest = vi.fn();
const mockCancelShiftChangeRequest = vi.fn();
const mockSubmitSpecialLeaveRequest = vi.fn();
const mockCancelSpecialLeaveRequest = vi.fn();

vi.mock("next-intl", () => ({
  useLocale: () => "en",
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    const translations: Record<string, string> = {
      summaryTitle: "Shift summary",
      summaryDescription: "Your self-service status",
      summaryMinimumLabel: "Minimum",
      summaryMinimumValue: `${values?.current ?? 0}/${values?.min ?? 0}/${values?.max ?? 0}`,
      summaryRequestsLabel: "Requests",
      summaryRequestsValue: `${values?.leave ?? 0} leave / ${values?.changes ?? 0} changes / ${values?.absences ?? 0} absences`,
      summaryWaitlistLabel: "Waitlist",
      summaryWaitlistValue: `${values?.offered ?? 0} offered / ${values?.waiting ?? 0} waiting`,
      summaryLateAbsenceLabel: "Late absences",
      summaryLateAbsenceValue: `${values?.used ?? 0}/${values?.max ?? 0} used inside ${values?.window ?? 0}h`,
      summaryNextShiftLabel: "Next shift",
      actionGuideTitle: "Which action should I use?",
      actionGuideDescription: "Pick the right option for your shift situation.",
      "actionGuide.cancel.title": "Cancel",
      "actionGuide.cancel.description": `Use before the ${values?.cutoff ?? 0}h cutoff.`,
      "actionGuide.change.title": "Request change",
      "actionGuide.change.description": "Ask admins to move you to another shift.",
      "actionGuide.cannotAttend.title": "Can't make it",
      "actionGuide.cannotAttend.description": `${values?.remaining ?? 0}/${values?.max ?? 0} late reports left inside ${values?.window ?? 0}h.`,
      activityTitle: "My request history",
      activityDescription: "Recent self-service activity",
      activityCount: `${values?.count ?? 0} recent`,
      activityEmpty: "No self-service activity yet",
      activityKindShift: "Shift",
      activityKindLeave: "Time off",
      activityKindChange: "Change",
      activityKindAbsence: "Absence",
      activityKindWaitlist: "Waitlist",
      activityStatusWaiting: "Waiting",
      activityStatusOffered: "Offered",
      activityStatusAccepted: "Accepted",
      activityStatusExpired: "Expired",
      activityStatusDeclined: "Declined",
      activityStatusRemoved: "Removed",
      shiftCount: `${values?.current ?? 0} of ${values?.max ?? 0} shifts`,
      specialLeaveTitle: "Need time off?",
      specialLeaveDescription: "Send a request to admins",
      specialLeaveStart: "From",
      specialLeaveEnd: "Until",
      specialLeaveReason: "Reason",
      specialLeaveReasonPlaceholder: "Reason...",
      specialLeaveSubmit: "Send request",
      specialLeaveSubmitting: "Sending...",
      changeRequestsTitle: "Shift change requests",
      changeRequestsDescription: "Track requests",
      changeRequestsEmpty: "No change requests",
      changeRequestedTo: "Requested",
      changeFlexibleTarget: "Flexible replacement",
      changeCancel: "Cancel request",
      changeRequestCancelled: "Change request cancelled",
      changeDialogTitle: "Request Shift Change",
      changeDialogMessage: "Ask admins to move this shift.",
      changePreferredShift: "Preferred shift",
      changeSlotsLoading: "Loading shifts...",
      changeNoPreferredShift: "No preferred shift",
      changeReasonPlaceholder: "Why do you need to change this shift?",
      changeConfirm: "Send Change Request",
      changeSubmitting: "Sending...",
      changeRequestSubmitted: "Shift-change request sent to the admins.",
      cancelDialogTitle: "Cancel shift",
      cancelDialogMessage: "Tell admins why you are cancelling.",
      cancelReasonPlaceholder: "Why are you cancelling?",
      cancelConfirm: "Cancel shift",
      cancelling: "Cancelling...",
      cannotAttendDialogTitle: "Can't make it",
      cannotAttendDialogMessage: "Tell admins why you cannot attend.",
      cannotAttendReasonPlaceholder: "Why can't you make it?",
      cannotAttendConfirm: "Report absence",
      cannotAttendSubmitting: "Reporting...",
      cannotAttendLatePolicy: `${values?.remaining ?? 0}/${values?.max ?? 0} late reports remaining.`,
      cannotAttendNotLatePolicy: `This is outside the ${values?.window ?? 0}h late window.`,
      cannotAttendSubmitted: "Absence report sent to the admins.",
      cannotAttendLateSubmitted: `${values?.remaining ?? 0} late reports remaining.`,
      absenceReportsTitle: "Absence reports",
      absenceReportsDescription: `${values?.used ?? 0}/${values?.max ?? 0} late reports used`,
      absenceReportsEmpty: "No absence reports",
      specialLeaveSubmitted: "Time-off request sent to the admins.",
      specialLeaveCancelled: "Time-off request cancelled.",
      specialLeaveCancel: "Cancel time off",
      specialLeaveDateRequired: "Choose dates",
      specialLeaveInvalidRange: "End must be after start",
      specialLeaveReasonRequired: "Reason is required",
      specialLeaveReasonTooLong: "Reason is too long",
      approved: "Approved",
      pending: "Pending",
      cancelled: "Cancelled",
      rejected: "Rejected",
      cancelButton: "Cancel",
      cancelDismiss: "Go Back",
      changeButton: "Request change",
      cannotAttendButton: "Can't make it",
      cannotAttendLimitReached: "You have reached the late absence limit for this cycle.",
      adminOverride: "Admin override",
    };
    return translations[key] ?? key;
  },
}));

vi.mock("../../lib/api/selfService", () => ({
  getMyShiftRequests: (...args: unknown[]) => mockGetMyShiftRequests(...args),
  getMyShiftChangeRequests: (...args: unknown[]) => mockGetMyShiftChangeRequests(...args),
  getMyAbsenceReports: (...args: unknown[]) => mockGetMyAbsenceReports(...args),
  getMyWaitlistEntries: (...args: unknown[]) => mockGetMyWaitlistEntries(...args),
  getAvailableSlots: (...args: unknown[]) => mockGetAvailableSlots(...args),
  cancelShiftRequest: (...args: unknown[]) => mockCancelShiftRequest(...args),
  reportCannotAttend: (...args: unknown[]) => mockReportCannotAttend(...args),
  submitShiftChangeRequest: (...args: unknown[]) => mockSubmitShiftChangeRequest(...args),
  cancelShiftChangeRequest: (...args: unknown[]) => mockCancelShiftChangeRequest(...args),
}));

vi.mock("../../lib/api/specialLeave", () => ({
  getMySpecialLeaveRequests: (...args: unknown[]) => mockGetMySpecialLeaveRequests(...args),
  submitSpecialLeaveRequest: (...args: unknown[]) => mockSubmitSpecialLeaveRequest(...args),
  cancelSpecialLeaveRequest: (...args: unknown[]) => mockCancelSpecialLeaveRequest(...args),
}));

describe("MyShiftsTab", () => {
  beforeEach(() => {
    const lateShiftStart = new Date(Date.now() + 60 * 60 * 1000);
    const lateShiftEnd = new Date(lateShiftStart.getTime() + 8 * 60 * 60 * 1000);
    mockGetMySpecialLeaveRequests.mockResolvedValue([]);
    mockGetMyShiftChangeRequests.mockResolvedValue([]);
    mockGetMyWaitlistEntries.mockResolvedValue([]);
    mockGetAvailableSlots.mockResolvedValue({ slots: [] });
    mockGetMyAbsenceReports.mockResolvedValue({
      reports: [],
      lateReportsUsed: 2,
      maxLateReports: 2,
      schedulingCycleId: "cycle-1",
    });
    mockGetMyShiftRequests.mockResolvedValue({
      requests: [
        {
          id: "request-1",
          shiftSlotId: "slot-1",
          slotDate: formatLocalDate(lateShiftStart),
          slotStartTime: formatLocalTime(lateShiftStart),
          slotEndTime: formatLocalTime(lateShiftEnd),
          taskName: "Front desk",
          status: "Approved",
          isAdminOverride: false,
          rejectionReason: null,
          cancellationReason: null,
          cancelledAt: null,
          createdAt: "2026-06-10T08:00:00",
        },
      ],
      currentShiftCount: 1,
      minShiftsPerCycle: 1,
      maxShiftsPerCycle: 3,
      cancellationCutoffHours: 48,
      maxLateReports: 2,
      lateCancellationWindowHours: 24,
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("blocks late absence reporting when the member has reached the cycle limit", async () => {
    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    const cannotAttendButton = await screen.findByRole("button", { name: "Can't make it" });

    expect(cannotAttendButton).toBeDisabled();
    expect(cannotAttendButton).toHaveAttribute(
      "title",
      "You have reached the late absence limit for this cycle.",
    );
    expect(screen.getByText("2/2 used inside 24h")).toBeInTheDocument();
    expect(screen.getByText("Which action should I use?")).toBeInTheDocument();
    expect(screen.getByText("Use before the 48h cutoff.")).toBeInTheDocument();
    expect(screen.getByText("0/2 late reports left inside 24h.")).toBeInTheDocument();

    await waitFor(() => expect(mockReportCannotAttend).not.toHaveBeenCalled());
  });

  it("lets members cancel a pending shift-change request", async () => {
    mockCancelShiftChangeRequest.mockResolvedValue(undefined);
    mockGetMyShiftChangeRequests.mockResolvedValue([
      {
        id: "change-1",
        shiftRequestId: "request-1",
        personId: "person-1",
        personName: "Member One",
        originalShiftSlotId: "slot-1",
        originalSlotDate: "2026-06-12",
        originalSlotStartTime: "09:00:00",
        originalSlotEndTime: "17:00:00",
        originalTaskName: "Front desk",
        requestedShiftSlotId: null,
        requestedSlotDate: null,
        requestedSlotStartTime: null,
        requestedSlotEndTime: null,
        requestedTaskName: null,
        reason: "Need to swap school pickup",
        status: "Pending",
        requestedAt: "2026-06-10T08:00:00",
        adminNote: null,
        reviewedAt: null,
      },
    ]);

    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    await waitFor(() => {
      expect(screen.getAllByText("Need to swap school pickup").length).toBeGreaterThan(0);
    });
    fireEvent.click(screen.getByRole("button", { name: "Cancel request" }));

    await waitFor(() => {
      expect(mockCancelShiftChangeRequest).toHaveBeenCalledWith("space-1", "group-1", "change-1");
    });
    expect(await screen.findByText("Change request cancelled")).toBeInTheDocument();
    expect(mockGetMyShiftChangeRequests).toHaveBeenCalledTimes(2);
  });

  it("shows confirmation after submitting a shift-change request", async () => {
    mockSubmitShiftChangeRequest.mockResolvedValue(undefined);

    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Request change" }));
    fireEvent.change(screen.getByPlaceholderText("Why do you need to change this shift?"), {
      target: { value: "Need a different shift time" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Send Change Request" }));

    await waitFor(() => {
      expect(mockSubmitShiftChangeRequest).toHaveBeenCalledWith(
        "space-1",
        "group-1",
        "request-1",
        "Need a different shift time",
        null,
      );
    });
    expect(await screen.findByText("Shift-change request sent to the admins.")).toBeInTheDocument();
  });

  it("lets members cancel an approved shift before the cutoff", async () => {
    const futureShiftStart = new Date(Date.now() + 96 * 60 * 60 * 1000);
    const futureShiftEnd = new Date(futureShiftStart.getTime() + 8 * 60 * 60 * 1000);
    mockCancelShiftRequest.mockResolvedValue(undefined);
    mockGetMyShiftRequests.mockResolvedValue({
      requests: [
        {
          id: "request-2",
          shiftSlotId: "slot-2",
          slotDate: formatLocalDate(futureShiftStart),
          slotStartTime: formatLocalTime(futureShiftStart),
          slotEndTime: formatLocalTime(futureShiftEnd),
          taskName: "Kitchen",
          status: "Approved",
          isAdminOverride: false,
          rejectionReason: null,
          cancellationReason: null,
          cancelledAt: null,
          createdAt: "2026-06-10T08:00:00",
        },
      ],
      currentShiftCount: 1,
      minShiftsPerCycle: 1,
      maxShiftsPerCycle: 3,
      cancellationCutoffHours: 48,
      maxLateReports: 2,
      lateCancellationWindowHours: 24,
    });

    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Cancel" }));
    fireEvent.change(screen.getByPlaceholderText("Why are you cancelling?"), {
      target: { value: "Family appointment" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Cancel shift" }));

    await waitFor(() => {
      expect(mockCancelShiftRequest).toHaveBeenCalledWith(
        "space-1",
        "group-1",
        "request-2",
        "Family appointment",
      );
    });
    await waitFor(() => {
      expect(screen.queryByText("Tell admins why you are cancelling.")).not.toBeInTheDocument();
    });
    expect(mockGetMyShiftRequests).toHaveBeenCalledTimes(2);
  });

  it("lets members cancel inside the cutoff while the request window is still open", async () => {
    const soonShiftStart = new Date(Date.now() + 2 * 60 * 60 * 1000);
    const soonShiftEnd = new Date(soonShiftStart.getTime() + 8 * 60 * 60 * 1000);
    mockCancelShiftRequest.mockResolvedValue(undefined);
    mockGetMyShiftRequests.mockResolvedValue({
      requests: [
        {
          id: "request-window-open",
          shiftSlotId: "slot-window-open",
          slotDate: formatLocalDate(soonShiftStart),
          slotStartTime: formatLocalTime(soonShiftStart),
          slotEndTime: formatLocalTime(soonShiftEnd),
          taskName: "Desk",
          status: "Approved",
          isAdminOverride: false,
          rejectionReason: null,
          cancellationReason: null,
          cancelledAt: null,
          createdAt: "2026-06-10T08:00:00",
          requestWindowOpen: true,
        },
      ],
      currentShiftCount: 1,
      minShiftsPerCycle: 1,
      maxShiftsPerCycle: 3,
      cancellationCutoffHours: 48,
      maxLateReports: 2,
      lateCancellationWindowHours: 24,
    });

    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Cancel" }));
    fireEvent.change(screen.getByPlaceholderText("Why are you cancelling?"), {
      target: { value: "Mistaken pick" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Cancel shift" }));

    await waitFor(() => {
      expect(mockCancelShiftRequest).toHaveBeenCalledWith(
        "space-1",
        "group-1",
        "request-window-open",
        "Mistaken pick",
      );
    });
  });

  it("lets members report that they cannot attend a future shift", async () => {
    const shiftStart = new Date(Date.now() + 36 * 60 * 60 * 1000);
    const shiftEnd = new Date(shiftStart.getTime() + 8 * 60 * 60 * 1000);
    mockReportCannotAttend.mockResolvedValue({ isLate: false, lateReportsRemaining: 2 });
    mockGetMyAbsenceReports.mockResolvedValue({
      reports: [],
      lateReportsUsed: 0,
      maxLateReports: 2,
      schedulingCycleId: "cycle-1",
    });
    mockGetMyShiftRequests.mockResolvedValue({
      requests: [
        {
          id: "request-3",
          shiftSlotId: "slot-3",
          slotDate: formatLocalDate(shiftStart),
          slotStartTime: formatLocalTime(shiftStart),
          slotEndTime: formatLocalTime(shiftEnd),
          taskName: "Front desk",
          status: "Approved",
          isAdminOverride: false,
          rejectionReason: null,
          cancellationReason: null,
          cancelledAt: null,
          createdAt: "2026-06-10T08:00:00",
        },
      ],
      currentShiftCount: 1,
      minShiftsPerCycle: 1,
      maxShiftsPerCycle: 3,
      cancellationCutoffHours: 48,
      maxLateReports: 2,
      lateCancellationWindowHours: 24,
    });

    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    fireEvent.click(await screen.findByRole("button", { name: "Can't make it" }));
    fireEvent.change(screen.getByPlaceholderText("Why can't you make it?"), {
      target: { value: "Medical appointment" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Report absence" }));

    await waitFor(() => {
      expect(mockReportCannotAttend).toHaveBeenCalledWith(
        "space-1",
        "group-1",
        "request-3",
        "Medical appointment",
      );
    });
    expect(await screen.findByText("Absence report sent to the admins.")).toBeInTheDocument();
  });

  it("lets members submit and cancel time-off requests", async () => {
    mockSubmitSpecialLeaveRequest.mockResolvedValue({ id: "leave-new" });
    mockCancelSpecialLeaveRequest.mockResolvedValue(undefined);
    mockGetMySpecialLeaveRequests
      .mockResolvedValueOnce([
        {
          id: "leave-1",
          spaceId: "space-1",
          personId: "person-1",
          personName: "Member One",
          startsAt: "2026-06-20T08:00:00",
          endsAt: "2026-06-21T08:00:00",
          reason: "Family event",
          status: "Pending",
          requestedByUserId: "user-1",
          processedByUserId: null,
          processedAt: null,
          adminNote: null,
          presenceWindowId: null,
          createdAt: "2026-06-10T10:00:00",
          updatedAt: "2026-06-10T10:00:00",
        },
      ])
      .mockResolvedValueOnce([
        {
          id: "leave-1",
          spaceId: "space-1",
          personId: "person-1",
          personName: "Member One",
          startsAt: "2026-06-20T08:00:00",
          endsAt: "2026-06-21T08:00:00",
          reason: "Family event",
          status: "Pending",
          requestedByUserId: "user-1",
          processedByUserId: null,
          processedAt: null,
          adminNote: null,
          presenceWindowId: null,
          createdAt: "2026-06-10T10:00:00",
          updatedAt: "2026-06-10T10:00:00",
        },
      ]);

    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    fireEvent.change(await screen.findByLabelText("From"), {
      target: { value: "2026-06-25T09:00" },
    });
    fireEvent.change(screen.getByLabelText("Until"), {
      target: { value: "2026-06-26T09:00" },
    });
    fireEvent.change(screen.getByPlaceholderText("Reason..."), {
      target: { value: "Vacation" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Send request" }));

    await waitFor(() => {
      expect(mockSubmitSpecialLeaveRequest).toHaveBeenCalledWith("space-1", {
        startsAt: "2026-06-25T06:00:00.000Z",
        endsAt: "2026-06-26T06:00:00.000Z",
        reason: "Vacation",
      });
    });
    expect(await screen.findByText("Time-off request sent to the admins.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancel time off" }));

    await waitFor(() => {
      expect(mockCancelSpecialLeaveRequest).toHaveBeenCalledWith("space-1", "leave-1");
    });
    expect(await screen.findByText("Time-off request cancelled.")).toBeInTheDocument();
  });

  it("shows a unified recent self-service history", async () => {
    mockGetMySpecialLeaveRequests.mockResolvedValue([
      {
        id: "leave-1",
        spaceId: "space-1",
        personId: "person-1",
        personName: "Member One",
        startsAt: "2026-06-20T08:00:00",
        endsAt: "2026-06-21T08:00:00",
        reason: "Family event",
        status: "Approved",
        requestedByUserId: "user-1",
        processedByUserId: "admin-1",
        processedAt: "2026-06-11T10:00:00",
        adminNote: "Approved by manager",
        presenceWindowId: "window-1",
        createdAt: "2026-06-10T10:00:00",
        updatedAt: "2026-06-11T10:00:00",
      },
    ]);
    mockGetMyAbsenceReports.mockResolvedValue({
      reports: [
        {
          id: "absence-1",
          shiftRequestId: "request-1",
          personId: "person-1",
          personName: "Member One",
          shiftSlotId: "slot-1",
          date: "2026-06-12",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Front desk",
          reason: "Sick",
          isLate: true,
          status: "Pending",
          reportedAt: "2026-06-11T09:00:00",
          adminNote: null,
          reviewedAt: null,
        },
      ],
      lateReportsUsed: 1,
      maxLateReports: 2,
      schedulingCycleId: "cycle-1",
    });
    mockGetMyWaitlistEntries.mockResolvedValue([
      {
        id: "waitlist-1",
        shiftSlotId: "slot-2",
        slotDate: "2026-06-13",
        slotStartTime: "10:00:00",
        slotEndTime: "18:00:00",
        taskName: "Kitchen",
        position: 1,
        status: "Offered",
        offeredAt: "2026-06-11T11:00:00",
        expiresAt: "2026-06-11T12:00:00",
      },
    ]);

    render(<MyShiftsTab spaceId="space-1" groupId="group-1" />);

    expect(await screen.findByText("My request history")).toBeInTheDocument();
    expect(screen.getByText("Time off")).toBeInTheDocument();
    expect(screen.getByText("Approved by manager")).toBeInTheDocument();
    expect(screen.getByText("Absence")).toBeInTheDocument();
    expect(screen.getAllByText("Sick").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Waitlist").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Offered").length).toBeGreaterThan(0);
  });
});

function formatLocalDate(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function formatLocalTime(value: Date): string {
  const hours = String(value.getHours()).padStart(2, "0");
  const minutes = String(value.getMinutes()).padStart(2, "0");
  const seconds = String(value.getSeconds()).padStart(2, "0");
  return `${hours}:${minutes}:${seconds}`;
}
