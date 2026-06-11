import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ShiftTemplatesTab from "../../components/groups/selfService/ShiftTemplatesTab";
import type { GroupTaskDto } from "../../lib/api/tasks";

const mockListShiftTemplates = vi.fn();
const mockCreateShiftTemplate = vi.fn();
const mockUpdateShiftTemplate = vi.fn();
const mockDeleteShiftTemplate = vi.fn();

const translations: Record<string, string> = {
  title: "Shift templates",
  createButton: "Add template",
  noTemplates: "No templates",
  editButton: "Edit",
  deleteButton: "Delete",
  deleteConfirmTitle: "Delete template",
  deleteConfirmMessage: "Delete this template?",
  deleteConfirmNo: "Keep",
  deleteConfirmYes: "Delete template",
  creating: "Creating...",
  saving: "Saving...",
  deleting: "Deleting...",
  "form.dayOfWeek": "Day",
  "form.selectDay": "Select day",
  "form.task": "Task",
  "form.selectTask": "Select task",
  "form.startTime": "Start",
  "form.endTime": "End",
  "form.requiredHeadcount": "people",
  "form.cancel": "Cancel",
  "form.submit": "Save",
  "validation.startAfterEnd": "Start must be before end",
  "validation.headcountRange": "Headcount must be between 1 and 999",
  "days.sunday": "Sunday",
  "days.monday": "Monday",
  "days.tuesday": "Tuesday",
  "days.wednesday": "Wednesday",
  "days.thursday": "Thursday",
  "days.friday": "Friday",
  "days.saturday": "Saturday",
};

vi.mock("next-intl", () => ({
  useLocale: () => "en",
  useTranslations: () => (key: string) => translations[key] ?? key,
}));

vi.mock("../../lib/api/selfService", () => ({
  listShiftTemplates: (...args: unknown[]) => mockListShiftTemplates(...args),
  createShiftTemplate: (...args: unknown[]) => mockCreateShiftTemplate(...args),
  updateShiftTemplate: (...args: unknown[]) => mockUpdateShiftTemplate(...args),
  deleteShiftTemplate: (...args: unknown[]) => mockDeleteShiftTemplate(...args),
}));

const tasks: GroupTaskDto[] = [
  {
    id: "task-1",
    name: "Front desk",
    startsAt: "2026-06-20T00:00:00Z",
    endsAt: "2026-06-27T00:00:00Z",
    shiftDurationMinutes: 480,
    requiredHeadcount: 1,
    burdenLevel: "Normal",
    effectiveBurdenLevel: "Normal",
    splitCount: 1,
    allowsDoubleShift: false,
    allowsOverlap: false,
    dailyStartTime: null,
    dailyEndTime: null,
    qualificationRequirements: [],
    createdAt: "2026-06-10T00:00:00Z",
    updatedAt: "2026-06-10T00:00:00Z",
  },
  {
    id: "task-2",
    name: "Gate",
    startsAt: "2026-06-20T00:00:00Z",
    endsAt: "2026-06-27T00:00:00Z",
    shiftDurationMinutes: 480,
    requiredHeadcount: 2,
    burdenLevel: "Normal",
    effectiveBurdenLevel: "Normal",
    splitCount: 1,
    allowsDoubleShift: false,
    allowsOverlap: false,
    dailyStartTime: null,
    dailyEndTime: null,
    qualificationRequirements: [],
    createdAt: "2026-06-10T00:00:00Z",
    updatedAt: "2026-06-10T00:00:00Z",
  },
];

function makeTemplate(overrides: Record<string, unknown> = {}) {
  return {
    id: "template-1",
    spaceId: "space-1",
    groupId: "group-1",
    groupTaskId: "task-1",
    groupTaskName: "Front desk",
    dayOfWeek: 1,
    startTime: "09:00:00",
    endTime: "17:00:00",
    requiredHeadcount: 1,
    isDeleted: false,
    createdAt: "2026-06-10T00:00:00Z",
    updatedAt: "2026-06-10T00:00:00Z",
    ...overrides,
  };
}

function getTimeInputs(container: HTMLElement) {
  return Array.from(container.querySelectorAll<HTMLInputElement>('input[type="time"]'));
}

function hasText(text: string) {
  return (_content: string, element: Element | null) =>
    element?.textContent?.includes(text) ?? false;
}

