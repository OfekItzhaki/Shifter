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
  const t = useTranslations("errors");

  // Log the error to console for debugging — never expose to the user
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <ErrorPageLayout
      statusCode={500}
      heading={t("server.title")}
      message={t("server.description")}
    >
      <button
        onClick={() => reset()}
        className="inline-flex items-center justify-center min-h-[44px] min-w-[44px] px-6 py-2.5 rounded-xl bg-blue-600 text-white font-medium text-sm hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-600 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500"
      >
        {t("retry")}
      </button>
      <Link
        href="/"
        className="inline-flex items-center justify-center min-h-[44px] min-w-[44px] px-6 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-medium text-sm hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500"
      >
        {t("goHome")}
      </Link>
    </ErrorPageLayout>
  );
}
