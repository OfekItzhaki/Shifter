"use client";

import { RefObject, useCallback, useEffect, useRef, useState } from "react";
import { useFeedbackSubmission } from "@/hooks/useFeedbackSubmission";

interface SubmissionModalProps {
  open: boolean;
  submissionType: "bug" | "feedback";
  onClose: () => void;
  triggerRef: RefObject<HTMLButtonElement | null>;
}

/**
 * SubmissionModal — Modal dialog for submitting bug reports or feedback.
 *
 * Features:
 * - Multi-line textarea with 5000 char limit and character counter
 * - Loading, success, error, and rate-limit states
 * - Focus trap (Tab/Shift+Tab cycle within modal)
 * - Escape key closes modal
 * - Focus restoration to trigger element on close
 * - Backdrop click to dismiss
 *
 * Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 4.5, 7.3, 8.2, 8.3, 8.4, 8.6, 8.7
 */
export default function SubmissionModal({
  open,
  submissionType,
  onClose,
  triggerRef,
}: SubmissionModalProps) {
  const [description, setDescription] = useState("");
  const { submit, status, errorMessage, retryAfterSeconds, reset } = useFeedbackSubmission();

  const dialogRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const autoCloseTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const title = submissionType === "bug" ? "Bug Report" : "Feedback";

  // ── Close handler: restore focus, reset state ────────────────────────────

  const handleClose = useCallback(() => {
    // Clear any pending auto-close timer
    if (autoCloseTimerRef.current) {
      clearTimeout(autoCloseTimerRef.current);
      autoCloseTimerRef.current = null;
    }

    // Reset form state
    setDescription("");
    reset();

    // Close the modal
    onClose();

    // Restore focus to trigger element
    setTimeout(() => {
      triggerRef.current?.focus();
    }, 0);
  }, [onClose, reset, triggerRef]);

  // ── Auto-close on success ────────────────────────────────────────────────

  useEffect(() => {
    if (status === "success") {
      autoCloseTimerRef.current = setTimeout(() => {
        handleClose();
      }, 2000);
    }

    return () => {
      if (autoCloseTimerRef.current) {
        clearTimeout(autoCloseTimerRef.current);
        autoCloseTimerRef.current = null;
      }
    };
  }, [status, handleClose]);

  // ── Focus textarea on open ───────────────────────────────────────────────

  useEffect(() => {
    if (open) {
      const timer = setTimeout(() => {
        textareaRef.current?.focus();
      }, 50);
      return () => clearTimeout(timer);
    }
  }, [open]);

  // ── Keyboard handling: Escape + Focus trap ───────────────────────────────

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (e.key === "Escape") {
        e.preventDefault();
        handleClose();
        return;
      }

      if (e.key === "Tab") {
        const dialog = dialogRef.current;
        if (!dialog) return;

        const focusableElements = dialog.querySelectorAll<HTMLElement>(
          'button:not([disabled]), textarea:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );
        if (focusableElements.length === 0) return;

        const first = focusableElements[0];
        const last = focusableElements[focusableElements.length - 1];

        if (e.shiftKey) {
          if (document.activeElement === first) {
            e.preventDefault();
            last.focus();
          }
        } else {
          if (document.activeElement === last) {
            e.preventDefault();
            first.focus();
          }
        }
      }
    },
    [handleClose]
  );

  // ── Submit handler ───────────────────────────────────────────────────────

  const handleSubmit = useCallback(async () => {
    if (description.trim().length === 0 || status === "loading") return;

    await submit({ type: submissionType, description });
  }, [description, status, submit, submissionType]);

  // ── Don't render if not open ─────────────────────────────────────────────

  if (!open) return null;

  const isSubmitDisabled = description.trim().length === 0 || status === "loading";

  return (
    <div
      className="fixed inset-0 z-[1100] flex items-center justify-center p-4"
      style={{ background: "rgba(0, 0, 0, 0.5)" }}
      onClick={(e) => {
        if (e.target === e.currentTarget) handleClose();
      }}
      role="presentation"
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="submission-modal-title"
        onKeyDown={handleKeyDown}
        className="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl w-full max-w-md relative overflow-hidden"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-700">
          <h2
            id="submission-modal-title"
            className="text-base font-semibold text-slate-900 dark:text-slate-100"
          >
            {title}
          </h2>
          <button
            type="button"
            onClick={handleClose}
            aria-label="Close"
            className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 p-1 rounded-lg transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500"
          >
            <svg
              width="18"
              height="18"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
              aria-hidden="true"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div className="px-6 py-4">
          {/* Success state */}
          {status === "success" && (
            <div
              role="alert"
              className="flex items-center gap-2 p-3 rounded-lg bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 text-green-700 dark:text-green-300 text-sm"
            >
              <svg
                width="18"
                height="18"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth={2}
                aria-hidden="true"
              >
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              Submitted successfully. This dialog will close shortly.
            </div>
          )}

          {/* Form (hidden during success) */}
          {status !== "success" && (
            <>
              {/* Error / rate limit message */}
              {status === "error" && errorMessage && (
                <div
                  role="alert"
                  aria-live="assertive"
                  className="mb-3 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-300 text-sm"
                >
                  {retryAfterSeconds != null
                    ? `${errorMessage} Try again in ${retryAfterSeconds} seconds.`
                    : errorMessage}
                </div>
              )}

              {/* Textarea */}
              <div className="mb-3">
                <label
                  htmlFor="submission-description"
                  className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5"
                >
                  Description
                </label>
                <textarea
                  ref={textareaRef}
                  id="submission-description"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  maxLength={5000}
                  disabled={status === "loading"}
                  rows={5}
                  placeholder={
                    submissionType === "bug"
                      ? "Describe the bug you encountered..."
                      : "Share your feedback..."
                  }
                  className="w-full px-3 py-2 border border-slate-300 dark:border-slate-600 rounded-lg text-sm text-slate-900 dark:text-slate-100 bg-white dark:bg-slate-700 placeholder-slate-400 dark:placeholder-slate-500 resize-y focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 focus-visible:border-sky-500 disabled:opacity-50 disabled:cursor-not-allowed"
                  aria-required="true"
                  aria-describedby="submission-char-count"
                />
                <div
                  id="submission-char-count"
                  className="mt-1 text-xs text-slate-500 dark:text-slate-400 text-right"
                  aria-live="polite"
                >
                  {description.length}/5000
                </div>
              </div>

              {/* Submit button */}
              <button
                type="button"
                onClick={handleSubmit}
                disabled={isSubmitDisabled}
                className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg text-sm font-semibold text-white bg-sky-600 hover:bg-sky-700 disabled:bg-slate-400 disabled:cursor-not-allowed transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 focus-visible:ring-offset-2"
              >
                {status === "loading" && (
                  <svg
                    className="animate-spin h-4 w-4"
                    viewBox="0 0 24 24"
                    fill="none"
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
                {status === "loading" ? "Submitting..." : "Submit"}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
