"use client";

import { useEffect, useState } from "react";
import { useThemeStore, resolveTheme, type Theme } from "@/lib/store/themeStore";

const OPTIONS: { value: Theme; icon: React.ReactNode; label: string }[] = [
  {
    value: "light",
    label: "Light",
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2m0 16v2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M2 12h2m16 0h2M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42" />
      </svg>
    ),
  },
  {
    value: "dark",
    label: "Dark",
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
        <path d="M21 12.8A8.6 8.6 0 1 1 11.2 3 6.7 6.7 0 0 0 21 12.8Z" />
      </svg>
    ),
  },
  {
    value: "system",
    label: "Auto",
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
        <rect x="3" y="4" width="18" height="12" rx="2" />
        <path d="M8 20h8m-4-4v4" />
      </svg>
    ),
  },
];

export default function DarkModeToggle() {
  const { theme, setTheme } = useThemeStore();
  const [resolvedTheme, setResolvedTheme] = useState<"light" | "dark">("light");

  useEffect(() => {
    const update = () => setResolvedTheme(resolveTheme(theme));
    update();

    if (theme !== "system") return;

    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    mq.addEventListener("change", update);
    return () => mq.removeEventListener("change", update);
  }, [theme]);

  return (
    <div style={{ padding: "8px 12px", display: "flex", alignItems: "center", gap: 6 }}>
      <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="var(--sidebar-muted)" strokeWidth={2} style={{ flexShrink: 0 }}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
      </svg>
      <div style={{ display: "flex", gap: 2 }} aria-label={`Theme: ${theme === "system" ? `Auto, currently ${resolvedTheme}` : theme}`}>
        {OPTIONS.map((opt) => {
          const active = theme === opt.value;
          const label = opt.value === "system" ? `Auto theme, currently ${resolvedTheme}` : opt.label;

          return (
            <button
              key={opt.value}
              onClick={() => setTheme(opt.value)}
              title={opt.value === "system" ? `Auto (${resolvedTheme} now)` : opt.label}
              aria-label={label}
              aria-pressed={active}
              style={{
                width: 26,
                height: 24,
                position: "relative",
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                background: active ? "rgba(14,165,233,0.2)" : "transparent",
                border: active ? "1px solid rgba(14,165,233,0.4)" : "1px solid transparent",
                borderRadius: 6,
                color: active ? "var(--sidebar-link-active-fg)" : "var(--sidebar-muted)",
                cursor: active ? "default" : "pointer",
                transition: "all 0.15s",
              }}
            >
              {opt.icon}
              {opt.value === "system" ? (
                <span
                  aria-hidden="true"
                  style={{
                    position: "absolute",
                    right: 3,
                    bottom: 3,
                    width: 5,
                    height: 5,
                    borderRadius: "50%",
                    background: resolvedTheme === "dark" ? "#38bdf8" : "#fbbf24",
                    boxShadow: "0 0 0 1px rgba(15,23,42,0.9)",
                  }}
                />
              ) : null}
            </button>
          );
        })}
      </div>
    </div>
  );
}
