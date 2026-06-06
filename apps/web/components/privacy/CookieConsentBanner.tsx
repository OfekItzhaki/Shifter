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
  const [showOptions, setShowOptions] = useState(false);

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
      className="fixed inset-x-3 bottom-3 z-[1400] mx-auto max-w-3xl rounded-lg border border-slate-200 bg-white p-3 shadow-xl dark:border-slate-700 dark:bg-slate-900 sm:p-4"
      aria-label={t("title")}
    >
      <div className="flex flex-col gap-3">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-sm leading-relaxed text-slate-700 dark:text-slate-300">
            <span className="font-semibold text-slate-900 dark:text-white">{t("title")}: </span>
            {t("summary")}
          </p>
          <div className="flex shrink-0 gap-2">
            <button
              type="button"
              onClick={() => choose("declined")}
              className="rounded-md bg-sky-600 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-sky-500"
            >
              {t("understood")}
            </button>
            <button
              type="button"
              onClick={() => setShowOptions((value) => !value)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800"
              aria-expanded={showOptions}
            >
              {t("options")}
            </button>
          </div>
        </div>
        {showOptions ? (
          <div className="rounded-md border border-slate-200 bg-slate-50 p-3 text-sm leading-relaxed text-slate-700 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-300">
            <p>
              {t("body")}{" "}
              <Link href="/privacy" className="text-sky-600 hover:underline">
                {t("privacyLink")}
              </Link>
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => choose("accepted")}
                className="rounded-md bg-sky-600 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-sky-500"
              >
                {t("accept")}
              </button>
              <button
                type="button"
                onClick={() => choose("declined")}
                className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                {t("decline")}
              </button>
            </div>
          </div>
        ) : null}
      </div>
    </aside>
  );
}
