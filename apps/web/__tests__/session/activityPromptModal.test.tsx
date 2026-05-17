/**
 * Unit tests for ActivityPromptModal component.
 *
 * Tests: countdown display, auto-exit on zero, focus management,
 * keyboard navigation, and button interactions.
 *
 * Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, act, fireEvent } from "@testing-library/react";
import ActivityPromptModal from "../../components/admin/ActivityPromptModal";

// Mock next-intl
vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      title: "Are you still active?",
      description: "Your session will expire due to inactivity. Choose to continue or exit.",
      yes: "Yes",
      no: "No",
    };
    return translations[key] ?? key;
  },
}));

describe("ActivityPromptModal", () => {
  let onYes: ReturnType<typeof vi.fn>;
  let onNo: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.useFakeTimers();
    onYes = vi.fn();
    onNo = vi.fn();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders nothing when open is false", () => {
    const { container } = render(
      <ActivityPromptModal open={false} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );
    expect(container.innerHTML).toBe("");
  });

  it("renders the modal when open is true", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    expect(screen.getByText("Are you still active?")).toBeInTheDocument();
    expect(screen.getByText("Yes")).toBeInTheDocument();
    expect(screen.getByText("No")).toBeInTheDocument();
  });

  it("displays the countdown timer starting at the given seconds", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    expect(screen.getByText("1:00")).toBeInTheDocument();
  });

  it("decrements the countdown every second", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(screen.getByText("0:59")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(screen.getByText("0:58")).toBeInTheDocument();
  });

  it("calls onNo when countdown reaches zero", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={3} onYes={onYes} onNo={onNo} />
    );

    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(onNo).toHaveBeenCalledTimes(1);
  });

  it("calls onYes when Yes button is clicked", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    fireEvent.click(screen.getByText("Yes"));
    expect(onYes).toHaveBeenCalledTimes(1);
  });

  it("calls onNo when No button is clicked", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    fireEvent.click(screen.getByText("No"));
    expect(onNo).toHaveBeenCalledTimes(1);
  });

  it("gives initial focus to the Yes button", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    act(() => {
      vi.advanceTimersByTime(100);
    });

    expect(document.activeElement).toBe(screen.getByText("Yes"));
  });

  it("has role=alertdialog and aria-modal=true for accessibility", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    const dialog = screen.getByRole("alertdialog");
    expect(dialog).toHaveAttribute("aria-modal", "true");
  });

  it("traps focus within the modal (Tab from last to first)", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    const dialog = screen.getByRole("alertdialog");
    const noButton = screen.getByText("No");

    // Focus the No button (last focusable)
    noButton.focus();

    // Press Tab — should wrap to Yes button
    fireEvent.keyDown(dialog, { key: "Tab", shiftKey: false });

    expect(document.activeElement).toBe(screen.getByText("Yes"));
  });

  it("traps focus within the modal (Shift+Tab from first to last)", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    const dialog = screen.getByRole("alertdialog");
    const yesButton = screen.getByText("Yes");

    // Focus the Yes button (first focusable)
    yesButton.focus();

    // Press Shift+Tab — should wrap to No button
    fireEvent.keyDown(dialog, { key: "Tab", shiftKey: true });

    expect(document.activeElement).toBe(screen.getByText("No"));
  });

  it("prevents background interaction with overlay", () => {
    render(
      <ActivityPromptModal open={true} countdownSeconds={60} onYes={onYes} onNo={onNo} />
    );

    // The overlay has role="presentation" and covers the entire viewport
    const overlay = screen.getByRole("presentation");
    expect(overlay).toHaveStyle({ position: "fixed", inset: "0" });
  });
});
