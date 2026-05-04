"use client";

import { useLocale, useTranslations } from "next-intl";

const LOCALES = [
  { code: "he", label: "עב", full: "עברית" },
  { code: "en", label: "EN", full: "English" },
  { code: "ru", label: "RU", full: "Русский" },
] as const;

interface Props {
  /** "sidebar" = dark sidebar style (default), "auth" = light card style */
  variant?: "sidebar" | "auth";
}

export default function LanguageSwitcher({ variant = "sidebar" }: Props) {
  const locale = useLocale();
  const t = useTranslations("language");

  function switchLocale(code: string) {
    if (code === locale) return;
    document.cookie = `locale=${code}; path=/; max-age=31536000; SameSite=Strict`;
    window.location.reload();
  }

  if (variant === "auth") {
    return (
      <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 6 }}>
        <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="#94a3b8" strokeWidth={2} style={{ flexShrink: 0 }}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M3 5h12M9 3v2m1.048 9.5A18.022 18.022 0 016.412 9m6.088 9h7M11 21l5-10 5 10M12.751 5C11.783 10.77 8.07 15.61 3 18.129" />
        </svg>
        <div style={{ display: "flex", gap: 4 }}>
          {LOCALES.map(l => (
            <button
              key={l.code}
              onClick={() => switchLocale(l.code)}
              title={l.full}
              aria-label={`Switch to ${l.full}`}
              style={{
                background: locale === l.code ? "rgba(59,130,246,0.1)" : "transparent",
                border: locale === l.code ? "1px solid rgba(59,130,246,0.4)" : "1px solid #e2e8f0",
                borderRadius: 6,
                color: locale === l.code ? "#3b82f6" : "#94a3b8",
                fontSize: 11,
                fontWeight: locale === l.code ? 700 : 500,
                padding: "3px 8px",
                cursor: locale === l.code ? "default" : "pointer",
                transition: "all 0.15s",
              }}
            >
              {l.label}
            </button>
          ))}
        </div>
      </div>
    );
  }

  // sidebar variant (original style)
  return (
    <div style={{ padding: "8px 12px", display: "flex", alignItems: "center", gap: 6 }}>
      <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="#64748b" strokeWidth={2} style={{ flexShrink: 0 }}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M3 5h12M9 3v2m1.048 9.5A18.022 18.022 0 016.412 9m6.088 9h7M11 21l5-10 5 10M12.751 5C11.783 10.77 8.07 15.61 3 18.129" />
      </svg>
      <div style={{ display: "flex", gap: 2 }}>
        {LOCALES.map(l => (
          <button
            key={l.code}
            onClick={() => switchLocale(l.code)}
            title={l.full}
            aria-label={`Switch to ${l.full}`}
            style={{
              background: locale === l.code ? "rgba(59,130,246,0.2)" : "transparent",
              border: locale === l.code ? "1px solid rgba(59,130,246,0.4)" : "1px solid transparent",
              borderRadius: 5,
              color: locale === l.code ? "#93c5fd" : "#64748b",
              fontSize: 11,
              fontWeight: locale === l.code ? 700 : 500,
              padding: "2px 6px",
              cursor: locale === l.code ? "default" : "pointer",
              transition: "all 0.15s",
            }}
          >
            {l.label}
          </button>
        ))}
      </div>
    </div>
  );
}
