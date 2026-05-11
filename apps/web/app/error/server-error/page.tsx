"use client";

import { useTranslations } from "next-intl";
import Link from "next/link";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

/**
 * Dedicated server error page for API 5xx responses.
 * Triggered by the axios interceptor when a 500, 502, 503, or 504 response is received.
 * Uses the shared ErrorPageLayout for consistent branding.
 */
export default function ServerErrorPage() {
  const t = useTranslations("errorPages");

  return (
    <ErrorPageLayout
      statusCode={500}
      heading={t("serverError.heading")}
      message={t("serverError.message")}
    >
      <button
        onClick={() => window.location.reload()}
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg bg-blue-500 text-white font-medium text-sm hover:bg-blue-600 transition-colors"
      >
        {t("serverError.tryAgain")}
      </button>
      <Link
        href="/"
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 font-medium text-sm hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
      >
        {t("serverError.goHome")}
      </Link>
    </ErrorPageLayout>
  );
}
