/**
 * Unit tests for PickerTabs component.
 *
 * Verifies:
 * - Renders two tab buttons with correct i18n labels (Req 5.1)
 * - Highlights active tab with visual indicator (Req 6.1)
 * - Calls onTabChange with correct tab ID on click (Req 5.1)
 * - Tab buttons meet 44x44px minimum tap target (Req 6.1)
 *
 * Requirements: 5.1, 6.1
 */

import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import PickerTabs from "../../components/pick/PickerTabs";

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      "tabs.slots": "משמרות פנויות",
      "tabs.myShifts": "המשמרות שלי",
      title: "בחירת משמרות",
    };
    return translations[key] ?? key;
  },
}));

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("PickerTabs (Task 7.1)", () => {
  const defaultProps = {
    activeTab: "slots" as const,
    onTabChange: vi.fn(),
  };

  it("renders two tab buttons with correct Hebrew labels", () => {
    render(<PickerTabs {...defaultProps} />);

    expect(screen.getByText("משמרות פנויות")).toBeInTheDocument();
    expect(screen.getByText("המשמרות שלי")).toBeInTheDocument();
  });

  it("renders tabs with tablist role for accessibility", () => {
    render(<PickerTabs {...defaultProps} />);

    expect(screen.getByRole("tablist")).toBeInTheDocument();
    const tabs = screen.getAllByRole("tab");
    expect(tabs).toHaveLength(2);
  });

  it("marks the active tab with aria-selected=true", () => {
    render(<PickerTabs {...defaultProps} activeTab="slots" />);

    const tabs = screen.getAllByRole("tab");
    expect(tabs[0]).toHaveAttribute("aria-selected", "true");
    expect(tabs[1]).toHaveAttribute("aria-selected", "false");
  });

  it("marks my-shifts tab as active when activeTab is my-shifts", () => {
    render(<PickerTabs {...defaultProps} activeTab="my-shifts" />);

    const tabs = screen.getAllByRole("tab");
    expect(tabs[0]).toHaveAttribute("aria-selected", "false");
    expect(tabs[1]).toHaveAttribute("aria-selected", "true");
  });

  it("highlights active tab with distinct styling", () => {
    render(<PickerTabs {...defaultProps} activeTab="slots" />);

    const tabs = screen.getAllByRole("tab");
    // Active tab has white background and dark text
    expect(tabs[0].className).toContain("bg-white");
    expect(tabs[0].className).toContain("text-slate-900");
    // Inactive tab has muted text
    expect(tabs[1].className).toContain("text-slate-500");
    expect(tabs[1].className).not.toContain("bg-white");
  });

  it("calls onTabChange with 'slots' when slots tab is clicked", () => {
    const onTabChange = vi.fn();
    render(<PickerTabs activeTab="my-shifts" onTabChange={onTabChange} />);

    fireEvent.click(screen.getByText("משמרות פנויות"));

    expect(onTabChange).toHaveBeenCalledTimes(1);
    expect(onTabChange).toHaveBeenCalledWith("slots");
  });

  it("calls onTabChange with 'my-shifts' when my-shifts tab is clicked", () => {
    const onTabChange = vi.fn();
    render(<PickerTabs activeTab="slots" onTabChange={onTabChange} />);

    fireEvent.click(screen.getByText("המשמרות שלי"));

    expect(onTabChange).toHaveBeenCalledTimes(1);
    expect(onTabChange).toHaveBeenCalledWith("my-shifts");
  });

  it("tab buttons have minimum 44px height for touch targets", () => {
    render(<PickerTabs {...defaultProps} />);

    const tabs = screen.getAllByRole("tab");
    tabs.forEach((tab) => {
      expect(tab.className).toContain("min-h-[44px]");
      expect(tab.className).toContain("min-w-[44px]");
    });
  });
});
