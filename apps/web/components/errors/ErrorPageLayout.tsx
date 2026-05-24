"use client";

import ShifterLogo from "@/components/shell/ShifterLogo";

export interface ErrorPageLayoutProps {
  /** The error heading text (already translated) */
  heading: string;
  /** The descriptive message (already translated) */
  message: string;
  /** HTTP status code displayed as a large numeral, or null for client errors */
  statusCode?: number | null;
  /** Action buttons/links rendered below the message */
  children: React.ReactNode;
}

/**
 * Shared layout component for all error pages (401, 403, 404, 500) and the ErrorBoundary fallback.
 * Centers content vertically/horizontally, displays the ShifterLogo, optional status code,
 * heading, message, and action slots. Supports dark mode and meets WCAG 2.1 AA requirements.
 */
export default function ErrorPageLayout({
  heading,
  message,
  statusCode,
  children,
}: ErrorPageLayoutProps) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-white dark:bg-slate-900 px-4">
      <div className="flex flex-col items-center text-center max-w-md w-full">
        {/* Logo */}
        <div className="mb-6">
          <ShifterLogo size={40} />
        </div>

        {/* Status code */}
        {statusCode != null && (
          <p
            className="text-7xl font-bold text-slate-200 dark:text-slate-700 mb-4 select-none"
            aria-hidden="true"
          >
            {statusCode}
          </p>
        )}

        {/* Heading */}
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100 mb-2">
          {heading}
        </h1>

        {/* Message */}
        <p className="text-base text-slate-500 dark:text-slate-400 mb-8 leading-relaxed">
          {message}
        </p>

        {/* Actions */}
        <div className="flex flex-col sm:flex-row items-center gap-3 [&>*]:min-h-[44px] [&>*]:min-w-[44px] [&>*]:focus-visible:outline [&>*]:focus-visible:outline-2 [&>*]:focus-visible:outline-offset-2 [&>*]:focus-visible:outline-sky-500">
          {children}
        </div>
      </div>
    </div>
  );
}
