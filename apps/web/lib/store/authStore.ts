import { create } from "zustand";
import { persist } from "zustand/middleware";
import { login as apiLogin, logout as apiLogout } from "@/lib/api/auth";
import { detectBrowserLocale } from "@/lib/utils/detectLocale";

interface AuthState {
  userId: string | null;
  displayName: string | null;
  preferredLocale: string;
  isAuthenticated: boolean;
  // Admin mode is scoped to a specific group — null means not in admin mode
  adminGroupId: string | null;

  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  enterAdminMode: (groupId: string) => void;
  exitAdminMode: () => void;
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
      isAuthenticated: false,
      adminGroupId: null,

      get isAdminMode() { return get().adminGroupId !== null; },

      login: async (email, password) => {
        const result = await apiLogin(email, password);
        localStorage.setItem("access_token", result.accessToken);
        localStorage.setItem("refresh_token", result.refreshToken);
        localStorage.removeItem("jobuler-space");
        document.cookie = `access_token=${result.accessToken}; path=/; max-age=900; SameSite=Strict`;
        // Use server-returned locale, or fall back to browser detection
        const locale = result.preferredLocale || detectBrowserLocale();
        document.cookie = `locale=${locale}; path=/; max-age=31536000; SameSite=Strict`;
        set({
          userId: result.userId,
          displayName: result.displayName,
          preferredLocale: locale,
          isAuthenticated: true,
          adminGroupId: null,
        });
      },

      logout: async () => {
        const refreshToken = localStorage.getItem("refresh_token");
        if (refreshToken) {
          try { await apiLogout(refreshToken); } catch { /* best effort */ }
        }
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        // Clear persisted space so the next user gets a fresh space resolution
        localStorage.removeItem("jobuler-space");
        document.cookie = "access_token=; path=/; max-age=0";
        document.cookie = "locale=; path=/; max-age=0";
        set({ userId: null, displayName: null, isAuthenticated: false, adminGroupId: null });
      },

      enterAdminMode: (groupId: string) => set({ adminGroupId: groupId }),
      exitAdminMode: () => set({ adminGroupId: null }),
      isAdminForGroup: (groupId: string) => get().adminGroupId === groupId,
    }),
    {
      name: "jobuler-auth",
      partialize: (state) => ({
        userId: state.userId,
        displayName: state.displayName,
        preferredLocale: state.preferredLocale,
        isAuthenticated: state.isAuthenticated,
        // Don't persist adminGroupId — always reset on page load
      }),
    }
  )
);
