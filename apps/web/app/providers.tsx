"use client";

import { useEffect, useState } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { queryClient } from "@/lib/query/queryClient";
import OfflineBanner from "@/components/shell/OfflineBanner";
import ThemeProvider from "@/components/ThemeProvider";
import AdminSessionGuard from "@/components/admin/AdminSessionGuard";
import FeedbackFab from "@/components/shell/FeedbackFab";
import { useAuthStore } from "@/lib/store/authStore";
import { initPostHog } from "@/lib/analytics/posthog";
import { initConnectivity } from "@/lib/api/client";
import { initBackgroundRefresh } from "@/lib/cache/backgroundRefresh";
import { useCacheLifecycle } from "@/lib/hooks/useCacheLifecycle";

export function Providers({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [hasAccessToken, setHasAccessToken] = useState(false);

  // Manage per-user cache lifecycle (SET_CURRENT_USER, CLEAR_USER_CACHE, CACHE_UPDATED)
  // The hook subscribes to authStore.userId internally, so it reacts when auth state changes.
  useCacheLifecycle();

  useEffect(() => {
    initPostHog();
    const cleanupConnectivity = initConnectivity();
    const cleanupBackgroundRefresh = initBackgroundRefresh();
    return () => {
      cleanupConnectivity();
      cleanupBackgroundRefresh();
    };
  }, []);

  useEffect(() => {
    function syncAccessToken() {
      setHasAccessToken(!!localStorage.getItem("access_token"));
    }

    syncAccessToken();
    window.addEventListener("storage", syncAccessToken);
    return () => window.removeEventListener("storage", syncAccessToken);
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <OfflineBanner />
        <AdminSessionGuard />
        {(isAuthenticated || hasAccessToken) && <FeedbackFab />}
        {children}
      </ThemeProvider>
    </QueryClientProvider>
  );
}
