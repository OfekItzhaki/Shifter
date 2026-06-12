"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { getProviderHealthReport, ProviderHealthReport } from "@/lib/api/platform";

type ProviderCatalogEntry = {
  label: string;
  purpose: string;
  optional: boolean;
};

const providerCatalog: Record<string, ProviderCatalogEntry> = {
  postgres: {
    label: "PostgreSQL",
    purpose: "Primary application database",
    optional: false,
  },
  redis: {
    label: "Redis",
    purpose: "Cache, sessions, and coordination",
    optional: false,
  },
  solver: {
    label: "Solver",
    purpose: "Automatic schedule generation",
    optional: false,
  },
  resend: {
    label: "Resend",
    purpose: "Email delivery",
    optional: true,
  },
  lemonsqueezy: {
    label: "LemonSqueezy",
    purpose: "Billing and subscription checkout",
    optional: true,
  },
  ai: {
    label: "AI",
    purpose: "Schedule import, scan, and assistant features",
    optional: true,
  },
  push: {
    label: "Push notifications",
    purpose: "Browser and PWA notifications",
    optional: true,
  },
};

function getProviderInfo(serviceName: string): ProviderCatalogEntry {
  return providerCatalog[serviceName.toLowerCase()] ?? {
    label: serviceName,
    purpose: "Configured service",
    optional: false,
  };
}

function statusClasses(status: string): string {
  switch (status) {
    case "healthy":
      return "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/40 dark:bg-emerald-900/20 dark:text-emerald-300";
    case "unhealthy":
      return "border-red-200 bg-red-50 text-red-700 dark:border-red-900/40 dark:bg-red-900/20 dark:text-red-300";
    case "skipped":
      return "border-slate-200 bg-slate-50 text-slate-600 dark:border-slate-700 dark:bg-slate-900/40 dark:text-slate-300";
    default:
      return "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/40 dark:bg-amber-900/20 dark:text-amber-300";
  }
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

export default function ProviderHealthPanel() {
  const t = useTranslations("platform.health");
  const [report, setReport] = useState<ProviderHealthReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [errorKey, setErrorKey] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setRefreshing(true);
    setErrorKey(null);

    try {
      setReport(await getProviderHealthReport());
    } catch {
      setErrorKey("loadError");
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    let isMounted = true;

    void getProviderHealthReport()
      .then((nextReport) => {
        if (!isMounted) return;
        setReport(nextReport);
        setErrorKey(null);
      })
      .catch(() => {
        if (!isMounted) return;
        setErrorKey("loadError");
      })
      .finally(() => {
        if (!isMounted) return;
        setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const counts = useMemo(() => {
    const checks = report?.checks ?? [];
    return {
      healthy: checks.filter((check) => check.status === "healthy").length,
      unhealthy: checks.filter((check) => check.status === "unhealthy").length,
      skipped: checks.filter((check) => check.status === "skipped").length,
      total: checks.length,
    };
  }, [report]);

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-800">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-sm font-bold text-slate-900 dark:text-white">{t("title")}</h2>
          <p className="mt-1 text-xs leading-5 text-slate-500 dark:text-slate-400">{t("description")}</p>
        </div>
        <button
          type="button"
          onClick={() => void refresh()}
          disabled={loading || refreshing}
          className="inline-flex w-fit rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-sky-700 transition hover:border-sky-200 hover:bg-sky-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:bg-slate-900 dark:text-sky-300 dark:hover:border-sky-700"
        >
          {refreshing ? t("refreshing") : t("refresh")}
        </button>
      </div>

      {loading && (
        <p className="mt-4 text-xs text-slate-400">{t("loading")}</p>
      )}

      {errorKey && (
        <p className="mt-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-900/40 dark:bg-red-900/20 dark:text-red-300">
          {t(errorKey)}
        </p>
      )}

      {report && !loading && (
        <>
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${statusClasses(report.overallStatus)}`}>
              {t(`status.${report.overallStatus}`)}
            </span>
            <span className="text-xs text-slate-500 dark:text-slate-400">
              {t("summary", counts)}
            </span>
            <span className="text-xs text-slate-400 dark:text-slate-500">
              {t("updated", { time: formatTimestamp(report.timestamp) })}
            </span>
          </div>

          <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {report.checks.map((check) => (
              <ProviderHealthCard key={check.serviceName} check={check} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}

function ProviderHealthCard({
  check,
}: {
  check: ProviderHealthReport["checks"][number];
}) {
  const t = useTranslations("platform.health");
  const provider = getProviderInfo(check.serviceName);

  return (
    <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-900/40">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-sm font-semibold text-slate-900 dark:text-white">{provider.label}</p>
            <span className="rounded-full border border-slate-200 bg-white px-2 py-0.5 text-[11px] font-semibold text-slate-500 dark:border-slate-700 dark:bg-slate-950/40 dark:text-slate-400">
              {provider.optional ? t("optional") : t("core")}
            </span>
          </div>
          <p className="mt-1 text-xs leading-5 text-slate-500 dark:text-slate-400">{provider.purpose}</p>
          {check.status === "skipped" && provider.optional && (
            <p className="mt-1 text-xs leading-5 text-slate-400 dark:text-slate-500">{t("optionalSkipped")}</p>
          )}
          {check.responseTime && (
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
              {t("responseTime", { time: check.responseTime })}
            </p>
          )}
        </div>
        <span className={`shrink-0 rounded-full border px-2 py-0.5 text-xs font-semibold ${statusClasses(check.status)}`}>
          {t(`status.${check.status}`)}
        </span>
      </div>
      {check.errorMessage && (
        <p className="mt-3 rounded-lg border border-red-200 bg-white px-3 py-2 text-xs leading-5 text-red-700 dark:border-red-900/40 dark:bg-red-950/30 dark:text-red-300">
          {check.errorMessage}
        </p>
      )}
    </div>
  );
}
