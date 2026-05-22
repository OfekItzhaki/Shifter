"use client";

import { Component, ErrorInfo, ReactNode } from "react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import ErrorPageLayout from "@/components/errors/ErrorPageLayout";

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

/**
 * Functional component that renders the error fallback UI.
 * Extracted from the class component so it can use the `useTranslations` hook.
 */
function ErrorFallback({ error }: { error: Error | null }) {
  const t = useTranslations("errorPages");
  const isDev = process.env.NODE_ENV === "development";

  return (
    <ErrorPageLayout
      heading={t("clientError.heading")}
      message={t("clientError.message")}
    >
      {isDev && error?.message && (
        <p className="text-xs text-gray-500 dark:text-gray-400 bg-gray-100 dark:bg-slate-800 rounded px-3 py-2 font-mono max-w-full break-words">
          {error.message}
        </p>
      )}
      <button
        onClick={() => window.location.reload()}
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg bg-sky-500 text-white font-medium text-sm hover:bg-sky-600 transition-colors"
      >
        {t("clientError.reload")}
      </button>
      <Link
        href="/"
        className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 font-medium text-sm hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
      >
        {t("clientError.goHome")}
      </Link>
    </ErrorPageLayout>
  );
}

/**
 * Class-based error boundary that catches unhandled JavaScript exceptions
 * in its child component tree and renders a branded fallback UI.
 * Logs error details to the console and differentiates between dev/prod modes.
 */
export default class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error(error);
    console.error(errorInfo.componentStack);
  }

  render() {
    if (this.state.hasError) {
      return <ErrorFallback error={this.state.error} />;
    }
    return this.props.children;
  }
}
