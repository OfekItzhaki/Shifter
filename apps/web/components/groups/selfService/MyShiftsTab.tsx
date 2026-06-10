"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  AvailableSlotDto,
  ShiftChangeRequestDto,
  cancelShiftChangeRequest,
  getAvailableSlots,
  getMyShiftChangeRequests,
  getMyShiftRequests,
  cancelShiftRequest,
  reportCannotAttend,
  submitShiftChangeRequest,
  ShiftRequestDto,
  MyShiftsResponse,
} from "@/lib/api/selfService";
import {
  cancelSpecialLeaveRequest,
  getMySpecialLeaveRequests,
  submitSpecialLeaveRequest,
  SpecialLeaveRequestDto,
} from "@/lib/api/specialLeave";
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

function isFutureApprovedShift(request: ShiftRequestDto): boolean {
  if (request.status !== "Approved") return false;

  const shiftStart = new Date(`${request.slotDate}T${request.slotStartTime}`);
  return !isNaN(shiftStart.getTime()) && shiftStart.getTime() > Date.now();
}

export default function MyShiftsTab({ spaceId, groupId }: MyShiftsTabProps) {
  const t = useTranslations("selfService.myShifts");

  const [data, setData] = useState<MyShiftsResponse | null>(null);
  const [specialLeaveRequests, setSpecialLeaveRequests] = useState<SpecialLeaveRequestDto[]>([]);
  const [changeRequests, setChangeRequests] = useState<ShiftChangeRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [leaveStart, setLeaveStart] = useState("");
  const [leaveEnd, setLeaveEnd] = useState("");
  const [leaveReason, setLeaveReason] = useState("");
  const [leaveSaving, setLeaveSaving] = useState(false);
  const [leaveError, setLeaveError] = useState<string | null>(null);
  const [leaveSuccess, setLeaveSuccess] = useState<string | null>(null);

  // Cancel dialog state
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [cancelTarget, setCancelTarget] = useState<ShiftRequestDto | null>(null);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelReasonError, setCancelReasonError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);

  const [cannotAttendDialogOpen, setCannotAttendDialogOpen] = useState(false);
  const [cannotAttendTarget, setCannotAttendTarget] = useState<ShiftRequestDto | null>(null);
  const [cannotAttendReason, setCannotAttendReason] = useState("");
  const [cannotAttendReasonError, setCannotAttendReasonError] = useState<string | null>(null);
  const [reportingCannotAttend, setReportingCannotAttend] = useState(false);
  const [cannotAttendError, setCannotAttendError] = useState<string | null>(null);
  const [cannotAttendSuccess, setCannotAttendSuccess] = useState<string | null>(null);

  const [changeDialogOpen, setChangeDialogOpen] = useState(false);
  const [changeTarget, setChangeTarget] = useState<ShiftRequestDto | null>(null);
  const [changeReason, setChangeReason] = useState("");
  const [changeReasonError, setChangeReasonError] = useState<string | null>(null);
  const [changeRequestedSlotId, setChangeRequestedSlotId] = useState("");
  const [changeSlotOptions, setChangeSlotOptions] = useState<AvailableSlotDto[]>([]);
  const [changeSlotsLoading, setChangeSlotsLoading] = useState(false);
  const [submittingChange, setSubmittingChange] = useState(false);
  const [changeError, setChangeError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [response, leaveRequests, shiftChanges] = await Promise.all([
        getMyShiftRequests(spaceId, groupId),
        getMySpecialLeaveRequests(spaceId),
        getMyShiftChangeRequests(spaceId, groupId),
      ]);
      setData(response);
      setSpecialLeaveRequests(leaveRequests);
      setChangeRequests(shiftChanges);
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

  function openCannotAttendDialog(request: ShiftRequestDto) {
    setCannotAttendTarget(request);
    setCannotAttendReason("");
    setCannotAttendReasonError(null);
    setCannotAttendError(null);
    setCannotAttendSuccess(null);
    setCannotAttendDialogOpen(true);
  }

  function closeCannotAttendDialog() {
    setCannotAttendDialogOpen(false);
    setCannotAttendTarget(null);
    setCannotAttendReason("");
    setCannotAttendReasonError(null);
    setCannotAttendError(null);
  }

  async function handleCannotAttendConfirm() {
    if (!cannotAttendTarget) return;

    const validation = validateCancellationReason(cannotAttendReason);
    if (!validation.valid) {
      setCannotAttendReasonError(validation.errorKey ?? null);
      return;
    }

    setReportingCannotAttend(true);
    setCannotAttendError(null);

    try {
      const result = await reportCannotAttend(spaceId, groupId, cannotAttendTarget.id, cannotAttendReason.trim());
      setCannotAttendSuccess(
        result.wasLate
          ? t("cannotAttendLateSubmitted", {
              used: result.lateReportsUsed,
              max: result.maxLateReports,
            })
          : t("cannotAttendSubmitted")
      );
      closeCannotAttendDialog();
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setCannotAttendError(message);
    } finally {
      setReportingCannotAttend(false);
    }
  }

  async function openChangeDialog(request: ShiftRequestDto) {
    setChangeTarget(request);
    setChangeReason("");
    setChangeReasonError(null);
    setChangeRequestedSlotId("");
    setChangeSlotOptions([]);
    setChangeError(null);
    setChangeDialogOpen(true);
    setChangeSlotsLoading(true);

    try {
      const slots = await getAvailableSlots(spaceId, groupId, "current");
      setChangeSlotOptions(
        slots.slots.filter(
          (slot) => slot.id !== request.shiftSlotId && slot.currentFillCount < slot.capacity
        )
      );
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setChangeError(message);
    } finally {
      setChangeSlotsLoading(false);
    }
  }

  function closeChangeDialog() {
    setChangeDialogOpen(false);
    setChangeTarget(null);
    setChangeReason("");
    setChangeReasonError(null);
    setChangeRequestedSlotId("");
    setChangeSlotOptions([]);
    setChangeError(null);
  }

  async function handleChangeConfirm() {
    if (!changeTarget) return;

    const validation = validateCancellationReason(changeReason);
    if (!validation.valid) {
      setChangeReasonError(validation.errorKey ?? null);
      return;
    }

    setSubmittingChange(true);
    setChangeError(null);

    try {
      await submitShiftChangeRequest(
        spaceId,
        groupId,
        changeTarget.id,
        changeReason.trim(),
        changeRequestedSlotId || null
      );
      closeChangeDialog();
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setChangeError(message);
    } finally {
      setSubmittingChange(false);
    }
  }

  async function handleSubmitSpecialLeave() {
    setLeaveError(null);
    setLeaveSuccess(null);

    if (!leaveStart || !leaveEnd) {
      setLeaveError(t("specialLeaveDateRequired"));
      return;
    }

    if (new Date(leaveStart).getTime() >= new Date(leaveEnd).getTime()) {
      setLeaveError(t("specialLeaveInvalidRange"));
      return;
    }

    if (leaveReason.trim().length === 0) {
      setLeaveError(t("specialLeaveReasonRequired"));
      return;
    }

    if (leaveReason.trim().length > 500) {
      setLeaveError(t("specialLeaveReasonTooLong"));
      return;
    }

    setLeaveSaving(true);

    try {
      await submitSpecialLeaveRequest(spaceId, {
        startsAt: new Date(leaveStart).toISOString(),
        endsAt: new Date(leaveEnd).toISOString(),
        reason: leaveReason.trim(),
      });
      setLeaveStart("");
      setLeaveEnd("");
      setLeaveReason("");
      setLeaveSuccess(t("specialLeaveSubmitted"));
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setLeaveError(message);
    } finally {
      setLeaveSaving(false);
    }
  }

  async function handleCancelSpecialLeave(requestId: string) {
    setLeaveError(null);
    setLeaveSuccess(null);

    try {
      await cancelSpecialLeaveRequest(spaceId, requestId);
      setLeaveSuccess(t("specialLeaveCancelled"));
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setLeaveError(message);
    }
  }

  async function handleCancelShiftChange(requestId: string) {
    setLeaveError(null);
    setLeaveSuccess(null);

    try {
      await cancelShiftChangeRequest(spaceId, groupId, requestId);
      setLeaveSuccess(t("changeRequestCancelled"));
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setLeaveError(message);
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

      {cannotAttendSuccess && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
          {cannotAttendSuccess}
        </div>
      )}

      <div className="bg-white border border-slate-200 rounded-xl px-4 py-4">
        <div className="flex flex-col gap-1 mb-4">
          <h3 className="text-sm font-semibold text-slate-900">{t("specialLeaveTitle")}</h3>
          <p className="text-xs text-slate-500">{t("specialLeaveDescription")}</p>
        </div>

        <div className="grid gap-3 md:grid-cols-[1fr_1fr_2fr_auto] md:items-end">
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">{t("specialLeaveStart")}</span>
            <input
              type="datetime-local"
              value={leaveStart}
              onChange={(e) => setLeaveStart(e.target.value)}
              className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400"
            />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">{t("specialLeaveEnd")}</span>
            <input
              type="datetime-local"
              value={leaveEnd}
              onChange={(e) => setLeaveEnd(e.target.value)}
              className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400"
            />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">{t("specialLeaveReason")}</span>
            <input
              value={leaveReason}
              onChange={(e) => setLeaveReason(e.target.value)}
              maxLength={500}
              placeholder={t("specialLeaveReasonPlaceholder")}
              className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400"
            />
          </label>
          <MutationButton
            onClick={handleSubmitSpecialLeave}
            loading={leaveSaving}
            disabled={leaveSaving}
            label={t("specialLeaveSubmit")}
            loadingLabel={t("specialLeaveSubmitting")}
          />
        </div>

        {leaveError && <p className="text-xs text-red-600 mt-3">{leaveError}</p>}
        {leaveSuccess && <p className="text-xs text-emerald-600 mt-3">{leaveSuccess}</p>}

        {specialLeaveRequests.length > 0 && (
          <div className="mt-4 space-y-2">
            {specialLeaveRequests.map((request) => (
              <SpecialLeaveCard
                key={request.id}
                request={request}
                onCancel={handleCancelSpecialLeave}
              />
            ))}
          </div>
        )}
      </div>

      <div className="bg-white border border-slate-200 rounded-xl px-4 py-4">
        <div className="flex flex-col gap-1 mb-4">
          <h3 className="text-sm font-semibold text-slate-900">{t("changeRequestsTitle")}</h3>
          <p className="text-xs text-slate-500">{t("changeRequestsDescription")}</p>
        </div>

        {changeRequests.length === 0 ? (
          <p className="text-xs text-slate-400">{t("changeRequestsEmpty")}</p>
        ) : (
          <div className="space-y-2">
            {changeRequests.map((request) => (
              <ShiftChangeCard
                key={request.id}
                request={request}
                onCancel={handleCancelShiftChange}
              />
            ))}
          </div>
        )}
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
          onCannotAttend={openCannotAttendDialog}
          onChange={openChangeDialog}
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
          onCannotAttend={openCannotAttendDialog}
          onChange={openChangeDialog}
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
          onCannotAttend={openCannotAttendDialog}
          onChange={openChangeDialog}
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

      <Modal
        open={cannotAttendDialogOpen}
        onClose={closeCannotAttendDialog}
        title={t("cannotAttendDialogTitle")}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">{t("cannotAttendDialogMessage")}</p>

          <textarea
            value={cannotAttendReason}
            onChange={(e) => {
              setCannotAttendReason(e.target.value);
              setCannotAttendReasonError(null);
            }}
            placeholder={t("cannotAttendReasonPlaceholder")}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
            rows={3}
            maxLength={500}
            dir="rtl"
          />

          <div className="flex justify-between items-center">
            <span className="text-xs text-slate-400">
              {cannotAttendReason.trim().length} / 500
            </span>
          </div>

          {cannotAttendReasonError && (
            <p className="text-xs text-red-600">{cannotAttendReasonError}</p>
          )}
          {cannotAttendError && (
            <p className="text-xs text-red-600">{cannotAttendError}</p>
          )}

          <div className="flex gap-3 justify-end">
            <button
              onClick={closeCannotAttendDialog}
              className="px-4 py-2 text-sm text-slate-600 hover:text-slate-800 border border-slate-200 rounded-lg transition-colors"
              disabled={reportingCannotAttend}
            >
              {t("cancelDismiss")}
            </button>
            <MutationButton
              onClick={handleCannotAttendConfirm}
              loading={reportingCannotAttend}
              disabled={cannotAttendReason.trim().length === 0}
              label={t("cannotAttendConfirm")}
              loadingLabel={t("cannotAttendSubmitting")}
              variant="danger"
            />
          </div>
        </div>
      </Modal>

      <Modal
        open={changeDialogOpen}
        onClose={closeChangeDialog}
        title={t("changeDialogTitle")}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">{t("changeDialogMessage")}</p>

          <label className="block space-y-1">
            <span className="text-xs font-medium text-slate-500">{t("changePreferredShift")}</span>
            <select
              value={changeRequestedSlotId}
              onChange={(e) => setChangeRequestedSlotId(e.target.value)}
              className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
              disabled={changeSlotsLoading}
            >
              <option value="">
                {changeSlotsLoading ? t("changeSlotsLoading") : t("changeNoPreferredShift")}
              </option>
              {changeSlotOptions.map((slot) => (
                <option key={slot.id} value={slot.id}>
                  {formatSlotDate(slot.date)} {formatTime24h(slot.startTime)}-{formatTime24h(slot.endTime)} {slot.taskName}
                </option>
              ))}
            </select>
          </label>

          <textarea
            value={changeReason}
            onChange={(e) => {
              setChangeReason(e.target.value);
              setChangeReasonError(null);
            }}
            placeholder={t("changeReasonPlaceholder")}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
            rows={3}
            maxLength={500}
            dir="rtl"
          />

          <div className="flex justify-between items-center">
            <span className="text-xs text-slate-400">
              {changeReason.trim().length} / 500
            </span>
          </div>

          {changeReasonError && (
            <p className="text-xs text-red-600">{changeReasonError}</p>
          )}
          {changeError && (
            <p className="text-xs text-red-600">{changeError}</p>
          )}

          <div className="flex gap-3 justify-end">
            <button
              onClick={closeChangeDialog}
              className="px-4 py-2 text-sm text-slate-600 hover:text-slate-800 border border-slate-200 rounded-lg transition-colors"
              disabled={submittingChange}
            >
              {t("cancelDismiss")}
            </button>
            <MutationButton
              onClick={handleChangeConfirm}
              loading={submittingChange}
              disabled={changeReason.trim().length === 0}
              label={t("changeConfirm")}
              loadingLabel={t("changeSubmitting")}
              variant="primary"
            />
          </div>
        </div>
      </Modal>
    </div>
  );
}

function formatSpecialLeaveDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function SpecialLeaveCard({
  request,
  onCancel,
}: {
  request: SpecialLeaveRequestDto;
  onCancel: (requestId: string) => void;
}) {
  const t = useTranslations("selfService.myShifts");
  const style = STATUS_BADGE_STYLES[request.status];
  const statusLabel = (() => {
    switch (request.status) {
      case "Approved": return t("approved");
      case "Pending": return t("pending");
      case "Cancelled": return t("cancelled");
      case "Rejected": return t("rejected");
    }
  })();

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <p className="text-xs font-medium text-slate-800">
          {formatSpecialLeaveDate(request.startsAt)} - {formatSpecialLeaveDate(request.endsAt)}
        </p>
        <p className="mt-0.5 truncate text-xs text-slate-500">{request.reason}</p>
      </div>
      <div className="flex items-center gap-2">
        <span className={`inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium ${style.badge}`}>
          <span className={`h-1.5 w-1.5 rounded-full ${style.dot}`} />
          {statusLabel}
        </span>
        {request.status === "Pending" && (
          <button
            type="button"
            onClick={() => onCancel(request.id)}
            className="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-600 transition-colors hover:bg-slate-100"
          >
            {t("specialLeaveCancel")}
          </button>
        )}
      </div>
    </div>
  );
}

