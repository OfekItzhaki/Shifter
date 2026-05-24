/**
 * Unit tests for HomeLeaveConfigCard component.
 *
 * Verifies:
 * - Renders all three mode options (Automatic, Manual, Disabled)
 * - Shows ratio slider only in Automatic mode
 * - Shows manual fields (base days, home days, min people) only in Manual mode
 * - Emergency freeze toggle shows "use for scheduling" sub-option when active
 * - Save button calls updateHomeLeaveConfig with correct payload
 * - Returns null when isOwner is false
 *
 * Validates: Requirements 6.1, 6.2
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import HomeLeaveConfigCard from "../../../components/spaces/HomeLeaveConfigCard";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockGetHomeLeaveConfig = vi.hoisted(() => vi.fn());
const mockUpdateHomeLeaveConfig = vi.hoisted(() => vi.fn());

vi.mock("@/lib/api/spaces", () => ({
  getHomeLeaveConfig: mockGetHomeLeaveConfig,
  updateHomeLeaveConfig: mockUpdateHomeLeaveConfig,
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      title: "Home Leave Configuration",
      description: "Configure home-leave settings for this space.",
      loading: "Loading...",
      loadError: "Failed to load configuration.",
      retry: "Retry",
      modeLabel: "Mode",
      modeAutomatic: "Automatic",
      modeManual: "Manual",
      modeDisabled: "Disabled",
      balanceLabel: "Balance Ratio",
      moreBase: "More Base",
      moreHome: "More Home",
      baseDaysLabel: "Base Days",
      homeDaysLabel: "Home Days",
      minPeopleLabel: "Min People at Base",
      emergencyFreezeLabel: "Emergency Freeze",
      emergencyFreezeHint: "Freeze all home leave immediately.",
      useForSchedulingLabel: "Use for scheduling",
      save: "Save",
      saving: "Saving...",
      saved: "Saved",
      saveError: "Failed to save configuration.",
    };
    return translations[key] ?? key;
  },
  useLocale: () => "en",
}));

// ── Test Data ─────────────────────────────────────────────────────────────────

const defaultConfig = {
  mode: "automatic" as const,
  balanceValue: 50,
  baseDays: 7,
  homeDays: 2,
  minPeopleAtBase: 8,
  minRestHours: 0,
  eligibilityThresholdHours: 168,
  leaveCapacity: 1,
  leaveDurationHours: 48,
  emergencyFreezeActive: false,
  emergencyUseForScheduling: false,
  freezeStartedAt: null,
  preFreezeMode: "automatic" as const,
};

const defaultProps = {
  spaceId: "space-123",
  isOwner: true,
};

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("HomeLeaveConfigCard (Task 15.2)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetHomeLeaveConfig.mockResolvedValue(defaultConfig);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 6.1: Renders all three mode options", () => {
    it("displays Automatic, Manual, and Disabled mode buttons", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("radio", { name: "Automatic" })).toBeInTheDocument();
      });

      expect(screen.getByRole("radio", { name: "Manual" })).toBeInTheDocument();
      expect(screen.getByRole("radio", { name: "Disabled" })).toBeInTheDocument();
    });

    it("marks the current mode as active (aria-checked)", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("radio", { name: "Automatic" })).toHaveAttribute("aria-checked", "true");
      });

      expect(screen.getByRole("radio", { name: "Manual" })).toHaveAttribute("aria-checked", "false");
      expect(screen.getByRole("radio", { name: "Disabled" })).toHaveAttribute("aria-checked", "false");
    });

    it("renders the mode selector within a radiogroup", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("radiogroup", { name: "Mode" })).toBeInTheDocument();
      });
    });
  });

  describe("Req 6.1: Conditional fields based on mode — Automatic", () => {
    it("shows ratio slider in Automatic mode", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("slider", { name: "Balance Ratio" })).toBeInTheDocument();
      });
    });

    it("does not show manual fields in Automatic mode", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("radio", { name: "Automatic" })).toBeInTheDocument();
      });

      expect(screen.queryByLabelText("Base Days")).not.toBeInTheDocument();
      expect(screen.queryByLabelText("Home Days")).not.toBeInTheDocument();
      expect(screen.queryByLabelText("Min People at Base")).not.toBeInTheDocument();
    });
  });

  describe("Req 6.1: Conditional fields based on mode — Manual", () => {
    it("shows manual fields (base days, home days, min people) in Manual mode", async () => {
      mockGetHomeLeaveConfig.mockResolvedValue({ ...defaultConfig, mode: "manual" });
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Base Days")).toBeInTheDocument();
      });

      expect(screen.getByLabelText("Home Days")).toBeInTheDocument();
      expect(screen.getByLabelText("Min People at Base")).toBeInTheDocument();
    });

    it("does not show ratio slider in Manual mode", async () => {
      mockGetHomeLeaveConfig.mockResolvedValue({ ...defaultConfig, mode: "manual" });
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Base Days")).toBeInTheDocument();
      });

      expect(screen.queryByRole("slider", { name: "Balance Ratio" })).not.toBeInTheDocument();
    });

    it("switches to manual fields when Manual mode button is clicked", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("radio", { name: "Automatic" })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole("radio", { name: "Manual" }));

      expect(screen.getByLabelText("Base Days")).toBeInTheDocument();
      expect(screen.getByLabelText("Home Days")).toBeInTheDocument();
      expect(screen.getByLabelText("Min People at Base")).toBeInTheDocument();
      expect(screen.queryByRole("slider", { name: "Balance Ratio" })).not.toBeInTheDocument();
    });
  });

  describe("Req 6.1: Conditional fields based on mode — Disabled", () => {
    it("shows neither slider nor manual fields in Disabled mode", async () => {
      mockGetHomeLeaveConfig.mockResolvedValue({ ...defaultConfig, mode: "disabled" });
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("radio", { name: "Disabled" })).toHaveAttribute("aria-checked", "true");
      });

      expect(screen.queryByRole("slider", { name: "Balance Ratio" })).not.toBeInTheDocument();
      expect(screen.queryByLabelText("Base Days")).not.toBeInTheDocument();
      expect(screen.queryByLabelText("Home Days")).not.toBeInTheDocument();
      expect(screen.queryByLabelText("Min People at Base")).not.toBeInTheDocument();
    });
  });

  describe("Req 6.1: Emergency freeze toggle behavior", () => {
    it("renders the emergency freeze toggle", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("switch", { name: "Emergency Freeze" })).toBeInTheDocument();
      });
    });

    it("does not show use-for-scheduling checkbox when freeze is inactive", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("switch", { name: "Emergency Freeze" })).toBeInTheDocument();
      });

      expect(screen.queryByLabelText("Use for scheduling")).not.toBeInTheDocument();
    });

    it("shows use-for-scheduling checkbox when freeze is active", async () => {
      mockGetHomeLeaveConfig.mockResolvedValue({ ...defaultConfig, emergencyFreezeActive: true });
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText("Use for scheduling")).toBeInTheDocument();
      });
    });

    it("shows use-for-scheduling checkbox after toggling freeze on", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("switch", { name: "Emergency Freeze" })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole("switch", { name: "Emergency Freeze" }));

      expect(screen.getByLabelText("Use for scheduling")).toBeInTheDocument();
    });

    it("hides use-for-scheduling checkbox after toggling freeze off", async () => {
      mockGetHomeLeaveConfig.mockResolvedValue({ ...defaultConfig, emergencyFreezeActive: true });
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("switch", { name: "Emergency Freeze" })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole("switch", { name: "Emergency Freeze" }));

      expect(screen.queryByLabelText("Use for scheduling")).not.toBeInTheDocument();
    });
  });

  describe("Req 6.2: Save button calls updateHomeLeaveConfig with correct payload", () => {
    it("calls updateHomeLeaveConfig with default automatic mode payload", async () => {
      mockUpdateHomeLeaveConfig.mockResolvedValue(undefined);
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
      });

      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: "Save" }));
      });

      await waitFor(() => {
        expect(mockUpdateHomeLeaveConfig).toHaveBeenCalledWith("space-123", {
          mode: "automatic",
          balanceValue: 50,
          baseDays: 7,
          homeDays: 2,
          minPeopleAtBase: 8,
          minRestHours: 0,
          eligibilityThresholdHours: 168,
          leaveCapacity: 1,
          leaveDurationHours: 48,
          emergencyFreezeActive: false,
          emergencyUseForScheduling: false,
        });
      });
    });

    it("calls updateHomeLeaveConfig with updated manual mode payload", async () => {
      mockUpdateHomeLeaveConfig.mockResolvedValue(undefined);
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("radio", { name: "Manual" })).toBeInTheDocument();
      });

      // Switch to manual mode
      fireEvent.click(screen.getByRole("radio", { name: "Manual" }));

      // Update manual fields
      fireEvent.change(screen.getByLabelText("Base Days"), { target: { value: "10" } });
      fireEvent.change(screen.getByLabelText("Home Days"), { target: { value: "3" } });
      fireEvent.change(screen.getByLabelText("Min People at Base"), { target: { value: "5" } });

      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: "Save" }));
      });

      await waitFor(() => {
        expect(mockUpdateHomeLeaveConfig).toHaveBeenCalledWith("space-123", {
          mode: "manual",
          balanceValue: 50,
          baseDays: 10,
          homeDays: 3,
          minPeopleAtBase: 5,
          minRestHours: 0,
          eligibilityThresholdHours: 168,
          leaveCapacity: 1,
          leaveDurationHours: 48,
          emergencyFreezeActive: false,
          emergencyUseForScheduling: false,
        });
      });
    });

    it("shows saved message after successful save", async () => {
      mockUpdateHomeLeaveConfig.mockResolvedValue(undefined);
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
      });

      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: "Save" }));
      });

      await waitFor(() => {
        expect(screen.getByText("Saved")).toBeInTheDocument();
      });
    });

    it("shows error message when save fails", async () => {
      mockUpdateHomeLeaveConfig.mockRejectedValue(new Error("Network error"));
      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
      });

      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: "Save" }));
      });

      await waitFor(() => {
        expect(screen.getByText("Failed to save configuration.")).toBeInTheDocument();
      });
    });

    it("disables save button while saving", async () => {
      let resolvePromise: () => void;
      mockUpdateHomeLeaveConfig.mockImplementation(
        () => new Promise<void>((resolve) => { resolvePromise = resolve; })
      );

      render(<HomeLeaveConfigCard {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
      });

      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: "Save" }));
      });

      expect(screen.getByRole("button", { name: "Saving..." })).toBeDisabled();

      await act(async () => {
        resolvePromise!();
      });

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Save" })).not.toBeDisabled();
      });
    });
  });

  describe("Req 6.1: Returns null when isOwner is false", () => {
    it("renders nothing when isOwner is false", () => {
      const { container } = render(
        <HomeLeaveConfigCard {...defaultProps} isOwner={false} />
      );

      expect(container.innerHTML).toBe("");
    });

    it("renders content when isOwner is true", async () => {
      render(<HomeLeaveConfigCard {...defaultProps} isOwner={true} />);

      await waitFor(() => {
        expect(screen.getByText("Home Leave Configuration")).toBeInTheDocument();
      });
    });
  });
});
