"use client";

import { useEffect, useRef } from "react";
import { refreshSession } from "@/lib/api/auth";
import { hasAuthGuardCookie, setAuthGuardCookie, setLocaleCookie } from "@/lib/auth/authGuardCookie";
import { hasStoredAccessToken, notifyAuthTokenChanged } from "@/lib/auth/tokenState";
import { useAuthStore } from "@/lib/store/authStore";
import { detectBrowserLocale } from "@/lib/utils/detectLocale";

export function useSessionBootstrap(): void {
  const attemptedRef = useRef(false);

  useEffect(() => {
    if (attemptedRef.current) return;
    attemptedRef.current = true;

    if (hasStoredAccessToken() || !hasAuthGuardCookie()) return;

    let cancelled = false;

    async function bootstrapFromRefreshCookie() {
      try {
        const session = await refreshSession();
        if (cancelled) return;

        localStorage.setItem("access_token", session.accessToken);
        localStorage.removeItem("refresh_token");
        notifyAuthTokenChanged();
        setAuthGuardCookie();

        const locale = session.preferredLocale || detectBrowserLocale();
        setLocaleCookie(locale);
        useAuthStore.setState({
          userId: session.userId,
          displayName: session.displayName,
          preferredLocale: locale,
          isAuthenticated: true,
          isPlatformAdmin: session.isPlatformAdmin ?? false,
          adminGroupId: null,
          timezoneId: session.timezoneId ?? "Asia/Jerusalem",
          timezoneOffsetMinutes: session.timezoneOffsetMinutes ?? 120,
        });
      } catch {
        // A missing/expired httpOnly refresh cookie simply means the browser is anonymous.
      }
    }

    bootstrapFromRefreshCookie();

    return () => {
      cancelled = true;
    };
  }, []);
}
