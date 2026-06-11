import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AdminOverridesTab from "../../app/groups/[groupId]/tabs/AdminOverridesTab";
import type { GroupMemberDto } from "../../lib/api/groups";

const mockGetAvailableSlots = vi.fn();
const mockGetAdminShiftSlotAssignments = vi.fn();
const mockAdminAssignMember = vi.fn();
const mockAdminRemoveMember = vi.fn();

const translations: Record<string, string> = {
  "selfService.error": "Error loading data",
  "selfService.adminOverrides.title": "Manual assignments",
  "selfService.adminOverrides.assignButton": "Assign member",
  "selfService.adminOverrides.selectMember": "Select member",
  "selfService.adminOverrides.assigning": "Assigning...",
  "selfService.adminOverrides.removeButton": "Remove",
  "selfService.adminOverrides.removing": "Removing...",
  "selfService.adminOverrides.confirmYes": "Confirm",
  "selfService.adminOverrides.confirmNo": "Cancel",
  "selfService.adminOverrides.noMembers": "No available members",
  "selfService.adminOverrides.removeConfirmTitle": "Remove assignment",
};

vi.mock("next-intl", () => ({
  useLocale: () => "en",
  useTranslations: (namespace: string) => (key: string, values?: Record<string, unknown>) => {
    if (`${namespace}.${key}` === "selfService.adminOverrides.removeConfirmMessage") {
      return `Remove ${values?.name ?? ""}?`;
    }

    return translations[`${namespace}.${key}`] ?? key;
  },
}));

vi.mock("@/lib/api/selfService", () => ({
  getAvailableSlots: (...args: unknown[]) => mockGetAvailableSlots(...args),
  getAdminShiftSlotAssignments: (...args: unknown[]) => mockGetAdminShiftSlotAssignments(...args),
  adminAssignMember: (...args: unknown[]) => mockAdminAssignMember(...args),
  adminRemoveMember: (...args: unknown[]) => mockAdminRemoveMember(...args),
}));

const members: GroupMemberDto[] = [
  {
    personId: "person-1",
    fullName: "Alice Admin",
    displayName: "Alice",
    isOwner: false,
    phoneNumber: null,
    email: null,
    invitationStatus: "Accepted",
    profileImageUrl: null,
    birthday: null,
    linkedUserId: "user-1",
    roleId: null,
    roleName: null,
  },
  {
    personId: "person-2",
    fullName: "Ben Backup",
    displayName: null,
    isOwner: false,
    phoneNumber: null,
    email: null,
    invitationStatus: "Accepted",
    profileImageUrl: null,
    birthday: null,
    linkedUserId: "user-2",
    roleId: null,
    roleName: null,
  },
];

