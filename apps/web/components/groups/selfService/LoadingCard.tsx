"use client";

import { useTranslations } from "next-intl";

export interface LoadingCardProps {
  /** Number of skeleton rows to display (default: 4) */
  rows?: number;
  /** Variant controls the skeleton shape to match different tab layouts */
  variant?: "list" | "form" | "slots";
}

/**
 * Shared loading skeleton component for self-service tabs.
 * Displays animated placeholder rows matching the expected tab layout.
 */
export default function LoadingCard({ rows = 4, variant = "list" }: LoadingCardProps) {
  const t = useTranslations("selfService");

  if (variant === "form") {
    return (
      <div className="space-y-4 bg-white border border-slate-200 rounded-xl p-6" aria-busy="true" aria-label={t("loading")}>
        <div className="animate-pulse space-y-5">
          {[...Array(rows)].map((_, i) => (
            <div key={i} className="space-y-2">
              <div className="h-4 bg-slate-200 rounded w-1/3" />
              <div className="h-10 bg-slate-100 rounded w-full" />
            </div>
          ))}
          <div className="h-10 bg-slate-200 rounded w-32 mt-4" />
        </div>
      </div>
    );
  }

  if (variant === "slots") {
    return (
      <div className="space-y-3" aria-busy="true" aria-label={t("loading")}>
        {[...Array(rows)].map((_, i) => (
          <div key={i} className="animate-pulse bg-white border border-slate-200 rounded-xl p-4">
            <div className="flex items-center justify-between">
              <div className="space-y-2 flex-1">
                <div className="h-4 bg-slate-200 rounded w-1/3" />
                <div className="h-3 bg-slate-100 rounded w-1/2" />
              </div>
              <div className="h-8 w-24 bg-slate-200 rounded-lg" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  // Default "list" variant
  return (
    <div className="space-y-3" aria-busy="true" aria-label={t("loading")}>
      {[...Array(rows)].map((_, i) => (
        <div key={i} className="animate-pulse bg-white border border-slate-200 rounded-xl p-4">
          <div className="flex items-center justify-between">
            <div className="space-y-2 flex-1">
              <div className="h-4 bg-slate-200 rounded w-1/3" />
              <div className="h-3 bg-slate-100 rounded w-1/2" />
            </div>
            <div className="h-6 w-16 bg-slate-200 rounded-full" />
          </div>
        </div>
      ))}
    </div>
  );
}
