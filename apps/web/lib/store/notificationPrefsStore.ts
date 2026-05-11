import { create } from "zustand";
import { persist } from "zustand/middleware";

/**
 * Notification event categories that users can toggle.
 * Maps to the eventType strings from the backend.
 */
export type NotificationCategory =
  | "solver_completed"
  | "solver_infeasible"
  | "solver_failed"
  | "solver_preflight_failed"
  | "schedule_published"
  | "group_alert";

export interface NotificationPrefs {
  /** Which categories are enabled (shown). All enabled by default. */
  enabled: Record<NotificationCategory, boolean>;
  /** Whether to show the in-app notification bell badge */
  showBadge: boolean;
  /** Toggle a specific category */
  toggle: (category: NotificationCategory) => void;
  /** Toggle badge visibility */
  toggleBadge: () => void;
  /** Reset all to defaults */
  resetDefaults: () => void;
}

const DEFAULT_ENABLED: Record<NotificationCategory, boolean> = {
  solver_completed: true,
  solver_infeasible: true,
  solver_failed: true,
  solver_preflight_failed: true,
  schedule_published: true,
  group_alert: true,
};

export const useNotificationPrefsStore = create<NotificationPrefs>()(
  persist(
    (set) => ({
      enabled: { ...DEFAULT_ENABLED },
      showBadge: true,
      toggle: (category) =>
        set((state) => ({
          enabled: { ...state.enabled, [category]: !state.enabled[category] },
        })),
      toggleBadge: () => set((state) => ({ showBadge: !state.showBadge })),
      resetDefaults: () => set({ enabled: { ...DEFAULT_ENABLED }, showBadge: true }),
    }),
    {
      name: "shifter-notification-prefs",
    }
  )
);
