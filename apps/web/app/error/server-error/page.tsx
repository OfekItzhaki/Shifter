"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

export default function ServerErrorPage() {
  const t = useTranslations("errorPages");
  const [from, setFrom] = useState("/home");

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    setFrom(params.get("from") || "/home");
  }, []);

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
