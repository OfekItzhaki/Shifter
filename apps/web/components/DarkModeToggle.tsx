"use client";

import { useThemeStore, resolveTheme, type Theme } from "@/lib/store/themeStore";

const OPTIONS: { value: Theme; icon: string; label: string }[] = [
  { value: "light", icon: "☀️", label: "Light" },
  { value: "dark", icon: "🌙", label: "Dark" },
  { value: "system", icon: "💻", label: "Auto" },
];

export default function DarkModeToggle() {
  const { theme, setTheme } = useThemeStore();

  return (
    <div style={{ padding: "8px 12px", display: "flex", alignItems: "center", gap: 6 }}>
      <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="#64748b" strokeWidth={2} style={{ flexShrink: 0 }}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
      </svg>
      <div style={{ display: "flex", gap: 2 }}>
        {OPTIONS.map(opt => (
          <button
            key={opt.value}
            onClick={() => setTheme(opt.value)}
            title={opt.label}
            aria-label={opt.label}
            style={{
              background: theme === opt.value ? "rgba(59,130,246,0.2)" : "transparent",
              border: theme === opt.value ? "1px solid rgba(14,165,233,0.4)" : "1px solid transparent",
              borderRadius: 5,
              fontSize: 12,
              padding: "2px 6px",
              cursor: theme === opt.value ? "default" : "pointer",
              transition: "all 0.15s",
            }}
          >
            {opt.icon}
          </button>
        ))}
      </div>
    </div>
  );
}
