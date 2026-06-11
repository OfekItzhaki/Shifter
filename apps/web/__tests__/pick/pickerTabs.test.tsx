/**
 * Unit tests for PickerTabs component.
 *
 * Verifies:
 * - Renders picker tab buttons with correct i18n labels (Req 5.1)
 * - Highlights active tab with visual indicator (Req 6.1)
 * - Calls onTabChange with correct tab ID on click (Req 5.1)
 * - Tab buttons meet 44x44px minimum tap target (Req 6.1)
 *
 * Requirements: 5.1, 6.1
 */

import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import PickerTabs from "../../components/pick/PickerTabs";

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      "tabs.status": "Status",
      "tabs.slots": "Available",
      "tabs.myShifts": "My Shifts",
      "tabs.waitlist": "Waitlist",
      "tabs.swaps": "Swaps",
      title: "Shift Picker",
    };
    return translations[key] ?? key;
  },
}));

describe("PickerTabs (Task 7.1)", () => {
  const defaultProps = {
    activeTab: "slots" as const,
    onTabChange: vi.fn(),
  };

  it("renders tab buttons with correct labels", () => {
    render(<PickerTabs {...defaultProps} />);

    expect(screen.getByText("Status")).toBeInTheDocument();
    expect(screen.getByText("Available")).toBeInTheDocument();
    expect(screen.getByText("My Shifts")).toBeInTheDocument();
    expect(screen.getByText("Waitlist")).toBeInTheDocument();
    expect(screen.getByText("Swaps")).toBeInTheDocument();
  });

  it("renders tabs with tablist role for accessibility", () => {
    render(<PickerTabs {...defaultProps} />);

    expect(screen.getByRole("tablist")).toBeInTheDocument();
    const tabs = screen.getAllByRole("tab");
    expect(tabs).toHaveLength(5);
  });

  it("marks the active tab with aria-selected=true", () => {
    render(<PickerTabs {...defaultProps} activeTab="slots" />);

    expect(screen.getByRole("tab", { name: "Status" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "Available" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: "My Shifts" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "Waitlist" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "Swaps" })).toHaveAttribute("aria-selected", "false");
  });

  it("marks status tab as active when activeTab is status", () => {
    render(<PickerTabs {...defaultProps} activeTab="status" />);

    expect(screen.getByRole("tab", { name: "Status" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: "Available" })).toHaveAttribute("aria-selected", "false");
  });

  it("marks my-shifts tab as active when activeTab is my-shifts", () => {
    render(<PickerTabs {...defaultProps} activeTab="my-shifts" />);

    expect(screen.getByRole("tab", { name: "Available" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "My Shifts" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: "Waitlist" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "Swaps" })).toHaveAttribute("aria-selected", "false");
  });

  it("marks waitlist tab as active when activeTab is waitlist", () => {
    render(<PickerTabs {...defaultProps} activeTab="waitlist" />);

    expect(screen.getByRole("tab", { name: "Available" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "My Shifts" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "Waitlist" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: "Swaps" })).toHaveAttribute("aria-selected", "false");
  });

  it("marks swaps tab as active when activeTab is swaps", () => {
    render(<PickerTabs {...defaultProps} activeTab="swaps" />);

    expect(screen.getByRole("tab", { name: "Available" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "My Shifts" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "Waitlist" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tab", { name: "Swaps" })).toHaveAttribute("aria-selected", "true");
  });

  it("highlights active tab with distinct styling", () => {
    render(<PickerTabs {...defaultProps} activeTab="slots" />);

    const activeTab = screen.getByRole("tab", { name: "Available" });
    const inactiveTab = screen.getByRole("tab", { name: "My Shifts" });
    expect(activeTab.className).toContain("bg-white");
    expect(activeTab.className).toContain("text-slate-900");
    expect(inactiveTab.className).toContain("text-slate-500");
    expect(inactiveTab.className).not.toContain("bg-white");
  });

  it("calls onTabChange with 'status' when status tab is clicked", () => {
    const onTabChange = vi.fn();
    render(<PickerTabs activeTab="slots" onTabChange={onTabChange} />);

    fireEvent.click(screen.getByText("Status"));

    expect(onTabChange).toHaveBeenCalledTimes(1);
    expect(onTabChange).toHaveBeenCalledWith("status");
  });

  it("calls onTabChange with 'slots' when slots tab is clicked", () => {
    const onTabChange = vi.fn();
    render(<PickerTabs activeTab="my-shifts" onTabChange={onTabChange} />);

    fireEvent.click(screen.getByText("Available"));

    expect(onTabChange).toHaveBeenCalledTimes(1);
    expect(onTabChange).toHaveBeenCalledWith("slots");
  });

  it("calls onTabChange with 'my-shifts' when my-shifts tab is clicked", () => {
    const onTabChange = vi.fn();
    render(<PickerTabs activeTab="slots" onTabChange={onTabChange} />);

    fireEvent.click(screen.getByText("My Shifts"));

    expect(onTabChange).toHaveBeenCalledTimes(1);
    expect(onTabChange).toHaveBeenCalledWith("my-shifts");
  });

  it("calls onTabChange with 'waitlist' when waitlist tab is clicked", () => {
    const onTabChange = vi.fn();
    render(<PickerTabs activeTab="slots" onTabChange={onTabChange} />);

    fireEvent.click(screen.getByText("Waitlist"));

    expect(onTabChange).toHaveBeenCalledTimes(1);
    expect(onTabChange).toHaveBeenCalledWith("waitlist");
  });

  it("calls onTabChange with 'swaps' when swaps tab is clicked", () => {
    const onTabChange = vi.fn();
    render(<PickerTabs activeTab="slots" onTabChange={onTabChange} />);

    fireEvent.click(screen.getByText("Swaps"));

    expect(onTabChange).toHaveBeenCalledTimes(1);
    expect(onTabChange).toHaveBeenCalledWith("swaps");
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
