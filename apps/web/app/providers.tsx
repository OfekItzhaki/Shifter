"use client";

import { QueryClientProvider } from "@tanstack/react-query";
import { queryClient } from "@/lib/query/queryClient";
import OfflineBanner from "@/components/shell/OfflineBanner";
import ThemeProvider from "@/components/ThemeProvider";

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <OfflineBanner />
        {children}
      </ThemeProvider>
    </QueryClientProvider>
  );
}
