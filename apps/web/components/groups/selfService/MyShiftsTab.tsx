"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  getMyShiftRequests,
  cancelShiftRequest,
  ShiftRequestDto,
  MyShiftsResponse,
} from "@/lib/api/selfService";
import { formatSlotDate, formatTime24h, HEBREW_DAY_NAMES } from "@/lib/utils/selfServiceFormat";
import { validateCancellationReason } from "@/lib/utils/selfServiceValidation";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import Modal from "@/components/Modal";
import LoadingCard from "./LoadingCard";
import ErrorRetry from "./ErrorRetry";
import MutationButton from "./MutationButton";

interface MyShiftsTabProps {
  spaceId: string;
  groupId: string;
}

/** Status badge color configuration */
const STATUS_BADGE_STYLES: Record<ShiftRequestDto["status"], { dot: string; badge: string }> = {
  Approved: { dot: "bg-emerald-500", badge: "bg-emerald-50 text-emerald-700 border-emerald-200" },
  Pending: { dot: "bg-amber-400", badge: "bg-amber-50 text-amber-700 border-amber-200" },
  Rejected: { dot: "bg-red-500", badge: "bg-red-50 text-red-700 border-red-200" },
  Cancelled: { dot: "bg-slate-400", badge: "bg-slate-50 text-slate-600 border-slate-200" },
};

/** Status sort order: approved first, then pending, then cancelled/rejected */
const STATUS_ORDER: Record<ShiftRequestDto["status"], number> = {
  Approved: 0,
  Pending: 1,
  Cancelled: 2,
  Rejected: 3,
};

function groupByStatus(requests: ShiftRequestDto[]): {
  approved: ShiftRequestDto[];
  pending: ShiftRequestDto[];
  cancelled: ShiftRequestDto[];
} {
  const approved: ShiftRequestDto[] = [];
  const pending: ShiftRequestDto[] = [];
  const cancelled: ShiftRequestDto[] = [];

  for (const req of requests) {
    switch (req.status) {
      case "Approved":
        approved.push(req);
        break;
      case "Pending":
        pending.push(req);
        break;
      case "Cancelled":
      case "Rejected":
        cancelled.push(req);
        break;
    }
  }

  return { approved, pending, cancelled };
}

/**
 * Determines if a shift can be cancelled based on the cancellation cutoff.
 * A shift can be cancelled if the shift start time is more than `cutoffHours` in the future.
 */
function canCancelShift(request: ShiftRequestDto, cancellationCutoffHours: number): boolean {
  if (request.status !== "Approved") return false;

  try {
    // Build the shift start datetime from slotDate + slotStartTime
    const shiftStart = new Date(`${request.slotDate}T${request.slotStartTime}`);
    if (isNaN(shiftStart.getTime())) return false;

    const now = new Date();
    const cutoffMs = cancellationCutoffHours * 60 * 60 * 1000;
    return shiftStart.getTime() - now.getTime() > cutoffMs;
  } catch {
    return false;
  }
}

export default function MyShiftsTab({ spaceId, groupId }: MyShiftsTabProps) {
  const t = useTranslations("selfService.myShifts");

  const [data, setData] = useState<MyShiftsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Cancel dialog state
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [cancelTarget, setCancelTarget] = useState<ShiftRequestDto | null>(null);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelReasonError, setCancelReasonError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await getMyShiftRequests(spaceId, groupId);
      setData(response);
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // ── Cancel handlers ──────────────────────────────────────────────────────

  function openCancelDialog(request: ShiftRequestDto) {
    setCancelTarget(request);
    setCancelReason("");
    setCancelReasonError(null);
    setCancelError(null);
    setCancelDialogOpen(true);
  }

  function closeCancelDialog() {
    setCancelDialogOpen(false);
    setCancelTarget(null);
    setCancelReason("");
    setCancelReasonError(null);
    setCancelError(null);
  }

  async function handleCancelConfirm() {
    if (!cancelTarget) return;

    const validation = validateCancellationReason(cancelReason);
    if (!validation.valid) {
      setCancelReasonError(validation.errorKey ?? null);
      return;
    }

    setCancelling(true);
    setCancelError(null);

    try {
      await cancelShiftRequest(spaceId, groupId, cancelTarget.id, cancelReason.trim());
      closeCancelDialog();
      // Refetch to reflect the cancellation
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setCancelError(message);
    } finally {
      setCancelling(false);
    }
  }

  // ── Loading state ────────────────────────────────────────────────────────

  if (loading) {
    return <LoadingCard rows={4} variant="list" />;
  }

  // ── Error state ──────────────────────────────────────────────────────────

  if (error) {
    return <ErrorRetry message={error} onRetry={fetchData} />;
  }

  if (!data) return null;

  const { approved, pending, cancelled } = groupByStatus(data.requests);
  const { currentShiftCount, minShiftsPerCycle, maxShiftsPerCycle, cancellationCutoffHours } = data;
  const isUnderScheduled = currentShiftCount < minShiftsPerCycle;

  // ── Render ───────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      {/* Shift count indicator */}
      <div className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
        <span className="text-sm font-medium text-slate-700">
          {t("shiftCount", { current: currentShiftCount, max: maxShiftsPerCycle })}
        </span>
      </div>

      {/* Under-scheduled warning */}
      {isUnderScheduled && (
        <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">
          <p className="text-sm text-amber-700 font-medium">
            {t("underScheduled", { min: minShiftsPerCycle })}
          </p>
        </div>
      )}

      {/* Empty state */}
      {data.requests.length === 0 && (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">{t("noShifts")}</p>
        </div>
      )}

      {/* Approved shifts */}
      {approved.length > 0 && (
        <ShiftSection
          title={t("approved")}
          requests={approved}
          statusKey="Approved"
          cancellationCutoffHours={cancellationCutoffHours}
          onCancel={openCancelDialog}
          t={t}
        />
      )}

      {/* Pending shifts */}
      {pending.length > 0 && (
        <ShiftSection
          title={t("pending")}
          requests={pending}
          statusKey="Pending"
          cancellationCutoffHours={cancellationCutoffHours}
          onCancel={openCancelDialog}
          t={t}
        />
      )}

      {/* Cancelled shifts */}
      {cancelled.length > 0 && (
        <ShiftSection
          title={t("cancelled")}
          requests={cancelled}
          statusKey="Cancelled"
          cancellationCutoffHours={cancellationCutoffHours}
          onCancel={openCancelDialog}
          t={t}
        />
      )}

      {/* Cancel dialog */}
      <Modal
        open={cancelDialogOpen}
        onClose={closeCancelDialog}
        title={t("cancelDialogTitle")}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">{t("cancelDialogMessage")}</p>

          <textarea
            value={cancelReason}
            onChange={(e) => {
              setCancelReason(e.target.value);
              setCancelReasonError(null);
            }}
            placeholder={t("cancelReasonPlaceholder")}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
            rows={3}
            maxLength={500}
            dir="rtl"
          />

          {/* Character count */}
          <div className="flex justify-between items-center">
            <span className="text-xs text-slate-400">
              {cancelReason.trim().length} / 500
            </span>
          </div>

          {/* Validation error */}
          {cancelReasonError && (
            <p className="text-xs text-red-600">{cancelReasonError}</p>
          )}

          {/* API error */}
          {cancelError && (
            <p className="text-xs text-red-600">{cancelError}</p>
          )}

          {/* Actions */}
          <div className="flex gap-3 justify-end">
            <button
              onClick={closeCancelDialog}
              className="px-4 py-2 text-sm text-slate-600 hover:text-slate-800 border border-slate-200 rounded-lg transition-colors"
              disabled={cancelling}
            >
              {t("cancelDismiss")}
            </button>
            <MutationButton
              onClick={handleCancelConfirm}
              loading={cancelling}
              disabled={cancelReason.trim().length === 0}
              label={t("cancelConfirm")}
              loadingLabel={t("cancelling")}
              variant="danger"
            />
          </div>
        </div>
      </Modal>
    </div>
  );
}

