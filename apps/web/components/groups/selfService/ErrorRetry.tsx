"use client";

import { useTranslations } from "next-intl";

export interface ErrorRetryProps {
  /** Error message to display. Falls back to generic i18n error message if not provided. */
  message?: string;
  /** Callback triggered when the user clicks the retry button */
  onRetry: () => void;
}

/**
 * Shared error state component for self-service tabs.
 * Displays a Hebrew error message with a "נסה שוב" (Retry) button.
 */
export default function ErrorRetry({ message, onRetry }: ErrorRetryProps) {
  const t = useTranslations("selfService");

  const displayMessage = message?.startsWith("selfService.")
    ? t(message.replace(/^selfService\./, ""))
    : message || t("error");

  return (
    <div className="flex flex-col items-center justify-center py-16 bg-white rounded-xl border border-slate-200">
      <div className="flex items-center justify-center w-12 h-12 rounded-full bg-slate-100 mb-3">
        <svg
          width={24}
          height={24}
          viewBox="0 0 24 24"
          fill="none"
          className="text-slate-400"
          stroke="currentColor"
          strokeWidth={1.5}
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
          <line x1="12" y1="9" x2="12" y2="13" />
          <circle cx="12" cy="17" r="0.5" fill="currentColor" stroke="none" />
        </svg>
      </div>
      <p className="text-sm text-slate-600 mb-4">{displayMessage}</p>
      <button
        onClick={onRetry}
        className="inline-flex items-center justify-center px-5 py-2 rounded-xl bg-sky-600 text-white text-sm font-medium hover:bg-sky-700 transition-colors"
      >
        {t("retry")}
      </button>
    </div>
  );
}
