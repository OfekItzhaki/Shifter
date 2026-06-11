import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import AbsenceReportsTab from "../../components/groups/selfService/AbsenceReportsTab";

const mockGetAbsenceReports = vi.fn();
const mockGetShiftChangeRequests = vi.fn();
const mockGetShiftChangeTargetSlots = vi.fn();
const mockApproveAbsenceReport = vi.fn();
const mockRejectAbsenceReport = vi.fn();
const mockApproveShiftChangeRequest = vi.fn();
const mockRejectShiftChangeRequest = vi.fn();
const mockGetAdminSpecialLeaveRequests = vi.fn();
const mockApproveSpecialLeaveRequest = vi.fn();
const mockRejectSpecialLeaveRequest = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    const translations: Record<string, string> = {
      title: "Review Requests",
      refresh: "Refresh",
      filterLabel: "Show",
      filterAria: "Review queue filter",
      "filter.pending": "Pending",
      "filter.all": "All",
      "filter.handled": "Handled",
      absenceReportsTitle: "Absence Reports",
      changeRequestsTitle: "Shift Change Requests",
      leaveRequestsTitle: "Time-off Requests",
      pendingSummary: `${values?.pending ?? 0} pending / ${values?.total ?? 0} total`,
      activityTitle: "Recent review activity",
      activityDescription: "Latest handled self-service requests across absence, shift changes, and time off.",
      activityCount: `${values?.count ?? 0} recent`,
      activityEmpty: "No reviewed or cancelled requests yet.",
      activityKindabsence: "Absence",
      activityKindchange: "Shift change",
      activityKindleave: "Time off",
      statusPending: "Pending",
      statusApproved: "Approved",
      statusRejected: "Rejected",
      statusCancelled: "Cancelled",
      empty: "No absence reports",
      emptyPending: "No pending absence reports",
      absenceReleasedNotice: "This shift has already been released for coverage.",
      changeRequestsEmpty: "No shift change requests",
      changeRequestsEmptyPending: "No pending shift change requests",
      leaveRequestsEmpty: "No time-off requests",
      leaveRequestsEmptyPending: "No pending time-off requests",
      adminNote: "Admin note",
      adminNotePlaceholder: "Admin note...",
      approve: "Approve",
      reject: "Reject",
      approving: "Approving...",
      rejecting: "Rejecting...",
      changeFrom: "From",
      changeTo: "To",
      changeFlexibleTarget: "Flexible replacement",
      changeTargetShift: "Target shift",
      changeTargetRequired: "Choose a target shift to approve",
    };
    return translations[key] ?? key;
  },
}));

vi.mock("../../lib/api/selfService", () => ({
  getAbsenceReports: (...args: unknown[]) => mockGetAbsenceReports(...args),
  getShiftChangeRequests: (...args: unknown[]) => mockGetShiftChangeRequests(...args),
  getShiftChangeTargetSlots: (...args: unknown[]) => mockGetShiftChangeTargetSlots(...args),
  approveAbsenceReport: (...args: unknown[]) => mockApproveAbsenceReport(...args),
  rejectAbsenceReport: (...args: unknown[]) => mockRejectAbsenceReport(...args),
  approveShiftChangeRequest: (...args: unknown[]) => mockApproveShiftChangeRequest(...args),
  rejectShiftChangeRequest: (...args: unknown[]) => mockRejectShiftChangeRequest(...args),
}));

vi.mock("../../lib/api/specialLeave", () => ({
  getAdminSpecialLeaveRequests: (...args: unknown[]) => mockGetAdminSpecialLeaveRequests(...args),
  approveSpecialLeaveRequest: (...args: unknown[]) => mockApproveSpecialLeaveRequest(...args),
  rejectSpecialLeaveRequest: (...args: unknown[]) => mockRejectSpecialLeaveRequest(...args),
}));

