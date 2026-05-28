"use client";

import { ButtonHTMLAttributes } from "react";

export interface MutationButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  /** Whether the mutation is currently in progress */
  loading?: boolean;
  /** Button label when idle */
  label: string;
  /** Button label when loading (optional, defaults to label) */
  loadingLabel?: string;
  /** Visual style variant */
  variant?: "primary" | "danger" | "secondary";
}

/**
 * Shared mutation button component for self-service tabs.
 * Shows a spinner and disables during in-flight requests to prevent double-submission.
 */
export default function MutationButton({
  loading = false,
  label,
  loadingLabel,
  variant = "primary",
  disabled,
  className,
  ...props
}: MutationButtonProps) {
  const variantStyles: Record<string, string> = {
    primary:
      "text-white bg-sky-600 hover:bg-sky-700 disabled:opacity-50 disabled:cursor-not-allowed",
    danger:
      "text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed",
    secondary:
      "text-slate-600 hover:text-slate-800 border border-slate-200 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed",
  };

  return (
    <button
      disabled={disabled || loading}
      className={`inline-flex items-center justify-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors ${variantStyles[variant]} ${className ?? ""}`}
      {...props}
    >
      {loading && (
        <svg
          className="animate-spin h-4 w-4"
          fill="none"
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          />
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
          />
        </svg>
      )}
      {loading ? (loadingLabel ?? label) : label}
    </button>
  );
}
