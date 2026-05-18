"use client";

import { useEffect } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { queryClient } from "@/lib/query/queryClient";
import OfflineBanner from "@/components/shell/OfflineBanner";
import ThemeProvider from "@/components/ThemeProvider";
import AdminSessionGuard from "@/components/admin/AdminSessionGuard";
import FeedbackFab from "@/components/shell/FeedbackFab";
import { useAuthStore } from "@/lib/store/authStore";
import { initPostHog } from "@/lib/analytics/posthog";

export function Providers({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  useEffect(() => {
    initPostHog();
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <OfflineBanner />
        <AdminSessionGuard />
        {isAuthenticated && <FeedbackFab />}
        {children}
      </ThemeProvider>
    </QueryClientProvider>
  );
}
