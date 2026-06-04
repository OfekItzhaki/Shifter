"use client";

import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";

type LegalLinkKey = "terms" | "privacy" | "security" | "privacyRequests" | "dpa" | "subprocessors";

const LINKS: Array<{ key: LegalLinkKey; href: string }> = [
  { key: "terms", href: "/terms" },
  { key: "privacy", href: "/privacy" },
  { key: "security", href: "/security" },
  { key: "privacyRequests", href: "/privacy-requests" },
  { key: "dpa", href: "/dpa" },
  { key: "subprocessors", href: "/subprocessors" },
];

interface LegalLinksProps {
  compact?: boolean;
  className?: string;
}

export default function LegalLinks({ compact = false, className }: LegalLinksProps) {
  const t = useTranslations("legalLinks");
  const locale = useLocale();
  const visibleLinks = compact ? LINKS.slice(0, 4) : LINKS;

  return (
    <nav
      aria-label={t("label")}
      className={className}
      dir={locale === "he" ? "rtl" : "ltr"}
      style={{
        display: "flex",
        flexWrap: "wrap",
        justifyContent: "center",
        gap: "6px 10px",
        textAlign: "center",
      }}
    >
      {visibleLinks.map((link, index) => (
        <span key={link.href} style={{ display: "inline-flex", alignItems: "center", gap: 10 }}>
          {index > 0 && <span style={{ fontSize: "0.6875rem", color: "#64748b" }}>·</span>}
          <Link href={link.href} style={{ fontSize: "0.6875rem", color: "#94a3b8", textDecoration: "none" }}>
            {t(link.key)}
          </Link>
        </span>
      ))}
    </nav>
  );
}
