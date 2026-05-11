"use client";

import { useEffect } from "react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

interface ErrorPageProps {
  error: Error & { digest?: string };
  reset: () => void;
}

/**
 * Next.js App Router error boundary page.
 * Renders when an unhandled server-side exception occurs during page rendering.
 * Must be a client component and must not depend on server-fetched data.
 */
export default function ErrorPage({ error, reset }: ErrorPageProps) {
  const t = useTranslations("errorPages");

  // Log the error to console for debugging — never expose to the user
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <ErrorPageLayout
      statusCode={500}
      heading={t("serverError.heading")}
      message={t("serverError.message")}
    >
      <button
        onClick={() => reset()}
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
