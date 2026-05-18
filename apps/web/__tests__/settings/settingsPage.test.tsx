/**
 * Unit tests for Settings page components.
 *
 * Tests: Country dropdown rendering and search, State dropdown conditional display,
 * country change clears state, resolved timezone display, and section presence
 * on Settings page vs Profile page.
 *
 * Requirements: 6.5, 7.1, 7.2, 7.3, 7.4
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

// ─── Mocks ───────────────────────────────────────────────────────────────────

// Mock next-intl — useTranslations returns a function that returns the key itself
vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
}));

// Mock authStore
const mockSetTimezone = vi.fn();
const mockSetTimeFormat = vi.fn();
vi.mock("@/lib/store/authStore", () => ({
  useAuthStore: Object.assign(
    (selector?: (state: any) => any) => {
      const state = {
        timezoneId: "Asia/Jerusalem",
        timezoneOffsetMinutes: 120,
        preferredLocale: "en",
        timeFormat: "24h",
        setTimeFormat: mockSetTimeFormat,
        setTimezone: mockSetTimezone,
      };
      return selector ? selector(state) : state;
    },
    {
      getState: () => ({
        timezoneId: "Asia/Jerusalem",
        timezoneOffsetMinutes: 120,
        preferredLocale: "en",
        timeFormat: "24h",
        setTimeFormat: mockSetTimeFormat,
        setTimezone: mockSetTimezone,
      }),
    }
  ),
}));

// Mock spaceStore
vi.mock("@/lib/store/spaceStore", () => ({
  useSpaceStore: (selector?: (state: any) => any) => {
    const state = { currentSpaceId: "space-1" };
    return selector ? selector(state) : state;
  },
}));

// Mock AppShell to just render children
vi.mock("@/components/shell/AppShell", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div data-testid="app-shell">{children}</div>,
}));

// Mock NotificationPreferences
vi.mock("@/components/NotificationPreferences", () => ({
  default: () => <div data-testid="notification-preferences">NotificationPreferences</div>,
}));

// Mock PushNotificationSettings
vi.mock("@/components/PushNotificationSettings", () => ({
  default: ({ spaceId }: { spaceId: string }) => (
    <div data-testid="push-notification-settings">PushNotificationSettings ({spaceId})</div>
  ),
}));

// Mock the API call
vi.mock("@/lib/api/userSettings", () => ({
  updateUserLocation: vi.fn().mockResolvedValue({
    ianaTimezoneId: "America/New_York",
    offsetMinutes: -300,
  }),
}));

import SettingsPage from "../../app/settings/page";

// ─── Tests ───────────────────────────────────────────────────────────────────

describe("Settings Page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("Settings sections presence (Req 6.5)", () => {
    it("renders the Location section with country dropdown", () => {
      render(<SettingsPage />);
      // The country dropdown button shows the placeholder key
      expect(screen.getByText("countryPlaceholder")).toBeInTheDocument();
    });

    it("renders the Time Format section with 24h and AM/PM buttons", () => {
      render(<SettingsPage />);
      expect(screen.getByText("24h")).toBeInTheDocument();
      expect(screen.getByText("AM/PM")).toBeInTheDocument();
    });

    it("renders the Notification Preferences section", () => {
      render(<SettingsPage />);
      expect(screen.getByTestId("notification-preferences")).toBeInTheDocument();
    });

    it("renders the Push Notification Settings section", () => {
      render(<SettingsPage />);
      expect(screen.getByTestId("push-notification-settings")).toBeInTheDocument();
    });
  });

  describe("Country dropdown (Req 7.1)", () => {
    it("renders a country dropdown button with placeholder text", () => {
      render(<SettingsPage />);
      const countryButton = screen.getByText("countryPlaceholder");
      expect(countryButton).toBeInTheDocument();
      expect(countryButton.tagName).toBe("BUTTON");
    });

    it("opens a searchable input when clicked", () => {
      render(<SettingsPage />);
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);

      // After clicking, a combobox input should appear
      const input = screen.getByRole("combobox");
      expect(input).toBeInTheDocument();
    });

    it("filters country options when typing in search", () => {
      render(<SettingsPage />);
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);

      const input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "Israel" } });

      // Should show Israel in the listbox
      const listbox = screen.getByRole("listbox");
      expect(listbox).toBeInTheDocument();

      const options = screen.getAllByRole("option");
      // Only Israel should match
      expect(options.length).toBe(1);
      expect(options[0]).toHaveTextContent("Israel");
    });

    it("shows all countries when search is empty", () => {
      render(<SettingsPage />);
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);

      const options = screen.getAllByRole("option");
      // Should have many countries (the full list)
      expect(options.length).toBeGreaterThan(50);
    });

    it("selects a country when an option is clicked", () => {
      render(<SettingsPage />);
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);

      // Search for Israel and select it
      const input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "Israel" } });

      const option = screen.getByRole("option", { name: "Israel" });
      fireEvent.click(option);

      // After selection, the button should show "Israel"
      expect(screen.getByText("Israel")).toBeInTheDocument();
    });
  });

  describe("State dropdown conditional display (Req 7.2, 7.3)", () => {
    it("does NOT show state dropdown for single-timezone country (Israel)", () => {
      render(<SettingsPage />);
      // Select Israel (single timezone)
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);

      const input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "Israel" } });
      fireEvent.click(screen.getByRole("option", { name: "Israel" }));

      // State dropdown should NOT be present
      expect(screen.queryByText("statePlaceholder")).not.toBeInTheDocument();
    });

    it("shows state dropdown for multi-timezone country (United States)", () => {
      render(<SettingsPage />);
      // Select United States (multi-timezone)
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);

      const input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "United States" } });
      fireEvent.click(screen.getByRole("option", { name: "United States" }));

      // State dropdown should appear
      expect(screen.getByText("statePlaceholder")).toBeInTheDocument();
    });

    it("shows state dropdown for Australia (multi-timezone)", () => {
      render(<SettingsPage />);
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);

      const input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "Australia" } });
      fireEvent.click(screen.getByRole("option", { name: "Australia" }));

      expect(screen.getByText("statePlaceholder")).toBeInTheDocument();
    });
  });

  describe("Country change clears state (Req 7.4)", () => {
    it("clears state selection when country changes to another multi-tz country", () => {
      render(<SettingsPage />);

      // Select US
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);
      let input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "United States" } });
      fireEvent.click(screen.getByRole("option", { name: "United States" }));

      // Select a state (New York)
      const stateButton = screen.getByText("statePlaceholder");
      fireEvent.click(stateButton);
      const stateInput = screen.getByRole("combobox");
      fireEvent.change(stateInput, { target: { value: "New York" } });
      fireEvent.click(screen.getByRole("option", { name: "New York" }));

      // Verify state is selected
      expect(screen.getByText("New York")).toBeInTheDocument();

      // Now change country to Canada
      const usButton = screen.getByText("United States");
      fireEvent.click(usButton);
      input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "Canada" } });
      fireEvent.click(screen.getByRole("option", { name: "Canada" }));

      // State should be cleared — the state dropdown should show placeholder again
      expect(screen.queryByText("New York")).not.toBeInTheDocument();
      expect(screen.getByText("statePlaceholder")).toBeInTheDocument();
    });

    it("clears state when switching from multi-tz to single-tz country", () => {
      render(<SettingsPage />);

      // Select US (multi-tz)
      const countryButton = screen.getByText("countryPlaceholder");
      fireEvent.click(countryButton);
      let input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "United States" } });
      fireEvent.click(screen.getByRole("option", { name: "United States" }));

      // Select a state
      const stateButton = screen.getByText("statePlaceholder");
      fireEvent.click(stateButton);
      const stateInput = screen.getByRole("combobox");
      fireEvent.change(stateInput, { target: { value: "California" } });
      fireEvent.click(screen.getByRole("option", { name: "California" }));

      // Switch to Israel (single-tz)
      const usButton = screen.getByText("United States");
      fireEvent.click(usButton);
      input = screen.getByRole("combobox");
      fireEvent.change(input, { target: { value: "Israel" } });
      fireEvent.click(screen.getByRole("option", { name: "Israel" }));

      // State dropdown should be gone entirely
      expect(screen.queryByText("statePlaceholder")).not.toBeInTheDocument();
      expect(screen.queryByText("California")).not.toBeInTheDocument();
    });
  });

  describe("Resolved timezone display (Req 7.5)", () => {
    it("displays the default timezone (Asia/Jerusalem) initially", () => {
      render(<SettingsPage />);
      expect(screen.getByText("Asia/Jerusalem")).toBeInTheDocument();
    });

    it("displays timezone label", () => {
      render(<SettingsPage />);
      expect(screen.getByText("timezone:")).toBeInTheDocument();
    });
  });
});
