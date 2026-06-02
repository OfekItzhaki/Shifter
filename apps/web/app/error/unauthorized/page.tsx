"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";
import { notifyAuthTokenChanged } from "@/lib/auth/tokenState";
import { useAuthStore } from "@/lib/store/authStore";

export default function UnauthorizedPage() {
  const t = useTranslations("errorPages");

  useEffect(() => {
    localStorage.removeItem("access_token");
    localStorage.removeItem("refresh_token");
    notifyAuthTokenChanged();
    useAuthStore.getState().clearAuthState();
  }, []);

  return (
    <ErrorPageLayout
      statusCode={401}
      heading={t("unauthorized.heading")}
      message={t("unauthorized.message")}
    >
      <Link
        href="/login"
        className="inline-flex items-center justify-center min-h-[44px] min-w-[44px] bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-6 py-3 rounded-xl transition-colors"
      >
        {t("unauthorized.login")}
      </Link>
    </ErrorPageLayout>
  );
}
