"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { getRunStatus } from "@/lib/api/schedule";

export interface RegenerationStatusIndicatorProps {
  /** The space ID for the current space */
  spaceId: string;
  /** The run ID to poll for status updates */
  runId: string;
  /** Called when the run completes successfully with the new draft version ID */
  onCompleted: (resultVersionId: string) => void;
  /** Called when the user dismisses the status indicator */
  onDismiss: () => void;
}

type RunPhase = "queued" | "running" | "completed" | "failed";

interface RunStatusResponse {
  status: string;
  resultVersionId?: string | null;
  errorSummary?: string | null;
}

/**
 * Polls the schedule run status endpoint and displays real-time progress
 * for a regeneration run.
 *
 * - Queued: spinner with "Queued..." text
 * - Running: spinner with "Generating schedule..." text
 * - Completed: success icon, "Schedule ready!" text, and a "Review Draft" button
 * - Failed: error icon with the localized error message from errorSummary
 *
 * Stops polling when status is "completed" or "failed".
 * Cleans up the polling interval on unmount.
 *
 * Validates: Requirements 8.1, 8.2, 8.3, 8.4, 8.5, 4.4
 */
export default function RegenerationStatusIndicator({
  spaceId,
  runId,
  onCompleted,
  onDismiss,
}: RegenerationStatusIndicatorProps) {
  const t = useTranslations("regeneration.status");
  const [phase, setPhase] = useState<RunPhase>("queued");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [resultVersionId, setResultVersionId] = useState<string | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const onCompletedRef = useRef(onCompleted);

  // Keep onCompleted ref up to date without re-triggering the effect
  useEffect(() => {
    onCompletedRef.current = onCompleted;
  }, [onCompleted]);

  const stopPolling = useCallback(() => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function poll() {
      try {
        const data = (await getRunStatus(spaceId, runId)) as RunStatusResponse;
        if (cancelled) return;

        const status = data.status?.toLowerCase();

        if (status === "completed") {
          setPhase("completed");
          stopPolling();
          if (data.resultVersionId) {
            setResultVersionId(data.resultVersionId);
            onCompletedRef.current(data.resultVersionId);
          }
        } else if (status === "failed" || status === "timedout") {
          setPhase("failed");
          setErrorMessage(data.errorSummary || t("unknownError"));
          stopPolling();
        } else if (status === "running") {
          setPhase("running");
        } else {
          // queued or any other intermediate state
          setPhase("queued");
        }
      } catch {
        if (cancelled) return;
        setPhase("failed");
        setErrorMessage(t("pollError"));
        stopPolling();
      }
    }

    // Initial poll immediately
    poll();

    // Then poll every 3 seconds
    intervalRef.current = setInterval(poll, 3000);

    return () => {
      cancelled = true;
      stopPolling();
    };
  }, [spaceId, runId, stopPolling, t]);

  const handleReviewDraft = useCallback(() => {
    if (resultVersionId) {
      onCompletedRef.current(resultVersionId);
    }
  }, [resultVersionId]);

  return (
    <div
      className="flex items-center gap-3 rounded-xl px-4 py-3 border transition-colors duration-200"
      role="status"
      aria-live="polite"
      aria-label={t("ariaLabel")}
      data-phase={phase}
      style={phaseStyles[phase]}
    >
      {/* Icon */}
      <div className="flex-shrink-0">
        {phase === "queued" && <SpinnerIcon />}
        {phase === "running" && <SpinnerIcon />}
        {phase === "completed" && <CheckIcon />}
        {phase === "failed" && <ErrorIcon />}
      </div>

      {/* Text */}
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium">
          {phase === "queued" && t("queued")}
          {phase === "running" && t("running")}
          {phase === "completed" && t("completed")}
          {phase === "failed" && t("failed")}
        </p>
        {phase === "failed" && errorMessage && (
          <p className="text-xs mt-0.5 opacity-80">{errorMessage}</p>
        )}
      </div>

      {/* Actions */}
      <div className="flex-shrink-0 flex items-center gap-2">
        {phase === "completed" && resultVersionId && (
          <button
            onClick={handleReviewDraft}
            className="text-xs font-semibold px-3 py-1.5 rounded-lg transition-colors"
            style={{
              backgroundColor: "rgb(4 120 87)",
              color: "white",
            }}
          >
            {t("reviewDraft")}
          </button>
        )}

        {(phase === "completed" || phase === "failed") && (
          <button
            onClick={onDismiss}
            aria-label={t("dismiss")}
            className="p-1 rounded-md opacity-60 hover:opacity-100 transition-opacity"
          >
            <svg
              className="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        )}
      </div>
    </div>
  );
}

// ── Inline styles per phase (avoids dynamic class string issues) ──────────────

const phaseStyles: Record<RunPhase, React.CSSProperties> = {
  queued: {
    backgroundColor: "rgb(239 246 255)", // blue-50
    borderColor: "rgb(191 219 254)", // blue-200
    color: "rgb(29 78 216)", // blue-700
  },
  running: {
    backgroundColor: "rgb(239 246 255)", // blue-50
    borderColor: "rgb(191 219 254)", // blue-200
    color: "rgb(29 78 216)", // blue-700
  },
  completed: {
    backgroundColor: "rgb(236 253 245)", // emerald-50
    borderColor: "rgb(167 243 208)", // emerald-200
    color: "rgb(4 120 87)", // emerald-700
  },
  failed: {
    backgroundColor: "rgb(254 242 242)", // red-50
    borderColor: "rgb(254 202 202)", // red-200
    color: "rgb(185 28 28)", // red-700
  },
};

// ── Icons ─────────────────────────────────────────────────────────────────────

function SpinnerIcon() {
  return (
    <svg
      className="animate-spin h-5 w-5"
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
  );
}

function CheckIcon() {
  return (
    <svg
      className="h-5 w-5"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth={2.5}
      aria-hidden="true"
    >
      <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
    </svg>
  );
}

function ErrorIcon() {
  return (
    <svg
      className="h-5 w-5"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth={2}
      aria-hidden="true"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z"
      />
    </svg>
  );
}