function ShiftChangeCard({
  request,
  onCancel,
}: {
  request: ShiftChangeRequestDto;
  onCancel: (requestId: string) => void;
}) {
  const t = useTranslations("selfService.myShifts");
  const style = STATUS_BADGE_STYLES[request.status];
  const requestedLabel = request.requestedShiftSlotId && request.requestedSlotDate
    ? `${formatSlotDate(request.requestedSlotDate)} ${formatTime24h(request.requestedSlotStartTime ?? "")}-${formatTime24h(request.requestedSlotEndTime ?? "")} ${request.requestedTaskName ?? ""}`
    : t("changeFlexibleTarget");

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <p className="text-xs font-medium text-slate-800">
          {formatSlotDate(request.originalSlotDate)} {formatTime24h(request.originalSlotStartTime)}-{formatTime24h(request.originalSlotEndTime)} - {request.originalTaskName}
        </p>
        <p className="mt-0.5 text-xs text-slate-500">{t("changeRequestedTo")}: {requestedLabel}</p>
        <p className="mt-0.5 truncate text-xs text-slate-500">{request.reason}</p>
      </div>
      <div className="flex items-center gap-2">
        <span className={`inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium ${style.badge}`}>
          <span className={`h-1.5 w-1.5 rounded-full ${style.dot}`} />
          {t(request.status === "Approved" ? "approved" : request.status === "Pending" ? "pending" : request.status === "Rejected" ? "rejected" : "cancelled")}
        </span>
        {request.status === "Pending" && (
          <button
            type="button"
            onClick={() => onCancel(request.id)}
            className="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-600 transition-colors hover:bg-slate-100"
          >
            {t("changeCancel")}
          </button>
        )}
      </div>
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
  onCannotAttend: (request: ShiftRequestDto) => void;
  onChange: (request: ShiftRequestDto) => void;
}

