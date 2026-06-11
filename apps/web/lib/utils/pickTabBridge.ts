import type { PickerTab } from "@/components/pick/PickerTabs";

export type MyShiftsNavigationTarget = "available-slots" | "waitlist";

export function mapMyShiftsNavigationToPickerTab(target: MyShiftsNavigationTarget): PickerTab {
  return target === "available-slots" ? "slots" : "waitlist";
}
