/**
 * Unit tests for ManagementTimeoutCard component.
 *
 * Verifies:
 * - Renders the current timeout value in the input
 * - Shows validation error for values outside [5, 120]
 * - Calls updateManagementTimeout with correct spaceId and minutes on save
 * - Returns null (doesn't render) when isOwner is false
 *
 * Validates: Requirements 5.1, 5.2, 5.3
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import ManagementTimeoutCard from "../../components/spaces/ManagementTimeoutCard";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockUpdateManagementTimeout = vi.fn();

vi.mock("@/lib/api/spaces", () => ({
  updateManagementTimeout: (...args: any[]) => mockUpdateManagementTimeout(...args),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      "managementTimeout.title": "Management Timeout",
      "managementTimeout.description": "Set the management session timeout for this space.",
      "managementTimeout.inputLabel": "Timeout in minutes",
      "managementTimeout.minutes": "minutes",
      "managementTimeout.validationError": "Value must be between 5 and 120 minutes.",
      "managementTimeout.saveError": "Failed to save timeout.",
      "managementTimeout.saved": "Timeout saved successfully.",
      save: "Save",
      saving: "Saving...",
    };
    return translations[key] ?? key;
  },
}));

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("ManagementTimeoutCard (Task 14.2)", () => {
  const defaultProps = {
    spaceId: "space-123",
    currentTimeout: 15,
    isOwner: true,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Renders current timeout value (Req 5.1)", () => {
    it("displays the current timeout value in the input field", () => {
      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes") as HTMLInputElement;
      expect(input.value).toBe("15");
    });

    it("updates displayed value when currentTimeout prop changes", () => {
      const { rerender } = render(<ManagementTimeoutCard {...defaultProps} currentTimeout={30} />);

      const input = screen.getByLabelText("Timeout in minutes") as HTMLInputElement;
      expect(input.value).toBe("30");

      rerender(<ManagementTimeoutCard {...defaultProps} currentTimeout={60} />);
      expect(input.value).toBe("60");
    });

    it("renders the title and description", () => {
      render(<ManagementTimeoutCard {...defaultProps} />);

      expect(screen.getByText("Management Timeout")).toBeInTheDocument();
      expect(screen.getByText("Set the management session timeout for this space.")).toBeInTheDocument();
    });
  });

  describe("Validation rejects out-of-range values (Req 5.2, 5.3)", () => {
    it("shows validation error when value is below 5", async () => {
      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "3" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
      });

      expect(screen.getByRole("alert")).toHaveTextContent("Value must be between 5 and 120 minutes.");
      expect(mockUpdateManagementTimeout).not.toHaveBeenCalled();
    });

    it("shows validation error when value is above 120", async () => {
      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "150" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
      });

      expect(screen.getByRole("alert")).toHaveTextContent("Value must be between 5 and 120 minutes.");
      expect(mockUpdateManagementTimeout).not.toHaveBeenCalled();
    });

    it("shows validation error for non-integer values", async () => {
      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "10.5" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
      });

      expect(screen.getByRole("alert")).toHaveTextContent("Value must be between 5 and 120 minutes.");
      expect(mockUpdateManagementTimeout).not.toHaveBeenCalled();
    });

    it("clears validation error when input changes", async () => {
      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "3" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
      });

      expect(screen.getByRole("alert")).toBeInTheDocument();

      // Changing input should clear the error
      fireEvent.change(input, { target: { value: "10" } });

      expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });
  });

  describe("API call dispatched with correct payload (Req 5.1, 5.2)", () => {
    it("calls updateManagementTimeout with correct spaceId and minutes on save", async () => {
      mockUpdateManagementTimeout.mockResolvedValue(undefined);

      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "30" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
      });

      await waitFor(() => {
        expect(mockUpdateManagementTimeout).toHaveBeenCalledWith("space-123", 30);
      });
    });

    it("shows success message after successful save", async () => {
      mockUpdateManagementTimeout.mockResolvedValue(undefined);

      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "60" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
      });

      await waitFor(() => {
        expect(screen.getByRole("status")).toHaveTextContent("Timeout saved successfully.");
      });
    });

    it("shows error message when API call fails", async () => {
      mockUpdateManagementTimeout.mockRejectedValue(new Error("Network error"));

      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "30" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
      });

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent("Failed to save timeout.");
      });
    });

    it("disables save button while saving", async () => {
      let resolvePromise: () => void;
      mockUpdateManagementTimeout.mockImplementation(
        () => new Promise<void>((resolve) => { resolvePromise = resolve; })
      );

      render(<ManagementTimeoutCard {...defaultProps} />);

      const input = screen.getByLabelText("Timeout in minutes");
      fireEvent.change(input, { target: { value: "30" } });

      const saveButton = screen.getByRole("button", { name: "Save" });
      await act(async () => {
        fireEvent.click(saveButton);
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

  describe("Hidden for non-owners (Req 5.1)", () => {
    it("returns null when isOwner is false", () => {
      const { container } = render(
        <ManagementTimeoutCard {...defaultProps} isOwner={false} />
      );

      expect(container.innerHTML).toBe("");
    });

    it("renders content when isOwner is true", () => {
      render(<ManagementTimeoutCard {...defaultProps} isOwner={true} />);

      expect(screen.getByText("Management Timeout")).toBeInTheDocument();
      expect(screen.getByLabelText("Timeout in minutes")).toBeInTheDocument();
    });
  });
});