function ShiftSection({ title, requests, statusKey, cancellationCutoffHours, onCancel, onCannotAttend, onChange }: ShiftSectionProps) {
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
            onCannotAttend={onCannotAttend}
            onChange={onChange}
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
  onCannotAttend: (request: ShiftRequestDto) => void;
  onChange: (request: ShiftRequestDto) => void;
}

function ShiftCard({ request, cancellationCutoffHours, onCancel, onCannotAttend, onChange }: ShiftCardProps) {
  const t = useTranslations("selfService.myShifts");
  const style = STATUS_BADGE_STYLES[request.status];
  const showCancelButton = canCancelShift(request, cancellationCutoffHours);
  const showCannotAttendButton = isFutureApprovedShift(request);

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
        {showCannotAttendButton && (
          <button
            onClick={() => onChange(request)}
            className="text-xs text-sky-700 hover:text-sky-800 border border-sky-200 bg-sky-50 hover:bg-sky-100 px-2.5 py-1 rounded-lg transition-colors"
          >
            {t("changeButton")}
          </button>
        )}
        {showCannotAttendButton && (
          <button
            onClick={() => onCannotAttend(request)}
            className="text-xs text-amber-700 hover:text-amber-800 border border-amber-200 bg-amber-50 hover:bg-amber-100 px-2.5 py-1 rounded-lg transition-colors"
          >
            {t("cannotAttendButton")}
          </button>
        )}
      </div>
    </div>
  );
}
