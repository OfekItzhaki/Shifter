"use client";

import { useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import { setLocaleCookie } from "@/lib/auth/authGuardCookie";
import { LOCALE_META, SUPPORTED_LOCALES, type Locale } from "@/lib/i18n/locales";

interface Props {
  /** "sidebar" = dark sidebar style (default), "auth" = light card style */
  variant?: "sidebar" | "auth";
}

export default function LanguageSwitcher({ variant = "sidebar" }: Props) {
  const locale = useLocale();
  const router = useRouter();

  function switchLocale(code: Locale) {
    if (code === locale) return;
    setLocaleCookie(code);
    router.refresh();
  }

  const stroke = variant === "auth" ? "#94a3b8" : "#64748b";
  const wrapperStyle = variant === "auth"
    ? { display: "flex", alignItems: "center", justifyContent: "center", gap: 6 }
    : { padding: "8px 12px", display: "flex", alignItems: "center", gap: 6 };
  const gap = variant === "auth" ? 4 : 2;

  return (
    <div style={wrapperStyle}>
      <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke={stroke} strokeWidth={2} style={{ flexShrink: 0 }}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M3 5h12M9 3v2m1.048 9.5A18.022 18.022 0 016.412 9m6.088 9h7M11 21l5-10 5 10M12.751 5C11.783 10.77 8.07 15.61 3 18.129" />
      </svg>
      <div style={{ display: "flex", gap }}>
        {SUPPORTED_LOCALES.map((code) => {
          const active = locale === code;
          const meta = LOCALE_META[code];
          return (
            <button
              key={code}
              onClick={() => switchLocale(code)}
              title={meta.fullLabel}
              aria-label={`Switch to ${meta.fullLabel}`}
              style={{
                background: active ? (variant === "auth" ? "rgba(14,165,233,0.1)" : "rgba(14,165,233,0.2)") : "transparent",
                border: active
                  ? "1px solid rgba(14,165,233,0.4)"
                  : variant === "auth" ? "1px solid #e2e8f0" : "1px solid transparent",
                borderRadius: variant === "auth" ? 6 : 5,
                color: active ? (variant === "auth" ? "#0ea5e9" : "#7dd3fc") : (variant === "auth" ? "#94a3b8" : "#64748b"),
                fontSize: 11,
                fontWeight: active ? 700 : 500,
                padding: variant === "auth" ? "3px 8px" : "2px 6px",
                cursor: active ? "default" : "pointer",
                transition: "all 0.15s",
              }}
            >
              {meta.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