// ── ShiftSection sub-component ─────────────────────────────────────────────

interface ShiftSectionProps {
  title: string;
  requests: ShiftRequestDto[];
  statusKey: ShiftRequestDto["status"];
  cancellationCutoffHours: number;
  onCancel: (request: ShiftRequestDto) => void;
  t: (key: string, values?: Record<string, unknown>) => string;
}

function ShiftSection({ title, requests, statusKey, cancellationCutoffHours, onCancel, t }: ShiftSectionProps) {
  return (
    <div>
      <div className="flex items-center gap-2 mb-2">
        <span className={`w-2 h-2 rounded-full ${STATUS_BADGE_STYLES[statusKey].dot}`} />
        <span className="text-xs font-semibold text-slate-500 uppercase tracking-wide">
          {title} ({requests.length})
        </span>
      </div>
      <div className="space-y-2">
        {requests.map((request) => (
          <ShiftCard
            key={request.id}
            request={request}
            cancellationCutoffHours={cancellationCutoffHours}
            onCancel={onCancel}
            t={t}
          />
        ))}
      </div>
    </div>
  );
}

// ── ShiftCard sub-component ────────────────────────────────────────────────

interface ShiftCardProps {
  request: ShiftRequestDto;
  cancellationCutoffHours: number;
  onCancel: (request: ShiftRequestDto) => void;
  t: (key: string, values?: Record<string, unknown>) => string;
}

function ShiftCard({ request, cancellationCutoffHours, onCancel, t }: ShiftCardProps) {
  const style = STATUS_BADGE_STYLES[request.status];
  const showCancelButton = canCancelShift(request, cancellationCutoffHours);

  // Get Hebrew day name from the date
  const dayName = (() => {
    try {
      const d = new Date(request.slotDate);
      if (isNaN(d.getTime())) return "";
      return HEBREW_DAY_NAMES[d.getDay()];
    } catch {
      return "";
    }
  })();

  const statusLabel = (() => {
    switch (request.status) {
      case "Approved": return t("approved");
      case "Pending": return t("pending");
      case "Cancelled": return t("cancelled");
      case "Rejected": return t("rejected");
    }
  })();

  return (
    <div className="flex items-center justify-between gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <p className="text-sm font-medium text-slate-900 truncate">
            {formatSlotDate(request.slotDate)}
          </p>
          {request.isAdminOverride && (
            <span className="text-xs text-purple-600 bg-purple-50 border border-purple-200 px-1.5 py-0.5 rounded">
              {t("adminOverride")}
            </span>
          )}
        </div>
        <p className="text-xs text-slate-500 mt-0.5">
          {formatTime24h(request.slotStartTime)} – {formatTime24h(request.slotEndTime)} · {request.taskName}
        </p>
      </div>

      <div className="flex items-center gap-2 flex-shrink-0">
        {/* Status badge */}
        <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium border ${style.badge}`}>
          <span className={`w-1.5 h-1.5 rounded-full ${style.dot}`} />
          {statusLabel}
        </span>

        {/* Cancel button */}
        {showCancelButton && (
          <button
            onClick={() => onCancel(request)}
            className="text-xs text-red-600 hover:text-red-700 border border-red-200 bg-red-50 hover:bg-red-100 px-2.5 py-1 rounded-lg transition-colors"
          >
            {t("cancelButton")}
          </button>
        )}
      </div>
    </div>
  );
}
