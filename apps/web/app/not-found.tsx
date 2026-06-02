"use client";

import Link from "next/link";
import { useTranslations } from "next-intl";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

export default function NotFound() {
  const t = useTranslations("errorPages.notFound");

  return (
    <ErrorPageLayout
      statusCode={404}
      heading={t("heading")}
      message={t("message")}
    >
      <Link
        href="/"
        className="inline-flex items-center justify-center min-h-[44px] min-w-[44px] px-6 py-2.5 rounded-xl bg-sky-600 text-white text-sm font-medium hover:bg-sky-700 dark:bg-sky-500 dark:hover:bg-sky-600 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-500"
      >
        {t("goHome")}
      </Link>
    </ErrorPageLayout>
  );
}
