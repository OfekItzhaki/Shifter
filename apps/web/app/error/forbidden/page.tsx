"use client";

import { Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import Link from "next/link";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

function ForbiddenContent() {
  const t = useTranslations("errorPages");
  const router = useRouter();
  const searchParams = useSearchParams();
  const from = searchParams.get("from");

  function handleGoBack() {
    if (from) {
      router.push(from);
    } else {
      router.back();
    }
  }

  return (
    <ErrorPageLayout
      statusCode={403}
      heading={t("forbidden.heading")}
      message={t("forbidden.message")}
    >
      <Link
        href="/"
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg bg-sky-600 text-white font-medium text-sm hover:bg-sky-700 transition-colors"
      >
        {t("forbidden.goHome")}
      </Link>
      <button
        onClick={handleGoBack}
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-slate-800 text-gray-700 dark:text-gray-200 font-medium text-sm hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
      >
        {t("forbidden.goBack")}
      </button>
    </ErrorPageLayout>
  );
}

export default function ForbiddenPage() {
  return (
    <Suspense fallback={null}>
      <ForbiddenContent />
    </Suspense>
  );
}
