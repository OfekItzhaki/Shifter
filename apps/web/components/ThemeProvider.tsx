"use client";

import { useEffect } from "react";
import { useThemeStore, resolveTheme } from "@/lib/store/themeStore";

/**
 * Applies the dark/light class to <html> based on the user's theme preference.
 * Also listens for system theme changes when set to "system".
 */
export default function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { theme } = useThemeStore();

  useEffect(() => {
    const resolved = resolveTheme(theme);
    const html = document.documentElement;

    if (resolved === "dark") {
      html.classList.add("dark");
    } else {
      html.classList.remove("dark");
    }

    // Update theme-color meta tag
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
      meta.setAttribute("content", resolved === "dark" ? "#0f172a" : "#f8fafc");
    }
  }, [theme]);

  // Listen for system theme changes
  useEffect(() => {
    if (theme !== "system") return;

    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = (e: MediaQueryListEvent) => {
      document.documentElement.classList.toggle("dark", e.matches);
    };
    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  }, [theme]);

  return <>{children}</>;
}
