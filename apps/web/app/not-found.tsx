"use client";

import Link from "next/link";
import { useTranslations } from "next-intl";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

export default function NotFound() {
  const t = useTranslations("errorPages");

  return (
    <ErrorPageLayout
      statusCode={404}
      heading={t("notFound.heading")}
      message={t("notFound.message")}
    >
      <Link
        href="/"
        className="inline-flex items-center justify-center min-h-[44px] min-w-[44px] px-6 py-2.5 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500"
      >
        {t("notFound.goHome")}
      </Link>
    </ErrorPageLayout>
  );
}