describe("AbsenceReportsTab", () => {
  it("defaults admin review queues to pending and can switch to all or handled requests", async () => {
    mockGetAbsenceReports.mockResolvedValue([
      makeAbsenceReport("absence-pending", "Pending", "Pending absence reason"),
      makeAbsenceReport("absence-late", "Pending", "Late absence reason", true),
      makeAbsenceReport("absence-approved", "Approved", "Approved absence reason"),
    ]);
    mockGetShiftChangeRequests.mockResolvedValue([
      makeShiftChangeRequest("change-pending", "Pending", "Pending change reason"),
      makeShiftChangeRequest("change-rejected", "Rejected", "Rejected change reason"),
    ]);
    mockGetShiftChangeTargetSlots.mockResolvedValue([]);
    mockGetAdminSpecialLeaveRequests.mockResolvedValue([
      makeLeaveRequest("leave-pending", "Pending", "Pending leave reason"),
      makeLeaveRequest("leave-cancelled", "Cancelled", "Cancelled leave reason"),
    ]);

    render(<AbsenceReportsTab spaceId="space-1" groupId="group-1" />);

    await waitFor(() => expect(screen.getByText("Pending absence reason")).toBeInTheDocument());
    expect(screen.getByText("Late absence reason")).toBeInTheDocument();
    expect(screen.getAllByText("This shift has already been released for coverage.")).toHaveLength(2);
    expect(screen.getByText("Late absence reason").compareDocumentPosition(screen.getByText("Pending absence reason")))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(screen.getByText("Pending change reason")).toBeInTheDocument();
    expect(screen.getByText("Pending leave reason")).toBeInTheDocument();
    expect(screen.queryByText("Approved absence reason")).not.toBeInTheDocument();
    expect(screen.queryByText("Rejected change reason")).not.toBeInTheDocument();
    expect(screen.queryByText("Cancelled leave reason")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    expect(screen.getByText("Approved absence reason")).toBeInTheDocument();
    expect(screen.getAllByText("This shift has already been released for coverage.")).toHaveLength(2);
    expect(screen.getByText("Rejected change reason")).toBeInTheDocument();
    expect(screen.getByText("Cancelled leave reason")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Handled" }));
    expect(screen.queryByText("Pending absence reason")).not.toBeInTheDocument();
    expect(screen.queryByText("Pending change reason")).not.toBeInTheDocument();
    expect(screen.queryByText("Pending leave reason")).not.toBeInTheDocument();
    expect(screen.getByText("Approved absence reason")).toBeInTheDocument();
    expect(screen.getByText("Rejected change reason")).toBeInTheDocument();
    expect(screen.getByText("Cancelled leave reason")).toBeInTheDocument();
  });
});

function makeAbsenceReport(id: string, status: "Pending" | "Approved" | "Rejected", reason: string, isLate = false) {
  return {
    id,
    shiftRequestId: `${id}-request`,
    personId: "person-1",
    personName: "Member One",
    shiftSlotId: `${id}-slot`,
    date: "2026-06-12",
    startTime: "08:00:00",
    endTime: "16:00:00",
    taskName: "Desk",
    reason,
    isLate,
    status,
    reportedAt: "2026-06-10T08:00:00Z",
    adminNote: null,
    reviewedAt: status === "Pending" ? null : "2026-06-10T09:00:00Z",
  };
}

function makeShiftChangeRequest(id: string, status: "Pending" | "Rejected", reason: string) {
  return {
    id,
    shiftRequestId: `${id}-request`,
    personId: "person-2",
    personName: "Member Two",
    originalShiftSlotId: `${id}-original`,
    originalSlotDate: "2026-06-13",
    originalSlotStartTime: "08:00:00",
    originalSlotEndTime: "16:00:00",
    originalTaskName: "Gate",
    requestedShiftSlotId: null,
    requestedSlotDate: null,
    requestedSlotStartTime: null,
    requestedSlotEndTime: null,
    requestedTaskName: null,
    reason,
    status,
    requestedAt: "2026-06-10T08:00:00Z",
    adminNote: null,
    reviewedAt: status === "Pending" ? null : "2026-06-10T09:00:00Z",
  };
}

function makeLeaveRequest(id: string, status: "Pending" | "Cancelled", reason: string) {
  return {
    id,
    personId: "person-3",
    personName: "Member Three",
    startsAt: "2026-06-14T08:00:00Z",
    endsAt: "2026-06-14T16:00:00Z",
    reason,
    status,
    requestedByUserId: "user-3",
    requestedAt: "2026-06-10T08:00:00Z",
    processedByUserId: null,
    processedAt: null,
    adminNote: null,
    presenceWindowId: null,
    updatedAt: "2026-06-10T09:00:00Z",
  };
}
