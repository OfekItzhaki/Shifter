import { describe, expect, it } from "vitest";
import { mapMyShiftsNavigationToPickerTab } from "../../lib/utils/pickTabBridge";

describe("pick tab bridge", () => {
  it("maps My Shifts navigation targets to picker tab ids", () => {
    expect(mapMyShiftsNavigationToPickerTab("available-slots")).toBe("slots");
    expect(mapMyShiftsNavigationToPickerTab("waitlist")).toBe("waitlist");
  });
});
