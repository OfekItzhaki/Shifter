"use client";

import Link from "next/link";
import { useTranslations } from "next-intl";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

export default function NotFound() {
  const t = useTranslations("errors");

  return (
    <ErrorPageLayout
      statusCode={404}
      heading={t("notFound.title")}
      message={t("notFound.description")}
    >
      <Link
        href="/"
        className="inline-flex items-center justify-center min-h-[44px] min-w-[44px] px-6 py-2.5 rounded-xl bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-600 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500"
      >
        {t("goHome")}
      </Link>
    </ErrorPageLayout>
  );
}
