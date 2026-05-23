"use client";

import { useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

/**
 * Dedicated server error page for API 5xx responses.
 * Triggered by the axios interceptor when a 500, 502, 503, or 504 response is received.
 * Uses the shared ErrorPageLayout for consistent branding.
 */
export default function ServerErrorPage() {
  const t = useTranslations("errorPages");
  const searchParams = useSearchParams();
  const from = searchParams.get("from") || "/home";

  return (
    <ErrorPageLayout
      heading={t("serverError.heading")}
      message={t("serverError.message")}
    >
      <button
        onClick={() => { window.location.href = from; }}
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg bg-sky-500 text-white font-medium text-sm hover:bg-sky-600 transition-colors"
      >
        {t("serverError.tryAgain")}
      </button>
      <Link
        href="/home"
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 font-medium text-sm hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
      >
        {t("serverError.goHome")}
      </Link>
    </ErrorPageLayout>
  );
}
