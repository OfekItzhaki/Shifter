/**
 * Unit tests for RoleAssignmentCard component.
 *
 * Verifies:
 * - Renders all members with their current permission levels (Req 4.6)
 * - Changing a dropdown calls assignSpaceRole with correct spaceId, userId, and level (Req 4.6)
 * - Shows success/error toast after API call (Req 4.6)
 * - Returns null when isOwner is false (Req 4.6)
 *
 * Requirements: 4.6
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import RoleAssignmentCard from "../../../components/spaces/RoleAssignmentCard";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockAssignSpaceRole = vi.fn();
const mockGetSpaceMembers = vi.fn();
const mockGetSpacePermissionLevels = vi.fn();

vi.mock("@/lib/api/spaces", () => ({
  assignSpaceRole: (...args: any[]) => mockAssignSpaceRole(...args),
  getSpaceMembers: (...args: any[]) => mockGetSpaceMembers(...args),
  getSpacePermissionLevels: (...args: any[]) => mockGetSpacePermissionLevels(...args),
  SpacePermissionLevel: {
    Member: 0,
    Admin: 1,
    GroupOwner: 2,
    SpaceOwner: 3,
  },
  SpaceMemberDto: {},
  SpacePermissionLevelDto: {},
}));

// Stable translation function reference to avoid useCallback re-creation
const tFn = (key: string, params?: Record<string, string>) => {
  const translations: Record<string, string> = {
    "roleAssignment.title": "Role Assignment",
    "roleAssignment.description": "Assign permission levels to space members.",
    "roleAssignment.loading": "Loading...",
    "roleAssignment.loadError": "Failed to load members.",
    "roleAssignment.retry": "Retry",
    "roleAssignment.saved": "Role saved successfully.",
    "roleAssignment.saveError": "Failed to save role.",
    "roleAssignment.noMembers": "No members found.",
    "roleAssignment.levels.spaceOwner": "Space Owner",
    "roleAssignment.levels.groupOwner": "Group Owner",
    "roleAssignment.levels.admin": "Admin",
    "roleAssignment.levels.member": "Member",
  };
  if (key === "roleAssignment.selectLabel" && params?.name) {
    return `Select role for ${params.name}`;
  }
  return translations[key] ?? key;
};

vi.mock("next-intl", () => ({
  useTranslations: () => tFn,
}));

// ── Test Data ─────────────────────────────────────────────────────────────────

const mockMembers = [
  { userId: "owner-1", displayName: "Owner User", email: "owner@test.com", joinedAt: "2025-01-01T00:00:00Z" },
  { userId: "member-2", displayName: "Alice", email: "alice@test.com", joinedAt: "2025-01-02T00:00:00Z" },
  { userId: "member-3", displayName: "Bob", email: "bob@test.com", joinedAt: "2025-01-03T00:00:00Z" },
];

const mockPermissions = [
  { userId: "owner-1", permissionLevel: 3 }, // SpaceOwner
  { userId: "member-2", permissionLevel: 1 }, // Admin
  { userId: "member-3", permissionLevel: 0 }, // Member
];

const defaultProps = {
  spaceId: "space-123",
  isOwner: true,
};

// Helper to render and wait for data to load
async function renderAndWaitForLoad(props = defaultProps) {
  let result: ReturnType<typeof render>;
  await act(async () => {
    result = render(<RoleAssignmentCard {...props} />);
  });
  await waitFor(() => {
    expect(screen.queryByText("Loading...")).not.toBeInTheDocument();
  });
  return result!;
}

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("RoleAssignmentCard (Task 17.2)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSpaceMembers.mockResolvedValue(mockMembers);
    mockGetSpacePermissionLevels.mockResolvedValue(mockPermissions);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 4.6: Hidden for non-owners", () => {
    it("renders nothing when isOwner is false", () => {
      const { container } = render(
        <RoleAssignmentCard {...defaultProps} isOwner={false} />
      );

      expect(container.innerHTML).toBe("");
    });

    it("renders the role assignment card when isOwner is true", async () => {
      await renderAndWaitForLoad();

      expect(screen.getByText("Role Assignment")).toBeInTheDocument();
    });
  });

  describe("Req 4.6: Renders all members with their current permission levels", () => {
    it("displays all members after loading", async () => {
      await renderAndWaitForLoad();

      expect(screen.getByText("Owner User")).toBeInTheDocument();
      expect(screen.getByText("Alice")).toBeInTheDocument();
      expect(screen.getByText("Bob")).toBeInTheDocument();
    });

    it("shows loading state initially", () => {
      // Use a never-resolving promise to keep loading state
      mockGetSpaceMembers.mockReturnValue(new Promise(() => {}));
      mockGetSpacePermissionLevels.mockReturnValue(new Promise(() => {}));

      render(<RoleAssignmentCard {...defaultProps} />);

      expect(screen.getByText("Loading...")).toBeInTheDocument();
    });

    it("displays SpaceOwner as a badge (not a dropdown)", async () => {
      await renderAndWaitForLoad();

      expect(screen.getByText("Space Owner")).toBeInTheDocument();

      // The space owner should not have a dropdown select — only 2 dropdowns for Alice and Bob
      const selects = screen.getAllByRole("combobox");
      expect(selects).toHaveLength(2);
    });

    it("shows correct permission level in dropdowns for non-owner members", async () => {
      await renderAndWaitForLoad();

      const selects = screen.getAllByRole("combobox");
      // Sorted by permission level descending: Alice (Admin=1) first, then Bob (Member=0)
      expect(selects[0]).toHaveValue("1"); // Alice - Admin
      expect(selects[1]).toHaveValue("0"); // Bob - Member
    });

    it("displays member emails", async () => {
      await renderAndWaitForLoad();

      expect(screen.getByText("alice@test.com")).toBeInTheDocument();
      expect(screen.getByText("bob@test.com")).toBeInTheDocument();
    });
  });

  describe("Req 4.6: Dropdown dispatches correct API call", () => {
    it("calls assignSpaceRole with correct spaceId, userId, and level on change", async () => {
      mockAssignSpaceRole.mockResolvedValue(undefined);
      await renderAndWaitForLoad();

      const selects = screen.getAllByRole("combobox");
      // Change Alice's role from Admin (1) to GroupOwner (2)
      await act(async () => {
        fireEvent.change(selects[0], { target: { value: "2" } });
      });

      await waitFor(() => {
        expect(mockAssignSpaceRole).toHaveBeenCalledWith("space-123", "member-2", 2);
      });
    });

    it("calls assignSpaceRole with Member level when downgrading", async () => {
      mockAssignSpaceRole.mockResolvedValue(undefined);
      await renderAndWaitForLoad();

      const selects = screen.getAllByRole("combobox");
      // Change Alice's role from Admin (1) to Member (0)
      await act(async () => {
        fireEvent.change(selects[0], { target: { value: "0" } });
      });

      await waitFor(() => {
        expect(mockAssignSpaceRole).toHaveBeenCalledWith("space-123", "member-2", 0);
      });
    });
  });

  describe("Req 4.6: Shows success/error toast after API call", () => {
    it("shows success toast after successful role assignment", async () => {
      mockAssignSpaceRole.mockResolvedValue(undefined);
      await renderAndWaitForLoad();

      const selects = screen.getAllByRole("combobox");
      await act(async () => {
        fireEvent.change(selects[0], { target: { value: "2" } });
      });

      await waitFor(() => {
        expect(screen.getByText("Role saved successfully.")).toBeInTheDocument();
      });
    });

    it("shows error toast when role assignment fails", async () => {
      mockAssignSpaceRole.mockRejectedValue(new Error("Network error"));
      await renderAndWaitForLoad();

      const selects = screen.getAllByRole("combobox");
      await act(async () => {
        fireEvent.change(selects[0], { target: { value: "2" } });
      });

      await waitFor(() => {
        expect(screen.getByText("Failed to save role.")).toBeInTheDocument();
      });
    });

    it("updates local state after successful assignment", async () => {
      mockAssignSpaceRole.mockResolvedValue(undefined);
      await renderAndWaitForLoad();

      const selects = screen.getAllByRole("combobox");
      // Change Alice from Admin (1) to GroupOwner (2)
      await act(async () => {
        fireEvent.change(selects[0], { target: { value: "2" } });
      });

      await waitFor(() => {
        // After successful save, the dropdown should reflect the new value
        expect(selects[0]).toHaveValue("2");
      });
    });

    it("shows error state when loading members fails", async () => {
      mockGetSpaceMembers.mockRejectedValue(new Error("Load error"));

      await act(async () => {
        render(<RoleAssignmentCard {...defaultProps} />);
      });

      await waitFor(() => {
        expect(screen.getByText("Failed to load members.")).toBeInTheDocument();
      });

      expect(screen.getByText("Retry")).toBeInTheDocument();
    });
  });
});
