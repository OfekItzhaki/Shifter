"use client";

import { useTranslations } from "next-intl";

export type PickerTab = "status" | "slots" | "my-shifts" | "waitlist" | "swaps";

export interface PickerTabsProps {
  activeTab: PickerTab;
  onTabChange: (tab: PickerTab) => void;
}

/**
 * Tab switcher for the shift picker view.
 * Renders two tabs: "משמרות פנויות" (available slots) and "המשמרות שלי" (my shifts).
 * Uses 44x44px minimum tap targets for mobile accessibility.
 *
 * Validates: Requirements 5.1, 6.1
 */
export default function PickerTabs({ activeTab, onTabChange }: PickerTabsProps) {
  const t = useTranslations("pick");

  const tabs: { id: PickerTab; label: string }[] = [
    { id: "status", label: t("tabs.status") },
    { id: "slots", label: t("tabs.slots") },
    { id: "my-shifts", label: t("tabs.myShifts") },
    { id: "waitlist", label: t("tabs.waitlist") },
    { id: "swaps", label: t("tabs.swaps") },
  ];

  return (
    <div
      className="flex gap-1 overflow-x-auto bg-slate-100 p-1 rounded-xl"
      role="tablist"
      aria-label={t("title")}
    >
      {tabs.map((tab) => (
        <button
          key={tab.id}
          role="tab"
          aria-selected={activeTab === tab.id}
          aria-controls={`panel-${tab.id}`}
          onClick={() => onTabChange(tab.id)}
          className={`min-h-[44px] min-w-fit flex-1 px-4 py-2 rounded-lg text-sm font-medium whitespace-nowrap transition-all ${
            activeTab === tab.id
              ? "bg-white text-slate-900 shadow-sm"
              : "text-slate-500 hover:text-slate-700"
          }`}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}
