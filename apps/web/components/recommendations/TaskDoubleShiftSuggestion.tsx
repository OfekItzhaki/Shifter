"use client";

import { useState } from "react";
import { useRecommendationForTask, useAcceptRecommendation, useDismissRecommendation } from "@/lib/query/hooks/useRecommendations";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import Modal from "@/components/Modal";
import SuccessToast from "./SuccessToast";

interface Props {
  spaceId: string;
  taskId: string;
}

/**
 * Inline chip/badge displayed next to the AllowsDoubleShift toggle in group task settings.
 * Shows a recommendation to enable double shifts when the engine detects it would cover additional slots.
 * Validates: Requirements 3.3, 4.1, 4.2, 4.3, 4.5
 */
export default function TaskDoubleShiftSuggestion({ spaceId, taskId }: Props) {
  const { data: recommendation } = useRecommendationForTask(spaceId, taskId);
  const acceptMutation = useAcceptRecommendation(spaceId);
  const dismissMutation = useDismissRecommendation(spaceId);
  const { fRange } = useDateFormat();

  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [acceptResult, setAcceptResult] = useState<string | null>(null);
  const [successToast, setSuccessToast] = useState<string | null>(null);

  // Render nothing if no active recommendation exists
  if (!recommendation) return null;

  const dateRange = fRange(recommendation.affectedDateStart, recommendation.affectedDateEnd);

  function handleEnableClick() {
    setShowConfirmDialog(true);
  }

  async function handleAccept(triggerNewRun: boolean) {
    try {
      const result = await acceptMutation.mutateAsync({
        recommendationId: recommendation!.id,
        triggerNewRun,
      });
      if (result.outcome === "AlreadyEnabled") {
        setAcceptResult(result.message);
      } else {
        // Show success toast with contextual message
        const taskName = recommendation!.taskName;
        if (triggerNewRun) {
          setSuccessToast(`Double shift enabled on "${taskName}". A new solver run has been queued.`);
        } else {
          setSuccessToast(`Double shift enabled on "${taskName}".`);
        }
      }
      setShowConfirmDialog(false);
    } catch {
      setShowConfirmDialog(false);
    }
  }

  function handleDismiss() {
    dismissMutation.mutate(recommendation!.id);
  }

  const isLoading = acceptMutation.isPending || dismissMutation.isPending;

  return (
    <>
      {/* Inline suggestion chip */}
      <div className="inline-flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-1.5 text-xs">
        {/* Lightbulb icon */}
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#d97706" strokeWidth={2} className="flex-shrink-0">
          <path strokeLinecap="round" strokeLinejoin="round" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
        </svg>

        <span className="text-amber-800">
          Enabling could cover <strong>{recommendation.additionalSlotsCovered}</strong> additional slot{recommendation.additionalSlotsCovered !== 1 ? "s" : ""} ({dateRange})
        </span>

        {/* Enable button */}
        <button
          onClick={handleEnableClick}
          disabled={isLoading}
          className="text-xs font-medium text-green-700 bg-green-50 border border-green-200 rounded-md px-2 py-0.5 hover:bg-green-100 transition-colors disabled:opacity-50"
        >
          Enable
        </button>

        {/* Dismiss button */}
        <button
          onClick={handleDismiss}
          disabled={isLoading}
          className="text-xs font-medium text-slate-500 hover:text-slate-700 transition-colors disabled:opacity-50"
        >
          Dismiss
        </button>
      </div>

      {/* Informational message when task already has double shift enabled */}
      {acceptResult && (
        <div className="inline-flex items-center gap-2 bg-blue-50 border border-blue-200 rounded-lg px-3 py-1.5 text-xs text-blue-700 mt-1">
          <span>{acceptResult}</span>
          <button onClick={() => setAcceptResult(null)} className="text-blue-500 hover:text-blue-700">✕</button>
        </div>
      )}

      {/* Confirmation dialog: ask whether to trigger a new solver run */}
      <Modal
        open={showConfirmDialog}
        onClose={() => setShowConfirmDialog(false)}
        title="Enable Double Shift"
        maxWidth={400}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">
            Double shift will be enabled on <strong>{recommendation.taskName}</strong>.
            Would you like to trigger a new solver run to recalculate the schedule with this change?
          </p>

          <div className="flex gap-2">
            <button
              onClick={() => handleAccept(true)}
              disabled={acceptMutation.isPending}
              className="flex-1 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
            >
              {acceptMutation.isPending ? "Enabling..." : "Enable & Run Solver"}
            </button>
            <button
              onClick={() => handleAccept(false)}
              disabled={acceptMutation.isPending}
              className="flex-1 text-sm text-slate-600 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 disabled:opacity-50 transition-colors"
            >
              Enable Only
            </button>
          </div>

          <button
            onClick={() => setShowConfirmDialog(false)}
            className="w-full text-xs text-slate-400 hover:text-slate-600 transition-colors"
          >
            Cancel
          </button>
        </div>
      </Modal>

      {/* Success toast notification after accept */}
      <SuccessToast
        visible={!!successToast}
        message={successToast ?? ""}
        onDismiss={() => setSuccessToast(null)}
      />
    </>
  );
}
