"use client";

import { useEffect, useState } from "react";

interface SuccessToastProps {
  /** Whether the toast is visible. */
  visible: boolean;
  /** Message to display. */
  message: string;
  /** Duration in ms before auto-dismiss. Default 5000 (5 seconds). */
  durationMs?: number;
  /** Called when the toast is dismissed (auto or manual). */
  onDismiss: () => void;
}

/**
 * A floating success toast notification.
 * Displays at the bottom-right, auto-dismisses after the configured duration.
 * Used for confirmation feedback after recommendation accept/dismiss actions.
 * Validates: Requirements 4.1, 4.2
 */
export default function SuccessToast({
  visible,
  message,
  durationMs = 5000,
  onDismiss,
}: SuccessToastProps) {
  const [show, setShow] = useState(false);

  useEffect(() => {
    if (visible) {
      setShow(true);
      const timer = setTimeout(() => {
        setShow(false);
        onDismiss();
      }, durationMs);
      return () => clearTimeout(timer);
    } else {
      setShow(false);
    }
  }, [visible, durationMs, onDismiss]);

  if (!show) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className="fixed bottom-6 right-6 z-[100] animate-in slide-in-from-bottom-4 fade-in duration-300"
    >
      <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl border border-green-200 dark:border-green-700 px-5 py-4 flex items-center gap-3 max-w-sm">
        <div className="w-10 h-10 rounded-xl bg-green-50 dark:bg-green-900/30 flex items-center justify-center flex-shrink-0">
          <svg
            width="20"
            height="20"
            fill="none"
            viewBox="0 0 24 24"
            stroke="#22c55e"
            strokeWidth={2}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
        </div>
        <p className="text-sm font-medium text-slate-900 dark:text-white flex-1">
          {message}
        </p>
        <button
          onClick={() => {
            setShow(false);
            onDismiss();
          }}
          className="flex-shrink-0 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
          aria-label="Dismiss"
        >
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>
  );
}
