"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { initPostHog } from "@/lib/analytics/posthog";
import { isRtl as isRtlLocale } from "@/lib/i18n/locales";
import {
  getAnalyticsConsent,
  setAnalyticsConsent,
  type AnalyticsConsent,
} from "@/lib/privacy/consent";

export default function CookieConsentBanner() {
  const t = useTranslations("privacyConsent");
  const locale = useLocale();
  const [consent, setConsent] = useState<AnalyticsConsent | null | "loading">("loading");

  useEffect(() => {
    setConsent(getAnalyticsConsent());
  }, []);

  function choose(value: AnalyticsConsent) {
    setAnalyticsConsent(value);
    setConsent(value);
    if (value === "accepted") {
      initPostHog();
    }
  }

  if (consent !== null) return null;

  const isRtl = isRtlLocale(locale);

  return (
    <aside
      dir={isRtl ? "rtl" : "ltr"}
      className="fixed inset-x-3 bottom-3 z-[1400] mx-auto max-w-3xl rounded-lg border border-slate-200 bg-white p-4 shadow-xl dark:border-slate-700 dark:bg-slate-900"
      aria-label={t("title")}
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="text-sm leading-relaxed text-slate-700 dark:text-slate-300">
          <p className="font-semibold text-slate-900 dark:text-white">{t("title")}</p>
          <p className="mt-1">
            {t("body")}{" "}
            <Link href="/privacy" className="text-sky-600 hover:underline">
              {t("privacyLink")}
            </Link>
          </p>
        </div>
        <div className="flex shrink-0 gap-2">
          <button
            type="button"
            onClick={() => choose("declined")}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            {t("decline")}
          </button>
          <button
            type="button"
            onClick={() => choose("accepted")}
            className="rounded-md bg-sky-600 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-sky-500"
          >
            {t("accept")}
          </button>
        </div>
      </div>
    </aside>
  );
}