describe("ShiftTemplatesTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockListShiftTemplates.mockResolvedValue([makeTemplate()]);
    mockCreateShiftTemplate.mockResolvedValue({ id: "template-2" });
    mockUpdateShiftTemplate.mockResolvedValue(makeTemplate());
    mockDeleteShiftTemplate.mockResolvedValue(undefined);
  });

  it("creates, edits, and deletes shift templates", async () => {
    mockListShiftTemplates
      .mockResolvedValueOnce([makeTemplate()])
      .mockResolvedValueOnce([makeTemplate(), makeTemplate({
        id: "template-2",
        groupTaskId: "task-2",
        groupTaskName: "Gate",
        dayOfWeek: 2,
        startTime: "10:00:00",
        endTime: "18:00:00",
        requiredHeadcount: 2,
      })])
      .mockResolvedValueOnce([makeTemplate({
        startTime: "08:00:00",
        endTime: "16:00:00",
        requiredHeadcount: 3,
      })])
      .mockResolvedValueOnce([]);

    const { container } = render(<ShiftTemplatesTab spaceId="space-1" groupId="group-1" tasks={tasks} />);

    expect((await screen.findAllByText(hasText("Front desk"))).length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole("button", { name: "Add template" }));
    const createComboboxes = screen.getAllByRole("combobox");
    fireEvent.change(createComboboxes[0], { target: { value: "2" } });
    fireEvent.change(createComboboxes[1], { target: { value: "task-2" } });
    let timeInputs = getTimeInputs(container);
    fireEvent.change(timeInputs[0], { target: { value: "10:00" } });
    fireEvent.change(timeInputs[1], { target: { value: "18:00" } });
    fireEvent.change(screen.getByRole("spinbutton"), { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(mockCreateShiftTemplate).toHaveBeenCalledWith("space-1", "group-1", {
        dayOfWeek: 2,
        startTime: "10:00",
        endTime: "18:00",
        requiredHeadcount: 2,
        groupTaskId: "task-2",
      });
    });
    expect((await screen.findAllByText(hasText("Gate"))).length).toBeGreaterThan(0);

    fireEvent.click(screen.getAllByRole("button", { name: "Edit" })[0]);
    const editComboboxes = screen.getAllByRole("combobox");
    fireEvent.change(editComboboxes[0], { target: { value: "1" } });
    fireEvent.change(editComboboxes[1], { target: { value: "task-1" } });
    timeInputs = getTimeInputs(container);
    fireEvent.change(timeInputs[0], { target: { value: "08:00" } });
    fireEvent.change(timeInputs[1], { target: { value: "16:00" } });
    fireEvent.change(screen.getByRole("spinbutton"), { target: { value: "3" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(mockUpdateShiftTemplate).toHaveBeenCalledWith("space-1", "group-1", "template-1", {
        dayOfWeek: 1,
        startTime: "08:00",
        endTime: "16:00",
        requiredHeadcount: 3,
        groupTaskId: "task-1",
      });
    });
    expect(await screen.findByText(/08:00/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    fireEvent.click(screen.getByRole("button", { name: "Delete template" }));

    await waitFor(() => {
      expect(mockDeleteShiftTemplate).toHaveBeenCalledWith("space-1", "group-1", "template-1");
    });
    expect(await screen.findByText("No templates")).toBeInTheDocument();
  });

  it("blocks invalid template time ranges before calling the API", async () => {
    mockListShiftTemplates.mockResolvedValue([]);
    const { container } = render(<ShiftTemplatesTab spaceId="space-1" groupId="group-1" tasks={tasks} />);

    fireEvent.click(await screen.findByRole("button", { name: "Add template" }));
    const createComboboxes = screen.getAllByRole("combobox");
    fireEvent.change(createComboboxes[0], { target: { value: "2" } });
    fireEvent.change(createComboboxes[1], { target: { value: "task-1" } });
    const timeInputs = getTimeInputs(container);
    fireEvent.change(timeInputs[0], { target: { value: "18:00" } });
    fireEvent.change(timeInputs[1], { target: { value: "10:00" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Start must be before end")).toBeInTheDocument();
    expect(mockCreateShiftTemplate).not.toHaveBeenCalled();
  });
});
