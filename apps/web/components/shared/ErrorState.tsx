"use client";

import { useTranslations } from "next-intl";
import Link from "next/link";

export type ErrorType = "network" | "not-found" | "permission" | "server" | "generic";

export interface ErrorStateProps {
  /** The type of error — determines the default icon, title, and description */
  type?: ErrorType;
  /** Override the default title */
  title?: string;
  /** Override the default description */
  description?: string;
  /** Callback for the retry button. If omitted, retry button is hidden. */
  onRetry?: () => void;
  /** Whether to show the "Back to home" link. Defaults to true. */
  showHomeLink?: boolean;
}

/**
 * Reusable error state component for in-page errors.
 * Centered within its container, supports dark mode, and uses the app's design language.
 * Works both inside AppShell (for in-page errors) and standalone (for error.tsx/not-found.tsx).
 */
export default function ErrorState({
  type = "generic",
  title,
  description,
  onRetry,
  showHomeLink = true,
}: ErrorStateProps) {
  const t = useTranslations("errors");

  const translationKey = typeToKey(type);
  const resolvedTitle = title ?? t(`${translationKey}.title`);
  const resolvedDescription = description ?? t(`${translationKey}.description`);

  return (
    <div className="flex flex-col items-center justify-center min-h-[50vh] px-4 py-12 text-center">
      {/* Illustration */}
      <div className="mb-6">
        <ErrorIllustration type={type} />
      </div>

      {/* Title */}
      <h2 className="text-xl font-semibold text-slate-900 dark:text-slate-100 mb-2">
        {resolvedTitle}
      </h2>

      {/* Description */}
      <p className="text-sm text-slate-500 dark:text-slate-400 max-w-sm mb-8 leading-relaxed">
        {resolvedDescription}
      </p>

      {/* Actions */}
      <div className="flex flex-col sm:flex-row items-center gap-3">
        {onRetry && (
          <button
            onClick={onRetry}
            className="inline-flex items-center justify-center min-h-[44px] px-6 py-2.5 rounded-xl bg-sky-600 text-white text-sm font-medium hover:bg-sky-700 dark:bg-sky-500 dark:hover:bg-sky-600 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-500"
          >
            {t("retry")}
          </button>
        )}
        {showHomeLink && (
          <Link
            href="/"
            className="inline-flex items-center justify-center min-h-[44px] px-6 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 text-sm font-medium hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-500"
          >
            {t("goHome")}
          </Link>
        )}
      </div>
    </div>
  );
}

function typeToKey(type: ErrorType): string {
  switch (type) {
    case "network":
      return "network";
    case "not-found":
      return "notFound";
    case "permission":
      return "permission";
    case "server":
      return "server";
    case "generic":
    default:
      return "generic";
  }
}

function ErrorIllustration({ type }: { type: ErrorType }) {
  const size = 80;
  const bgClass = "bg-slate-100 dark:bg-slate-800";

  return (
    <div
      className={`flex items-center justify-center rounded-full ${bgClass}`}
      style={{ width: size, height: size }}
    >
      <svg
        width={40}
        height={40}
        viewBox="0 0 24 24"
        fill="none"
        className="text-slate-400 dark:text-slate-500"
        strokeWidth={1.5}
        strokeLinecap="round"
        strokeLinejoin="round"
        stroke="currentColor"
      >
        {getIconPath(type)}
      </svg>
    </div>
  );
}

function getIconPath(type: ErrorType) {
  switch (type) {
    case "network":
      // Broken connection / wifi-off style
      return (
        <>
          <path d="M1 1l22 22" />
          <path d="M16.72 11.06A10.94 10.94 0 0 1 19 12.55" />
          <path d="M5 12.55a10.94 10.94 0 0 1 5.17-2.39" />
          <path d="M10.71 5.05A16 16 0 0 1 22.56 9" />
          <path d="M1.42 9a15.91 15.91 0 0 1 4.7-2.88" />
          <path d="M8.53 16.11a6 6 0 0 1 6.95 0" />
          <circle cx="12" cy="20" r="1" fill="currentColor" stroke="none" />
        </>
      );
    case "not-found":
      // Search / magnifying glass with question mark
      return (
        <>
          <circle cx="11" cy="11" r="8" />
          <path d="M21 21l-4.35-4.35" />
          <path d="M11 8a3 3 0 0 0-3 3" />
          <path d="M11 14v.01" />
        </>
      );
    case "permission":
      // Lock / shield
      return (
        <>
          <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
          <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          <circle cx="12" cy="16" r="1" fill="currentColor" stroke="none" />
        </>
      );
    case "server":
      // Server with X
      return (
        <>
          <rect x="2" y="2" width="20" height="8" rx="2" ry="2" />
          <rect x="2" y="14" width="20" height="8" rx="2" ry="2" />
          <circle cx="6" cy="6" r="1" fill="currentColor" stroke="none" />
          <circle cx="6" cy="18" r="1" fill="currentColor" stroke="none" />
          <path d="M16 16l4 4" />
          <path d="M20 16l-4 4" />
        </>
      );
    case "generic":
    default:
      // Warning triangle
      return (
        <>
          <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
          <line x1="12" y1="9" x2="12" y2="13" />
          <circle cx="12" cy="17" r="0.5" fill="currentColor" stroke="none" />
        </>
      );
  }
}
