import { create } from "zustand";
import { persist } from "zustand/middleware";
import { login as apiLogin, logout as apiLogout } from "@/lib/api/auth";
import type { MeDto } from "@/lib/api/auth";
import { detectBrowserLocale } from "@/lib/utils/detectLocale";
import { notifyAuthTokenChanged } from "@/lib/auth/tokenState";
import { clearAuthGuardCookie, clearLocaleCookie, setAuthGuardCookie, setLocaleCookie } from "@/lib/auth/authGuardCookie";

interface AuthState {
  userId: string | null;
  displayName: string | null;
  preferredLocale: string;
  timeFormat: "24h" | "12h";
  isAuthenticated: boolean;
  isPlatformAdmin: boolean;
  // Admin mode is scoped to a specific group — null means not in admin mode
  adminGroupId: string | null;
  // Timezone fields — resolved from user's Country/State at login/refresh
  timezoneId: string | null;
  timezoneOffsetMinutes: number;

  login: (identifier: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  enterAdminMode: (groupId: string) => void;
  exitAdminMode: () => void;
  setTimeFormat: (format: "24h" | "12h") => void;
  setTimezone: (timezoneId: string | null, offsetMinutes: number) => void;
  syncFromMe: (me: Pick<MeDto, "userId" | "displayName" | "isPlatformAdmin">) => void;
  clearAuthState: () => void;
  // Convenience: is the user in admin mode for a specific group?
  isAdminForGroup: (groupId: string) => boolean;
  // Legacy: global admin mode check (true if admin for ANY group)
  isAdminMode: boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      userId: null,
      displayName: null,
      preferredLocale: "he",
      timeFormat: "24h",
      isAuthenticated: false,
      isPlatformAdmin: false,
      adminGroupId: null,
      timezoneId: "Asia/Jerusalem",
      timezoneOffsetMinutes: 120,

      get isAdminMode() { return get().adminGroupId !== null; },

      login: async (identifier, password) => {
        const result = await apiLogin(identifier, password);
        localStorage.setItem("access_token", result.accessToken);
        localStorage.removeItem("refresh_token");
        notifyAuthTokenChanged();
        // Don't clear jobuler-space on re-login — preserve the user's space selection.
        // Only clear on logout (different user scenario).
        setAuthGuardCookie();
        // Use server-returned locale, or fall back to browser detection
        const locale = result.preferredLocale || detectBrowserLocale();
        setLocaleCookie(locale);
        set({
          userId: result.userId,
          displayName: result.displayName,
          preferredLocale: locale,
          isAuthenticated: true,
          isPlatformAdmin: result.isPlatformAdmin ?? false,
          adminGroupId: null,
          timezoneId: result.timezoneId ?? "Asia/Jerusalem",
          timezoneOffsetMinutes: result.timezoneOffsetMinutes ?? 120,
        });
      },

      logout: async () => {
        try { await apiLogout(); } catch { /* best effort */ }
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        notifyAuthTokenChanged();
        // Clear persisted space so the next user gets a fresh space resolution
        localStorage.removeItem("jobuler-space");
        clearAuthGuardCookie();
        clearLocaleCookie();
        get().clearAuthState();
      },

      enterAdminMode: (groupId: string) => set({ adminGroupId: groupId }),
      exitAdminMode: () => set({ adminGroupId: null }),
      setTimeFormat: (format: "24h" | "12h") => set({ timeFormat: format }),
      setTimezone: (timezoneId: string | null, offsetMinutes: number) => set({
        timezoneId: timezoneId ?? "Asia/Jerusalem",
        timezoneOffsetMinutes: offsetMinutes ?? 120,
      }),
      syncFromMe: (me) => set({
        userId: me.userId,
        displayName: me.displayName,
        isAuthenticated: true,
        isPlatformAdmin: me.isPlatformAdmin ?? get().isPlatformAdmin,
      }),
      clearAuthState: () => set({
        userId: null,
        displayName: null,
        isAuthenticated: false,
        isPlatformAdmin: false,
        adminGroupId: null,
      }),
      isAdminForGroup: (groupId: string) => get().adminGroupId !== null,
    }),
    {
      name: "jobuler-auth",
      partialize: (state) => ({
        userId: state.userId,
        displayName: state.displayName,
        preferredLocale: state.preferredLocale,
        timeFormat: state.timeFormat,
        isAuthenticated: state.isAuthenticated,
        isPlatformAdmin: state.isPlatformAdmin,
        timezoneId: state.timezoneId,
        timezoneOffsetMinutes: state.timezoneOffsetMinutes,
        adminGroupId: state.adminGroupId,
      }),
    }
  )
);
