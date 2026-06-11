import type { PickerTab } from "@/components/pick/PickerTabs";

export type MyShiftsNavigationTarget = "available-slots" | "waitlist" | "swaps";

export function mapMyShiftsNavigationToPickerTab(target: MyShiftsNavigationTarget): PickerTab {
  if (target === "available-slots") return "slots";
  return target;
}
