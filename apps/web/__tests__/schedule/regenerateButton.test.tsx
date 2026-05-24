/**
 * Unit tests for RegenerateButton component.
 *
 * Verifies:
 * - Button renders only when user has ScheduleRecalculate permission (Req 7.3)
 * - Button hidden when no published version exists (Req 1.4)
 * - Button disabled with status indicator when regeneration in progress (Req 1.5)
 * - Button click triggers onRegenerate callback (Req 1.1)
 *
 * Requirements: 1.1, 1.4, 1.5, 7.3
 */

import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      button: "Regenerate Schedule",
      inProgress: "Regenerating...",
    };
    return translations[key] ?? key;
  },
}));

import RegenerateButton, {
  type RegenerateButtonProps,
} from "../../components/schedule/RegenerateButton";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function defaultProps(overrides?: Partial<RegenerateButtonProps>): RegenerateButtonProps {
  return {
    spaceId: "space-1",
    groupId: "group-1",
    hasPublishedVersion: true,
    hasPermission: true,
    isRegenerationInProgress: false,
    onRegenerate: vi.fn(),
    ...overrides,
  };
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe("RegenerateButton", () => {
  describe("Req 7.3: Permission gating", () => {
    it("renders nothing when hasPermission is false", () => {
      const { container } = render(
        <RegenerateButton {...defaultProps({ hasPermission: false })} />
      );
      expect(container.innerHTML).toBe("");
    });

    it("renders the button when hasPermission is true", () => {
      render(<RegenerateButton {...defaultProps({ hasPermission: true })} />);
      expect(screen.getByRole("button")).toBeInTheDocument();
    });
  });

  describe("Req 1.4: Hidden when no published version exists", () => {
    it("renders nothing when hasPublishedVersion is false", () => {
      const { container } = render(
        <RegenerateButton {...defaultProps({ hasPublishedVersion: false })} />
      );
      expect(container.innerHTML).toBe("");
    });

    it("renders the button when hasPublishedVersion is true", () => {
      render(<RegenerateButton {...defaultProps({ hasPublishedVersion: true })} />);
      expect(screen.getByRole("button")).toBeInTheDocument();
    });
  });

  describe("Req 1.5: Disabled with status indicator when regeneration in progress", () => {
    it("disables the button when isRegenerationInProgress is true", () => {
      render(
        <RegenerateButton {...defaultProps({ isRegenerationInProgress: true })} />
      );
      const button = screen.getByRole("button");
      expect(button).toBeDisabled();
    });

    it("shows in-progress text when regeneration is running", () => {
      render(
        <RegenerateButton {...defaultProps({ isRegenerationInProgress: true })} />
      );
      expect(screen.getByText("Regenerating...")).toBeInTheDocument();
    });

    it("sets aria-busy when regeneration is in progress", () => {
      render(
        <RegenerateButton {...defaultProps({ isRegenerationInProgress: true })} />
      );
      const button = screen.getByRole("button");
      expect(button).toHaveAttribute("aria-busy", "true");
    });

    it("button is enabled when no regeneration is in progress", () => {
      render(
        <RegenerateButton {...defaultProps({ isRegenerationInProgress: false })} />
      );
      const button = screen.getByRole("button");
      expect(button).not.toBeDisabled();
    });
  });

  describe("Req 1.1: Click triggers onRegenerate", () => {
    it("calls onRegenerate when button is clicked", () => {
      const onRegenerate = vi.fn();
      render(<RegenerateButton {...defaultProps({ onRegenerate })} />);
      fireEvent.click(screen.getByRole("button"));
      expect(onRegenerate).toHaveBeenCalledTimes(1);
    });

    it("does not call onRegenerate when button is disabled", () => {
      const onRegenerate = vi.fn();
      render(
        <RegenerateButton
          {...defaultProps({ onRegenerate, isRegenerationInProgress: true })}
        />
      );
      fireEvent.click(screen.getByRole("button"));
      expect(onRegenerate).not.toHaveBeenCalled();
    });
  });

  describe("Button text", () => {
    it("shows 'Regenerate Schedule' text when idle", () => {
      render(<RegenerateButton {...defaultProps()} />);
      expect(screen.getByText("Regenerate Schedule")).toBeInTheDocument();
    });
  });
});
