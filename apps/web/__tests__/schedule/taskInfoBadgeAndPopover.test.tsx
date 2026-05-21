/**
 * Unit tests for TaskInfoBadge and TaskInfoPopover components.
 *
 * Tests: badge hidden when config is null, popover shows "default settings"
 * message for default config, popover closes on click-outside, and localized
 * strings render correctly.
 *
 * Requirements: 5.3, 6.2, 6.3, 6.4, 7.3
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import type { TaskConfigSummaryDto } from "@/lib/api/groups";

// ─── Mocks ───────────────────────────────────────────────────────────────────

// Mock next-intl — useTranslations returns a function that echoes the key
// but provides specific translations for the schedule namespace
vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      "taskInfo.defaultSettings": "Default settings in use",
      "taskInfo.doubleShift": "Double Shift",
      "taskInfo.overlap": "Overlap",
      "taskInfo.timeWindow": "Time Window",
      "taskInfo.allDay": "24/7",
      "taskInfo.burden": "Burden",
      "taskInfo.qualifications": "Qualifications",
      "taskInfo.splitCount": "Split Count",
    };
    return translations[key] ?? key;
  },
}));

import TaskInfoBadge from "../../components/schedule/TaskInfoBadge";
import TaskInfoPopover from "../../components/schedule/TaskInfoPopover";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function makeDefaultConfig(): TaskConfigSummaryDto {
  return {
    taskId: "task-1",
    allowsDoubleShift: false,
    allowsOverlap: false,
    dailyStartTime: null,
    dailyEndTime: null,
    burdenLevel: "Normal",
    requiredQualificationNames: [],
    splitCount: 1,
  };
}

function makeCustomConfig(): TaskConfigSummaryDto {
  return {
    taskId: "task-2",
    allowsDoubleShift: true,
    allowsOverlap: true,
    dailyStartTime: "08:00",
    dailyEndTime: "16:00",
    burdenLevel: "Hard",
    requiredQualificationNames: ["Medic", "Driver"],
    splitCount: 3,
  };
}

// ─── TaskInfoBadge Tests ─────────────────────────────────────────────────────

describe("TaskInfoBadge", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders nothing when config is null (Req 7.3)", () => {
    const { container } = render(<TaskInfoBadge config={null} />);
    expect(container.innerHTML).toBe("");
  });

  it("renders nothing when config is undefined (Req 7.3)", () => {
    const { container } = render(<TaskInfoBadge config={undefined} />);
    expect(container.innerHTML).toBe("");
  });

  it("renders the info button with correct aria-label when config is provided (Req 5.3)", () => {
    render(<TaskInfoBadge config={makeDefaultConfig()} />);
    const button = screen.getByRole("button", { name: "Task configuration info" });
    expect(button).toBeInTheDocument();
  });

  it("opens the popover when the badge is clicked", () => {
    render(<TaskInfoBadge config={makeDefaultConfig()} />);
    const button = screen.getByRole("button", { name: "Task configuration info" });
    fireEvent.click(button);
    // Popover should now be visible (role=tooltip)
    expect(screen.getByRole("tooltip")).toBeInTheDocument();
  });

  it("closes the popover when the badge is clicked again (toggle)", () => {
    render(<TaskInfoBadge config={makeDefaultConfig()} />);
    const button = screen.getByRole("button", { name: "Task configuration info" });
    fireEvent.click(button);
    expect(screen.getByRole("tooltip")).toBeInTheDocument();
    fireEvent.click(button);
    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });
});

// ─── TaskInfoPopover Tests ───────────────────────────────────────────────────

describe("TaskInfoPopover", () => {
  let onClose: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
    onClose = vi.fn();
  });

  it("shows 'default settings' message for default config (Req 6.4)", () => {
    render(<TaskInfoPopover config={makeDefaultConfig()} onClose={onClose} />);
    expect(screen.getByText("Default settings in use")).toBeInTheDocument();
  });

  it("does NOT show 'default settings' message for custom config", () => {
    render(<TaskInfoPopover config={makeCustomConfig()} onClose={onClose} />);
    expect(screen.queryByText("Default settings in use")).not.toBeInTheDocument();
  });

  it("closes on click-outside (Req 6.3)", () => {
    render(
      <div>
        <div data-testid="outside">Outside area</div>
        <TaskInfoPopover config={makeDefaultConfig()} onClose={onClose} />
      </div>
    );
    const outside = screen.getByTestId("outside");
    fireEvent.mouseDown(outside);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("does NOT close when clicking inside the popover", () => {
    render(<TaskInfoPopover config={makeDefaultConfig()} onClose={onClose} />);
    const popover = screen.getByRole("tooltip");
    fireEvent.mouseDown(popover);
    expect(onClose).not.toHaveBeenCalled();
  });

  it("renders localized labels correctly for custom config (Req 6.2)", () => {
    render(<TaskInfoPopover config={makeCustomConfig()} onClose={onClose} />);
    expect(screen.getByText("Double Shift")).toBeInTheDocument();
    expect(screen.getByText("Overlap")).toBeInTheDocument();
    expect(screen.getByText("Time Window")).toBeInTheDocument();
    expect(screen.getByText("Burden")).toBeInTheDocument();
    expect(screen.getByText("Qualifications")).toBeInTheDocument();
    expect(screen.getByText("Split Count")).toBeInTheDocument();
  });

  it("shows '24/7' when no time window is set (Req 6.2)", () => {
    const config: TaskConfigSummaryDto = {
      ...makeDefaultConfig(),
      allowsDoubleShift: true, // make it non-default so we see the full popover
    };
    render(<TaskInfoPopover config={config} onClose={onClose} />);
    expect(screen.getByText("24/7")).toBeInTheDocument();
  });

  it("shows time window when dailyStartTime and dailyEndTime are set", () => {
    render(<TaskInfoPopover config={makeCustomConfig()} onClose={onClose} />);
    expect(screen.getByText("08:00 – 16:00")).toBeInTheDocument();
  });

  it("shows split count only when > 1", () => {
    const configNoSplit: TaskConfigSummaryDto = {
      ...makeDefaultConfig(),
      allowsDoubleShift: true, // non-default to show full popover
      splitCount: 1,
    };
    render(<TaskInfoPopover config={configNoSplit} onClose={onClose} />);
    expect(screen.queryByText("Split Count")).not.toBeInTheDocument();
  });

  it("shows qualifications when present", () => {
    render(<TaskInfoPopover config={makeCustomConfig()} onClose={onClose} />);
    expect(screen.getByText("Medic, Driver")).toBeInTheDocument();
  });

  it("hides qualifications row when none required", () => {
    const config: TaskConfigSummaryDto = {
      ...makeDefaultConfig(),
      allowsDoubleShift: true, // non-default to show full popover
      requiredQualificationNames: [],
    };
    render(<TaskInfoPopover config={config} onClose={onClose} />);
    expect(screen.queryByText("Qualifications")).not.toBeInTheDocument();
  });

  it("shows burden level value", () => {
    render(<TaskInfoPopover config={makeCustomConfig()} onClose={onClose} />);
    expect(screen.getByText("Hard")).toBeInTheDocument();
  });

  it("shows checkmark for enabled double shift and overlap", () => {
    render(<TaskInfoPopover config={makeCustomConfig()} onClose={onClose} />);
    const checkmarks = screen.getAllByText("✓");
    expect(checkmarks.length).toBe(2); // doubleShift + overlap
  });

  it("shows cross for disabled double shift and overlap", () => {
    const config: TaskConfigSummaryDto = {
      ...makeDefaultConfig(),
      dailyStartTime: "06:00", // non-default to show full popover
      dailyEndTime: "18:00",
    };
    render(<TaskInfoPopover config={config} onClose={onClose} />);
    const crosses = screen.getAllByText("✗");
    expect(crosses.length).toBe(2); // doubleShift + overlap
  });
});
