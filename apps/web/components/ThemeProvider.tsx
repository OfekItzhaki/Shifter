"use client";

import { useEffect } from "react";
import { useThemeStore, resolveTheme } from "@/lib/store/themeStore";

function applyThemeClass(resolved: "light" | "dark") {
  const html = document.documentElement;
  html.classList.toggle("dark", resolved === "dark");
  html.dataset.theme = resolved;

  const meta = document.querySelector('meta[name="theme-color"]');
  if (meta) {
    meta.setAttribute("content", resolved === "dark" ? "#1f2326" : "#f1f5f9");
  }
}

/**
 * Applies the dark/light class to <html> based on the user's theme preference.
 * Also listens for system theme changes when set to "system".
 */
export default function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { theme } = useThemeStore();

  useEffect(() => {
    applyThemeClass(resolveTheme(theme));
  }, [theme]);

  // Listen for system theme changes
  useEffect(() => {
    if (theme !== "system") return;

    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    applyThemeClass(mq.matches ? "dark" : "light");

    const handler = (e: MediaQueryListEvent) => {
      applyThemeClass(e.matches ? "dark" : "light");
    };

    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  }, [theme]);

  return <>{children}</>;
}
