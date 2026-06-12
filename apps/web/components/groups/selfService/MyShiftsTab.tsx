"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  AbsenceReportDto,
  AvailableSlotDto,
  ShiftChangeRequestDto,
  cancelShiftChangeRequest,
  getAvailableSlots,
  getMyAbsenceReports,
  getMyShiftChangeRequests,
  getMyShiftRequests,
  getMyWaitlistEntries,
  cancelShiftRequest,
  reportCannotAttend,
  submitShiftChangeRequest,
  WaitlistEntryDto,
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
  onNavigate?: (tab: "available-slots" | "waitlist" | "swaps") => void;
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

type ActivityStatus =
  | ShiftRequestDto["status"]
  | SpecialLeaveRequestDto["status"]
  | WaitlistEntryDto["status"];

interface RequestActivityItem {
  id: string;
  title: string;
  detail: string;
  status: ActivityStatus;
  occurredAt: string;
  note: string | null;
}

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
 * Determines if a shift can be cancelled based on the request window and cancellation cutoff.
 * Backend allows cancellation while the request window is open; after it closes, cutoff applies.
 */
function canCancelShift(request: ShiftRequestDto, cancellationCutoffHours: number): boolean {
  if (request.status !== "Approved") return false;

  try {
    // Build the shift start datetime from slotDate + slotStartTime
    const shiftStart = new Date(`${request.slotDate}T${request.slotStartTime}`);
    if (isNaN(shiftStart.getTime())) return false;

    const now = new Date();
    if (shiftStart.getTime() <= now.getTime()) return false;
    if (request.requestWindowOpen) return true;

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

function isLateAbsenceReport(request: ShiftRequestDto, lateWindowHours: number): boolean {
  if (!isFutureApprovedShift(request)) return false;

  const shiftStart = new Date(`${request.slotDate}T${request.slotStartTime}`);
  const lateWindowMs = lateWindowHours * 60 * 60 * 1000;
  return shiftStart.getTime() - Date.now() <= lateWindowMs;
}

function formatActivityDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return date.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function sortActivityByNewest(items: RequestActivityItem[]): RequestActivityItem[] {
  return [...items].sort((a, b) => {
    const bTime = new Date(b.occurredAt).getTime();
    const aTime = new Date(a.occurredAt).getTime();
    return (Number.isNaN(bTime) ? 0 : bTime) - (Number.isNaN(aTime) ? 0 : aTime);
  });
}

export default function MyShiftsTab({ spaceId, groupId, onNavigate }: MyShiftsTabProps) {
  const t = useTranslations("selfService.myShifts");

  const [data, setData] = useState<MyShiftsResponse | null>(null);
  const [specialLeaveRequests, setSpecialLeaveRequests] = useState<SpecialLeaveRequestDto[]>([]);
  const [changeRequests, setChangeRequests] = useState<ShiftChangeRequestDto[]>([]);
  const [absenceReports, setAbsenceReports] = useState<AbsenceReportDto[]>([]);
  const [waitlistEntries, setWaitlistEntries] = useState<WaitlistEntryDto[]>([]);
  const [absenceReportsUsed, setAbsenceReportsUsed] = useState(0);
  const [maxAbsenceReports, setMaxAbsenceReports] = useState(0);
  const [lateReportsUsed, setLateReportsUsed] = useState(0);
  const [maxLateReports, setMaxLateReports] = useState(0);
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

  const fetchData = useCallback(async (showLoading = true) => {
    try {
      if (showLoading) {
        setLoading(true);
      }
      setError(null);
      const [response, leaveRequests, shiftChanges, absenceResponse, waitlistResponse] = await Promise.all([
        getMyShiftRequests(spaceId, groupId),
        getMySpecialLeaveRequests(spaceId),
        getMyShiftChangeRequests(spaceId, groupId),
        getMyAbsenceReports(spaceId, groupId),
        getMyWaitlistEntries(spaceId, groupId),
      ]);
      setData(response);
      setSpecialLeaveRequests(leaveRequests);
      setChangeRequests(shiftChanges);
      setAbsenceReports(absenceResponse.reports);
      setWaitlistEntries(waitlistResponse);
      setAbsenceReportsUsed(absenceResponse.absenceReportsUsed ?? absenceResponse.reports.filter((report) => report.status !== "Rejected").length);
      setMaxAbsenceReports(absenceResponse.maxAbsenceReports ?? 3);
      setLateReportsUsed(absenceResponse.lateReportsUsed);
      setMaxLateReports(absenceResponse.maxLateReports);
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      if (showLoading) {
        setLoading(false);
      }
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(() => fetchData());
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
      await fetchData(false);
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
          : t("cannotAttendSubmitted", {
              used: result.absenceReportsUsed,
              max: result.maxAbsenceReports,
            })
      );
      closeCannotAttendDialog();
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setCannotAttendError(message);
      await fetchData(false);
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
      const slots = await getAvailableSlots(spaceId, groupId, request.schedulingCycleId);
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
    setLeaveSuccess(null);

    try {
      await submitShiftChangeRequest(
        spaceId,
        groupId,
        changeTarget.id,
        changeReason.trim(),
        changeRequestedSlotId || null
      );
      closeChangeDialog();
      setLeaveSuccess(t("changeRequestSubmitted"));
      await fetchData();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setChangeError(message);
      await fetchData(false);
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
      await fetchData(false);
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
      await fetchData(false);
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
      await fetchData(false);
    }
  }

  // ── Loading state ────────────────────────────────────────────────────────

  if (loading) {
    return <LoadingCard rows={4} variant="list" />;
  }

  // ── Error state ──────────────────────────────────────────────────────────

  if (error) {
    return <ErrorRetry message={error} onRetry={() => fetchData()} />;
  }

  if (!data) return null;

  const { approved, pending, cancelled } = groupByStatus(data.requests);
  const {
    currentShiftCount,
    minShiftsPerCycle,
    maxShiftsPerCycle,
    cancellationCutoffHours,
    lateCancellationWindowHours,
  } = data;
  const isUnderScheduled = currentShiftCount < minShiftsPerCycle;
  const allowShiftChangeRequests = data.allowShiftChangeRequests ?? true;
  const allowAbsenceReports = data.allowAbsenceReports ?? true;
  const allowShiftSwaps = data.allowShiftSwaps ?? true;
  const pendingLeaveCount = specialLeaveRequests.filter((request) => request.status === "Pending").length;
  const pendingChangeCount = changeRequests.filter((request) => request.status === "Pending").length;
  const pendingAbsenceCount = absenceReports.filter((report) => report.status === "Pending").length;
  const absenceReportsRemaining = Math.max(0, maxAbsenceReports - absenceReportsUsed);
  const lateReportsRemaining = Math.max(0, maxLateReports - lateReportsUsed);
  const waitlistOfferCount = waitlistEntries.filter((entry) => entry.status === "Offered").length;
  const waitingCount = waitlistEntries.filter((entry) => entry.status === "Waiting").length;
  const cannotAttendWouldBeLate = cannotAttendTarget
    ? isLateAbsenceReport(cannotAttendTarget, lateCancellationWindowHours)
    : false;
  const cannotAttendLimitReached = absenceReportsRemaining <= 0
    || (cannotAttendWouldBeLate && lateReportsRemaining <= 0);
  const nextShift = [...approved]
    .filter(isFutureApprovedShift)
    .sort((a, b) =>
      `${a.slotDate}T${a.slotStartTime}`.localeCompare(`${b.slotDate}T${b.slotStartTime}`)
    )[0] ?? null;
  const requestActivity = sortActivityByNewest([
    ...data.requests.map((request) => ({
      id: `shift-${request.id}`,
      title: t("activityKindShift"),
      detail: `${formatSlotDate(request.slotDate)} ${formatTime24h(request.slotStartTime)}-${formatTime24h(request.slotEndTime)} - ${request.taskName}`,
      status: request.status,
      occurredAt: request.cancelledAt ?? request.createdAt,
      note: request.cancellationReason ?? request.rejectionReason,
    })),
    ...specialLeaveRequests.map((request) => ({
      id: `leave-${request.id}`,
      title: t("activityKindLeave"),
      detail: `${formatSpecialLeaveDate(request.startsAt)} - ${formatSpecialLeaveDate(request.endsAt)}`,
      status: request.status,
      occurredAt: request.processedAt ?? request.updatedAt,
      note: request.adminNote ?? request.reason,
    })),
    ...changeRequests.map((request) => ({
      id: `change-${request.id}`,
      title: t("activityKindChange"),
      detail: request.requestedSlotDate
        ? `${formatSlotDate(request.originalSlotDate)} -> ${formatSlotDate(request.requestedSlotDate)}`
        : `${formatSlotDate(request.originalSlotDate)} -> ${t("changeFlexibleTarget")}`,
      status: request.status,
      occurredAt: request.reviewedAt ?? request.requestedAt,
      note: request.adminNote ?? request.reason,
    })),
    ...absenceReports.map((report) => ({
      id: `absence-${report.id}`,
      title: t("activityKindAbsence"),
      detail: `${formatSlotDate(report.date)} ${formatTime24h(report.startTime)}-${formatTime24h(report.endTime)} - ${report.taskName}`,
      status: report.status,
      occurredAt: report.reviewedAt ?? report.reportedAt,
      note: report.adminNote ?? report.reason,
    })),
    ...waitlistEntries.map((entry) => ({
      id: `waitlist-${entry.id}`,
      title: t("activityKindWaitlist"),
      detail: `${formatSlotDate(entry.slotDate)} ${formatTime24h(entry.slotStartTime)}-${formatTime24h(entry.slotEndTime)} - ${entry.taskName}`,
      status: entry.status,
      occurredAt: entry.expiresAt ?? entry.offeredAt ?? entry.slotDate,
      note: null,
    })),
  ]).slice(0, 8);

  // ── Render ───────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">{t("summaryTitle")}</h3>
            <p className="text-xs text-slate-500">{t("summaryDescription")}</p>
          </div>
          {isUnderScheduled && onNavigate && (
            <button
              type="button"
              onClick={() => onNavigate("available-slots")}
              className="mt-2 inline-flex w-fit rounded-lg bg-sky-600 px-3 py-2 text-xs font-semibold text-white transition-colors hover:bg-sky-700 sm:mt-0"
            >
              {t("summaryPickShifts")}
            </button>
          )}
        </div>

        <div className="mt-4 grid gap-2 md:grid-cols-2 xl:grid-cols-5">
          <SummaryCard
            label={t("summaryMinimumLabel")}
            value={t("summaryMinimumValue", {
              current: currentShiftCount,
              min: minShiftsPerCycle,
              max: maxShiftsPerCycle,
            })}
            tone={isUnderScheduled ? "warning" : "ok"}
          />
          <SummaryCard
            label={t("summaryRequestsLabel")}
            value={t("summaryRequestsValue", {
              leave: pendingLeaveCount,
              changes: pendingChangeCount,
              absences: pendingAbsenceCount,
            })}
            tone={pendingLeaveCount + pendingChangeCount + pendingAbsenceCount > 0 ? "warning" : "default"}
          />
          <SummaryCard
            label={t("summaryWaitlistLabel")}
            value={t("summaryWaitlistValue", {
              offered: waitlistOfferCount,
              waiting: waitingCount,
            })}
            tone={waitlistOfferCount > 0 ? "danger" : waitingCount > 0 ? "warning" : "default"}
            onClick={onNavigate && waitlistEntries.length > 0 ? () => onNavigate("waitlist") : undefined}
            actionLabel={waitlistEntries.length > 0 ? t("summaryOpenWaitlist") : undefined}
          />
          <SummaryCard
            label={t("summaryLateAbsenceLabel")}
            value={t("summaryLateAbsenceValue", {
              used: lateReportsUsed,
              max: maxLateReports,
              window: lateCancellationWindowHours,
            })}
            tone={lateReportsRemaining <= 0 ? "danger" : lateReportsRemaining === 1 ? "warning" : "default"}
          />
          <SummaryCard
            label={t("summaryAbsenceLabel")}
            value={t("summaryAbsenceValue", {
              used: absenceReportsUsed,
              max: maxAbsenceReports,
            })}
            tone={absenceReportsRemaining <= 0 ? "danger" : absenceReportsRemaining === 1 ? "warning" : "default"}
          />
          <SummaryCard
            label={t("summaryNextShiftLabel")}
            value={nextShift
              ? `${formatSlotDate(nextShift.slotDate)} ${formatTime24h(nextShift.slotStartTime)}-${formatTime24h(nextShift.slotEndTime)}`
              : t("summaryNoNextShift")}
            tone={nextShift ? "default" : "warning"}
          />
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex flex-col gap-1">
          <h3 className="text-sm font-semibold text-slate-900">{t("actionGuideTitle")}</h3>
          <p className="text-xs text-slate-500">{t("actionGuideDescription")}</p>
        </div>
        <div className="mt-4 grid gap-3 md:grid-cols-3">
          <ActionGuideCard
            title={t("actionGuide.cancel.title")}
            description={t("actionGuide.cancel.description", { cutoff: cancellationCutoffHours })}
            tone="default"
          />
          <ActionGuideCard
            title={t("actionGuide.change.title")}
            description={t("actionGuide.change.description")}
            tone="default"
            actionLabel={onNavigate && allowShiftSwaps ? t("actionGuide.change.swapAction") : undefined}
            onAction={onNavigate && allowShiftSwaps ? () => onNavigate("swaps") : undefined}
          />
          <ActionGuideCard
            title={t("actionGuide.cannotAttend.title")}
            description={t("actionGuide.cannotAttend.description", {
              totalRemaining: absenceReportsRemaining,
              totalMax: maxAbsenceReports,
              remaining: lateReportsRemaining,
              max: maxLateReports,
              window: lateCancellationWindowHours,
            })}
            tone={absenceReportsRemaining <= 0 || lateReportsRemaining <= 0 ? "danger" : absenceReportsRemaining === 1 || lateReportsRemaining === 1 ? "warning" : "default"}
          />
        </div>
      </div>

      <RequestActivityTimeline items={requestActivity} />

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
              data-testid="self-service-special-leave-start"
              type="datetime-local"
              value={leaveStart}
              onChange={(e) => setLeaveStart(e.target.value)}
              className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400"
            />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">{t("specialLeaveEnd")}</span>
            <input
              data-testid="self-service-special-leave-end"
              type="datetime-local"
              value={leaveEnd}
              onChange={(e) => setLeaveEnd(e.target.value)}
              className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400"
            />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">{t("specialLeaveReason")}</span>
            <input
              data-testid="self-service-special-leave-reason"
              value={leaveReason}
              onChange={(e) => setLeaveReason(e.target.value)}
              maxLength={500}
              placeholder={t("specialLeaveReasonPlaceholder")}
              className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400"
            />
          </label>
          <MutationButton
            data-testid="self-service-submit-special-leave"
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

      <div className="bg-white border border-slate-200 rounded-xl px-4 py-4">
        <div className="flex flex-col gap-1 mb-4">
          <h3 className="text-sm font-semibold text-slate-900">{t("absenceReportsTitle")}</h3>
          <p className="text-xs text-slate-500">
            {t("absenceReportsDescription", {
              used: absenceReportsUsed,
              max: maxAbsenceReports,
              lateUsed: lateReportsUsed,
              lateMax: maxLateReports,
            })}
          </p>
        </div>

        {absenceReports.length === 0 ? (
          <p className="text-xs text-slate-400">{t("absenceReportsEmpty")}</p>
        ) : (
          <div className="space-y-2">
            {absenceReports.map((report) => (
              <AbsenceReportCard key={report.id} report={report} />
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
          lateCancellationWindowHours={lateCancellationWindowHours}
          absenceReportsRemaining={absenceReportsRemaining}
          lateReportsRemaining={lateReportsRemaining}
          onCancel={openCancelDialog}
          onCannotAttend={openCannotAttendDialog}
          onChange={openChangeDialog}
          allowAbsenceReports={allowAbsenceReports}
          allowShiftChangeRequests={allowShiftChangeRequests}
        />
      )}

      {/* Pending shifts */}
      {pending.length > 0 && (
        <ShiftSection
          title={t("pending")}
          requests={pending}
          statusKey="Pending"
          cancellationCutoffHours={cancellationCutoffHours}
          lateCancellationWindowHours={lateCancellationWindowHours}
          absenceReportsRemaining={absenceReportsRemaining}
          lateReportsRemaining={lateReportsRemaining}
          onCancel={openCancelDialog}
          onCannotAttend={openCannotAttendDialog}
          onChange={openChangeDialog}
          allowAbsenceReports={allowAbsenceReports}
          allowShiftChangeRequests={allowShiftChangeRequests}
        />
      )}

      {/* Cancelled shifts */}
      {cancelled.length > 0 && (
        <ShiftSection
          title={t("cancelled")}
          requests={cancelled}
          statusKey="Cancelled"
          cancellationCutoffHours={cancellationCutoffHours}
          lateCancellationWindowHours={lateCancellationWindowHours}
          absenceReportsRemaining={absenceReportsRemaining}
          lateReportsRemaining={lateReportsRemaining}
          onCancel={openCancelDialog}
          onCannotAttend={openCannotAttendDialog}
          onChange={openChangeDialog}
          allowAbsenceReports={allowAbsenceReports}
          allowShiftChangeRequests={allowShiftChangeRequests}
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
              data-testid="self-service-confirm-cancel-shift"
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
          {cannotAttendTarget && (
            <div className={`rounded-lg border px-3 py-2 text-xs ${
              cannotAttendLimitReached
                ? "border-red-200 bg-red-50 text-red-700"
                : cannotAttendWouldBeLate
                  ? "border-amber-200 bg-amber-50 text-amber-800"
                  : "border-slate-200 bg-slate-50 text-slate-600"
            }`}>
              {absenceReportsRemaining <= 0
                ? t("cannotAttendTotalPolicy", {
                    used: absenceReportsUsed,
                    max: maxAbsenceReports,
                  })
                : cannotAttendWouldBeLate
                  ? t("cannotAttendLatePolicy", {
                    used: lateReportsUsed,
                    max: maxLateReports,
                    totalUsed: absenceReportsUsed,
                    totalMax: maxAbsenceReports,
                    remaining: lateReportsRemaining,
                    window: lateCancellationWindowHours,
                  })
                : t("cannotAttendNotLatePolicy", {
                    used: absenceReportsUsed,
                    max: maxAbsenceReports,
                    window: lateCancellationWindowHours,
                  })}
            </div>
          )}

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
          {cannotAttendLimitReached && (
            <p className="text-xs text-red-600">{t("cannotAttendLimitReached")}</p>
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
              data-testid="self-service-confirm-cannot-attend"
              loading={reportingCannotAttend}
              disabled={cannotAttendReason.trim().length === 0 || cannotAttendLimitReached}
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
              data-testid="self-service-change-target-slot"
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
              data-testid="self-service-confirm-change"
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

function SummaryCard({
  label,
  value,
  tone = "default",
  actionLabel,
  onClick,
}: {
  label: string;
  value: string;
  tone?: "default" | "ok" | "warning" | "danger";
  actionLabel?: string;
  onClick?: () => void;
}) {
  const toneClass =
    tone === "danger"
      ? "border-red-200 bg-red-50 text-red-900"
      : tone === "warning"
        ? "border-amber-200 bg-amber-50 text-amber-900"
        : tone === "ok"
          ? "border-emerald-200 bg-emerald-50 text-emerald-900"
          : "border-slate-200 bg-slate-50 text-slate-900";

  const content = (
    <>
      <div className="text-xs font-medium text-slate-500">{label}</div>
      <div className="mt-1 text-sm font-semibold">{value}</div>
      {actionLabel && <div className="mt-2 text-xs font-medium text-sky-700">{actionLabel}</div>}
    </>
  );

  if (onClick) {
    return (
      <button
        type="button"
        onClick={onClick}
        className={`rounded-lg border px-3 py-2 text-left transition-colors hover:border-sky-300 hover:bg-sky-50 focus:outline-none focus:ring-2 focus:ring-sky-400 ${toneClass}`}
      >
        {content}
      </button>
    );
  }

  return (
    <div className={`rounded-lg border px-3 py-2 ${toneClass}`}>
      {content}
    </div>
  );
}

function ActionGuideCard({
  title,
  description,
  tone,
  actionLabel,
  onAction,
}: {
  title: string;
  description: string;
  tone: "default" | "warning" | "danger";
  actionLabel?: string;
  onAction?: () => void;
}) {
  const toneClass =
    tone === "danger"
      ? "border-red-200 bg-red-50"
      : tone === "warning"
        ? "border-amber-200 bg-amber-50"
        : "border-slate-200 bg-slate-50";

  return (
    <div className={`rounded-lg border px-3 py-3 ${toneClass}`}>
      <p className="text-sm font-semibold text-slate-900">{title}</p>
      <p className="mt-1 text-xs leading-5 text-slate-600">{description}</p>
      {actionLabel && onAction && (
        <button
          type="button"
          onClick={onAction}
          className="mt-3 rounded-lg border border-sky-200 bg-white px-3 py-1.5 text-xs font-medium text-sky-700 transition-colors hover:bg-sky-50 focus:outline-none focus:ring-2 focus:ring-sky-400"
        >
          {actionLabel}
        </button>
      )}
    </div>
  );
}

function RequestActivityTimeline({ items }: { items: RequestActivityItem[] }) {
  const t = useTranslations("selfService.myShifts");

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 className="text-sm font-semibold text-slate-900">{t("activityTitle")}</h3>
          <p className="text-xs text-slate-500">{t("activityDescription")}</p>
        </div>
        <span className="inline-flex w-fit rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-600">
          {t("activityCount", { count: items.length })}
        </span>
      </div>

      {items.length === 0 ? (
        <p className="mt-4 text-xs text-slate-400">{t("activityEmpty")}</p>
      ) : (
        <ol className="mt-4 divide-y divide-slate-100">
          {items.map((item) => (
            <li key={item.id} className="grid gap-2 py-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-xs font-semibold uppercase text-slate-500">
                    {item.title}
                  </span>
                  <ActivityStatusBadge status={item.status} />
                </div>
                <p className="mt-1 truncate text-sm font-medium text-slate-900">{item.detail}</p>
                {item.note && (
                  <p className="mt-0.5 line-clamp-2 text-xs text-slate-500">{item.note}</p>
                )}
              </div>
              <time className="text-xs text-slate-400" dateTime={item.occurredAt}>
                {formatActivityDateTime(item.occurredAt)}
              </time>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

function ActivityStatusBadge({ status }: { status: ActivityStatus }) {
  const t = useTranslations("selfService.myShifts");
  const statusClass =
    status === "Approved" || status === "Accepted"
      ? "border-emerald-200 bg-emerald-50 text-emerald-700"
      : status === "Pending" || status === "Waiting" || status === "Offered"
        ? "border-amber-200 bg-amber-50 text-amber-700"
        : status === "Rejected" || status === "Declined"
          ? "border-red-200 bg-red-50 text-red-700"
          : "border-slate-200 bg-slate-50 text-slate-600";

  const labelMap: Record<ActivityStatus, string> = {
    Approved: t("approved"),
    Pending: t("pending"),
    Rejected: t("rejected"),
    Cancelled: t("cancelled"),
    Waiting: t("activityStatusWaiting"),
    Offered: t("activityStatusOffered"),
    Accepted: t("activityStatusAccepted"),
    Expired: t("activityStatusExpired"),
    Declined: t("activityStatusDeclined"),
    Removed: t("activityStatusRemoved"),
  };

  return (
    <span className={`inline-flex rounded-full border px-2 py-0.5 text-xs font-medium ${statusClass}`}>
      {labelMap[status]}
    </span>
  );
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
    <div
      data-testid="self-service-special-leave-card"
      data-special-leave-request-id={request.id}
      className="flex flex-col gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 sm:flex-row sm:items-center sm:justify-between"
    >
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
            data-testid="self-service-cancel-special-leave"
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

function AbsenceReportCard({ report }: { report: AbsenceReportDto }) {
  const t = useTranslations("selfService.myShifts");
  const style = STATUS_BADGE_STYLES[report.status];
  const statusLabel = (() => {
    switch (report.status) {
      case "Approved": return t("approved");
      case "Pending": return t("pending");
      case "Rejected": return t("rejected");
    }
  })();

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-xs font-medium text-slate-800">
            {formatSlotDate(report.date)} {formatTime24h(report.startTime)}-{formatTime24h(report.endTime)} - {report.taskName}
          </p>
          {report.isLate && (
            <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700">
              {t("absenceLate")}
            </span>
          )}
        </div>
        <p className="mt-0.5 truncate text-xs text-slate-500">{report.reason}</p>
        {report.adminNote && (
          <p className="mt-0.5 text-xs text-slate-500">
            {t("absenceAdminNote")}: {report.adminNote}
          </p>
        )}
      </div>
      <span className={`inline-flex w-fit items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium ${style.badge}`}>
        <span className={`h-1.5 w-1.5 rounded-full ${style.dot}`} />
        {statusLabel}
      </span>
    </div>
  );
}

interface ShiftSectionProps {
  title: string;
  requests: ShiftRequestDto[];
  statusKey: ShiftRequestDto["status"];
  cancellationCutoffHours: number;
  lateCancellationWindowHours: number;
  absenceReportsRemaining: number;
  lateReportsRemaining: number;
  allowAbsenceReports: boolean;
  allowShiftChangeRequests: boolean;
  onCancel: (request: ShiftRequestDto) => void;
  onCannotAttend: (request: ShiftRequestDto) => void;
  onChange: (request: ShiftRequestDto) => void;
}

function ShiftSection({
  title,
  requests,
  statusKey,
  cancellationCutoffHours,
  lateCancellationWindowHours,
  absenceReportsRemaining,
  lateReportsRemaining,
  allowAbsenceReports,
  allowShiftChangeRequests,
  onCancel,
  onCannotAttend,
  onChange,
}: ShiftSectionProps) {
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
            lateCancellationWindowHours={lateCancellationWindowHours}
            absenceReportsRemaining={absenceReportsRemaining}
            lateReportsRemaining={lateReportsRemaining}
            allowAbsenceReports={allowAbsenceReports}
            allowShiftChangeRequests={allowShiftChangeRequests}
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
  lateCancellationWindowHours: number;
  absenceReportsRemaining: number;
  lateReportsRemaining: number;
  allowAbsenceReports: boolean;
  allowShiftChangeRequests: boolean;
  onCancel: (request: ShiftRequestDto) => void;
  onCannotAttend: (request: ShiftRequestDto) => void;
  onChange: (request: ShiftRequestDto) => void;
}

function ShiftCard({
  request,
  cancellationCutoffHours,
  lateCancellationWindowHours,
  absenceReportsRemaining,
  lateReportsRemaining,
  allowAbsenceReports,
  allowShiftChangeRequests,
  onCancel,
  onCannotAttend,
  onChange,
}: ShiftCardProps) {
  const t = useTranslations("selfService.myShifts");
  const style = STATUS_BADGE_STYLES[request.status];
  const showCancelButton = canCancelShift(request, cancellationCutoffHours);
  const futureApprovedShift = isFutureApprovedShift(request);
  const showChangeButton = futureApprovedShift && allowShiftChangeRequests;
  const showCannotAttendButton = futureApprovedShift && allowAbsenceReports;
  const absenceReportWouldBeBlocked = showCannotAttendButton
    && absenceReportsRemaining <= 0;
  const lateReportWouldBeBlocked = showCannotAttendButton
    && isLateAbsenceReport(request, lateCancellationWindowHours)
    && lateReportsRemaining <= 0;
  const cannotAttendWouldBeBlocked = absenceReportWouldBeBlocked || lateReportWouldBeBlocked;

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
    <div
      data-testid="self-service-shift-card"
      data-shift-request-id={request.id}
      className="bg-white border border-slate-200 rounded-xl px-4 py-3"
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
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

        <div className="flex flex-wrap items-center gap-2 sm:flex-shrink-0">
          {/* Status badge */}
          <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium border ${style.badge}`}>
            <span className={`w-1.5 h-1.5 rounded-full ${style.dot}`} />
            {statusLabel}
          </span>

          {/* Cancel button */}
          {showCancelButton && (
            <button
              onClick={() => onCancel(request)}
              data-testid="self-service-cancel-shift"
              className="text-xs text-red-600 hover:text-red-700 border border-red-200 bg-red-50 hover:bg-red-100 px-2.5 py-1 rounded-lg transition-colors"
            >
              {t("cancelButton")}
            </button>
          )}
          {showChangeButton && (
            <button
              onClick={() => onChange(request)}
              data-testid="self-service-change-shift"
              className="text-xs text-sky-700 hover:text-sky-800 border border-sky-200 bg-sky-50 hover:bg-sky-100 px-2.5 py-1 rounded-lg transition-colors"
            >
              {t("changeButton")}
            </button>
          )}
          {showCannotAttendButton && (
            <button
              onClick={() => onCannotAttend(request)}
              data-testid="self-service-cannot-attend"
              disabled={cannotAttendWouldBeBlocked}
              title={cannotAttendWouldBeBlocked ? t("cannotAttendLimitReached") : undefined}
              className="text-xs text-amber-700 hover:text-amber-800 border border-amber-200 bg-amber-50 hover:bg-amber-100 px-2.5 py-1 rounded-lg transition-colors disabled:cursor-not-allowed disabled:border-slate-200 disabled:bg-slate-100 disabled:text-slate-400"
            >
              {t("cannotAttendButton")}
            </button>
          )}
        </div>
      </div>

      {cannotAttendWouldBeBlocked && (
        <p className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-5 text-amber-800">
          {t("cannotAttendLimitReached")}
        </p>
      )}
    </div>
  );
}