describe("AdminOverridesTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAvailableSlots.mockResolvedValue({
      requestWindowOpen: true,
      requestWindowOpensAt: null,
      requestWindowClosesAt: "2026-06-19T00:00:00Z",
      slots: [
        {
          id: "slot-1",
          date: "2026-06-20",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Desk",
          capacity: 2,
          currentFillCount: 0,
        },
      ],
    });
    mockGetAdminShiftSlotAssignments.mockResolvedValue([]);
    mockAdminAssignMember.mockResolvedValue({ shiftRequestId: "request-1" });
    mockAdminRemoveMember.mockResolvedValue(undefined);
  });

  it("lets admins manually assign an available member to an unfilled slot", async () => {
    mockGetAvailableSlots
      .mockResolvedValueOnce({
        requestWindowOpen: true,
        requestWindowOpensAt: null,
        requestWindowClosesAt: "2026-06-19T00:00:00Z",
        slots: [
          {
            id: "slot-1",
            date: "2026-06-20",
            startTime: "09:00:00",
            endTime: "17:00:00",
            taskName: "Desk",
            capacity: 2,
            currentFillCount: 0,
          },
        ],
      })
      .mockResolvedValueOnce({
        requestWindowOpen: true,
        requestWindowOpensAt: null,
        requestWindowClosesAt: "2026-06-19T00:00:00Z",
        slots: [
          {
            id: "slot-1",
            date: "2026-06-20",
            startTime: "09:00:00",
            endTime: "17:00:00",
            taskName: "Desk",
            capacity: 2,
            currentFillCount: 1,
          },
        ],
      });
    mockGetAdminShiftSlotAssignments
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([
        {
          shiftSlotId: "slot-1",
          personId: "person-1",
          personName: "Alice",
        },
      ]);

    render(
      <AdminOverridesTab
        spaceId="space-1"
        groupId="group-1"
        members={members}
        hasSchedulePublishPermission
      />
    );

    fireEvent.click(await screen.findByRole("button", { name: "Assign member" }));
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "person-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => {
      expect(mockAdminAssignMember).toHaveBeenCalledWith("space-1", "group-1", "slot-1", "person-1");
    });
    await waitFor(() => {
      expect(mockGetAvailableSlots).toHaveBeenCalledTimes(2);
      expect(mockGetAdminShiftSlotAssignments).toHaveBeenCalledTimes(2);
    });
    expect(await screen.findByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("1/2")).toBeInTheDocument();
  });

  it("lets admins remove an existing manual assignment from a slot", async () => {
    mockGetAvailableSlots.mockResolvedValue({
      requestWindowOpen: true,
      requestWindowOpensAt: null,
      requestWindowClosesAt: "2026-06-19T00:00:00Z",
      slots: [
        {
          id: "slot-1",
          date: "2026-06-20",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Desk",
          capacity: 2,
          currentFillCount: 1,
        },
      ],
    });
    mockGetAvailableSlots
      .mockResolvedValueOnce({
        requestWindowOpen: true,
        requestWindowOpensAt: null,
        requestWindowClosesAt: "2026-06-19T00:00:00Z",
        slots: [
          {
            id: "slot-1",
            date: "2026-06-20",
            startTime: "09:00:00",
            endTime: "17:00:00",
            taskName: "Desk",
            capacity: 2,
            currentFillCount: 1,
          },
        ],
      })
      .mockResolvedValueOnce({
        requestWindowOpen: true,
        requestWindowOpensAt: null,
        requestWindowClosesAt: "2026-06-19T00:00:00Z",
        slots: [
          {
            id: "slot-1",
            date: "2026-06-20",
            startTime: "09:00:00",
            endTime: "17:00:00",
            taskName: "Desk",
            capacity: 2,
            currentFillCount: 0,
          },
        ],
      });
    mockGetAdminShiftSlotAssignments
      .mockResolvedValueOnce([
        {
          shiftSlotId: "slot-1",
          personId: "person-1",
          personName: "Alice",
        },
      ])
      .mockResolvedValueOnce([]);

    render(
      <AdminOverridesTab
        spaceId="space-1"
        groupId="group-1"
        members={members}
        hasSchedulePublishPermission
      />
    );

    fireEvent.click(await screen.findByRole("button", { name: "Remove" }));
    fireEvent.click(await screen.findByRole("button", { name: "Confirm" }));

    await waitFor(() => {
      expect(mockAdminRemoveMember).toHaveBeenCalledWith("space-1", "group-1", "slot-1", "person-1");
    });
    await waitFor(() => {
      expect(mockGetAvailableSlots).toHaveBeenCalledTimes(2);
      expect(mockGetAdminShiftSlotAssignments).toHaveBeenCalledTimes(2);
    });
    expect(screen.queryByText("Alice")).not.toBeInTheDocument();
    expect(screen.getByText("0/2")).toBeInTheDocument();
  });

  it("refreshes assignments after an admin assignment fails", async () => {
    mockAdminAssignMember.mockRejectedValue({
      response: { data: { detail: "The member is already assigned to this shift slot." } },
    });
    mockGetAvailableSlots
      .mockResolvedValueOnce({
        requestWindowOpen: true,
        requestWindowOpensAt: null,
        requestWindowClosesAt: "2026-06-19T00:00:00Z",
        slots: [
          {
            id: "slot-1",
            date: "2026-06-20",
            startTime: "09:00:00",
            endTime: "17:00:00",
            taskName: "Desk",
            capacity: 2,
            currentFillCount: 0,
          },
        ],
      })
      .mockResolvedValueOnce({
        requestWindowOpen: true,
        requestWindowOpensAt: null,
        requestWindowClosesAt: "2026-06-19T00:00:00Z",
        slots: [
          {
            id: "slot-1",
            date: "2026-06-20",
            startTime: "09:00:00",
            endTime: "17:00:00",
            taskName: "Desk",
            capacity: 2,
            currentFillCount: 1,
          },
        ],
      });
    mockGetAdminShiftSlotAssignments
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([
        {
          shiftSlotId: "slot-1",
          personId: "person-1",
          personName: "Alice",
        },
      ]);

    render(
      <AdminOverridesTab
        spaceId="space-1"
        groupId="group-1"
        members={members}
        hasSchedulePublishPermission
      />
    );

    fireEvent.click(await screen.findByRole("button", { name: "Assign member" }));
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "person-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirm" }));

    expect(await screen.findByText("The member is already assigned to this shift slot.")).toBeInTheDocument();
    expect(await screen.findByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("1/2")).toBeInTheDocument();
    expect(mockGetAdminShiftSlotAssignments).toHaveBeenCalledTimes(2);
  });
});
