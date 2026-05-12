"use client";

import { useTranslations } from "next-intl";
import {
  useNotificationPrefsStore,
  type NotificationCategory,
} from "@/lib/store/notificationPrefsStore";

interface CategoryInfo {
  key: NotificationCategory;
  icon: string;
  color: string;
}

const CATEGORIES: CategoryInfo[] = [
  { key: "solver_completed", icon: "✓", color: "#10b981" },
  { key: "solver_infeasible", icon: "⚠", color: "#f59e0b" },
  { key: "solver_failed", icon: "✕", color: "#ef4444" },
  { key: "solver_preflight_failed", icon: "⚡", color: "#f97316" },
  { key: "schedule_published", icon: "📅", color: "#3b82f6" },
  { key: "group_alert", icon: "🔔", color: "#8b5cf6" },
];

export default function NotificationPreferences() {
  const t = useTranslations("profile.notificationPrefs");
  const { enabled, showBadge, toggle, toggleBadge, resetDefaults } =
    useNotificationPrefsStore();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-slate-100">{t("title")}</h2>
        <button
          onClick={resetDefaults}
          className="text-xs text-blue-600 hover:underline"
        >
          {t("resetDefaults")}
        </button>
      </div>

      <p className="text-xs text-slate-500 dark:text-slate-400">{t("description")}</p>

      {/* Badge toggle */}
      <div className="flex items-center justify-between py-2.5 border-b border-slate-100 dark:border-slate-700">
        <div className="flex items-center gap-3 min-w-0 flex-1">
          <span
            className="w-7 h-7 rounded-lg flex items-center justify-center text-xs flex-shrink-0"
            style={{ background: "#ef444415", color: "#ef4444" }}
          >
            ●
          </span>
          <div className="min-w-0">
            <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{t("showBadge")}</p>
            <p className="text-xs text-slate-400 dark:text-slate-500">{t("showBadgeDesc")}</p>
          </div>
        </div>
        <ToggleSwitch checked={showBadge} onChange={toggleBadge} />
      </div>

      {/* Category toggles */}
      <div className="space-y-0.5">
        {CATEGORIES.map(({ key, icon, color }) => (
          <div
            key={key}
            className="flex items-center justify-between py-2.5 border-b border-slate-50 dark:border-slate-700 last:border-0"
          >
            <div className="flex items-center gap-3 min-w-0 flex-1">
              <span
                className="w-7 h-7 rounded-lg flex items-center justify-center text-xs flex-shrink-0"
                style={{ background: `${color}15`, color }}
              >
                {icon}
              </span>
              <div className="min-w-0">
                <p className="text-sm font-medium text-slate-800 dark:text-slate-200">
                  {t(`categories.${key}`)}
                </p>
                <p className="text-xs text-slate-400 dark:text-slate-500">
                  {t(`categoriesDesc.${key}`)}
                </p>
              </div>
            </div>
            <ToggleSwitch
              checked={enabled[key]}
              onChange={() => toggle(key)}
            />
          </div>
        ))}
      </div>
    </div>
  );
}

function ToggleSwitch({
  checked,
  onChange,
}: {
  checked: boolean;
  onChange: () => void;
}) {
  return (
    <button
      role="switch"
      aria-checked={checked}
      onClick={onChange}
      className={`relative inline-flex h-6 w-10 items-center rounded-full transition-colors flex-shrink-0 ${
        checked ? "bg-blue-500" : "bg-slate-200 dark:bg-slate-500"
      }`}
    >
      <span
        className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
          checked ? "translate-x-[20px]" : "translate-x-[4px]"
        }`}
      />
    </button>
  );
}
