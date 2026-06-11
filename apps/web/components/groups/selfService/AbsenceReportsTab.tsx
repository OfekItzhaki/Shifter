"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  approveSpecialLeaveRequest,
  getAdminSpecialLeaveRequests,
  rejectSpecialLeaveRequest,
  SpecialLeaveRequestDto,
} from "@/lib/api/specialLeave";
import {
  AbsenceReportDto,
  AvailableSlotDto,
  ShiftChangeRequestDto,
  approveAbsenceReport,
  approveShiftChangeRequest,
  getAbsenceReports,
  getShiftChangeRequests,
  getShiftChangeTargetSlots,
  rejectAbsenceReport,
  rejectShiftChangeRequest,
} from "@/lib/api/selfService";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import { formatSlotDate, formatTime24h } from "@/lib/utils/selfServiceFormat";
import LoadingCard from "./LoadingCard";
import ErrorRetry from "./ErrorRetry";
import MutationButton from "./MutationButton";

interface Props {
  spaceId: string;
  groupId: string;
  onReviewed?: () => void | Promise<void>;
}

type ReviewQueueFilter = "pending" | "all" | "handled";

const STATUS_STYLES: Record<AbsenceReportDto["status"] | ShiftChangeRequestDto["status"] | SpecialLeaveRequestDto["status"], string> = {
  Pending: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-red-200 bg-red-50 text-red-700",
  Cancelled: "border-slate-200 bg-slate-50 text-slate-600",
};

const REVIEW_FILTERS: ReviewQueueFilter[] = ["pending", "all", "handled"];

function pendingFirst<T extends { status: string }>(items: T[]): T[] {
  return [...items].sort((a, b) => {
    if (a.status === b.status) return 0;
    if (a.status === "Pending") return -1;
    if (b.status === "Pending") return 1;
    return 0;
  });
}

function sortAbsenceReportsForReview(items: AbsenceReportDto[]): AbsenceReportDto[] {
  return [...items].sort((a, b) => {
    if (a.status !== b.status) {
      if (a.status === "Pending") return -1;
      if (b.status === "Pending") return 1;
    }

    if (a.status === "Pending" && b.status === "Pending" && a.isLate !== b.isLate) {
      return a.isLate ? -1 : 1;
    }

    return new Date(b.reportedAt).getTime() - new Date(a.reportedAt).getTime();
  });
}

function countPending(items: { status: string }[]): number {
  return items.filter((item) => item.status === "Pending").length;
}

function filterReviewItems<T extends { status: string }>(items: T[], filter: ReviewQueueFilter): T[] {
  if (filter === "pending") return items.filter((item) => item.status === "Pending");
  if (filter === "handled") return items.filter((item) => item.status !== "Pending");
  return items;
}

interface ReviewActivityItem {
  id: string;
  kind: "absence" | "change" | "leave";
  personName: string;
  status: "Approved" | "Rejected" | "Cancelled";
  summary: string;
  occurredAt: string;
  note: string | null;
}

function formatActivityTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

export default function AbsenceReportsTab({ spaceId, groupId, onReviewed }: Props) {
  const t = useTranslations("selfService.absenceReports");
  const [reports, setReports] = useState<AbsenceReportDto[]>([]);
  const [changeRequests, setChangeRequests] = useState<ShiftChangeRequestDto[]>([]);
  const [leaveRequests, setLeaveRequests] = useState<SpecialLeaveRequestDto[]>([]);
  const [changeSlotOptions, setChangeSlotOptions] = useState<AvailableSlotDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [adminNotes, setAdminNotes] = useState<Record<string, string>>({});
  const [changeTargetSlots, setChangeTargetSlots] = useState<Record<string, string>>({});
  const [actionLoading, setActionLoading] = useState<Record<string, "approve" | "reject">>({});
  const [changeActionLoading, setChangeActionLoading] = useState<Record<string, "approve" | "reject">>({});
  const [leaveActionLoading, setLeaveActionLoading] = useState<Record<string, "approve" | "reject">>({});
  const [actionError, setActionError] = useState<string | null>(null);
  const [queueFilter, setQueueFilter] = useState<ReviewQueueFilter>("pending");

  const fetchReports = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [absenceRows, changeRows, targetSlots, leaveRows] = await Promise.all([
        getAbsenceReports(spaceId, groupId),
        getShiftChangeRequests(spaceId, groupId),
        getShiftChangeTargetSlots(spaceId, groupId, "current"),
        getAdminSpecialLeaveRequests(spaceId, undefined, undefined, undefined, groupId),
      ]);
      setReports(absenceRows);
      setChangeRequests(changeRows);
      setLeaveRequests(leaveRows);
      setChangeSlotOptions(targetSlots);
      setChangeTargetSlots((prev) => {
        const next = { ...prev };
        const availableTargetIds = new Set(targetSlots.map((slot) => slot.id));
        for (const request of changeRows) {
          if (next[request.id] && !availableTargetIds.has(next[request.id])) {
            delete next[request.id];
          }
          if (!next[request.id] && request.requestedShiftSlotId && availableTargetIds.has(request.requestedShiftSlotId)) {
            next[request.id] = request.requestedShiftSlotId;
          }
        }
        return next;
      });
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(fetchReports);
  }, [fetchReports]);

  async function review(reportId: string, action: "approve" | "reject") {
    setActionLoading((prev) => ({ ...prev, [reportId]: action }));
    setActionError(null);

    try {
      const note = adminNotes[reportId] ?? "";
      if (action === "approve") {
        await approveAbsenceReport(spaceId, groupId, reportId, note);
      } else {
        await rejectAbsenceReport(spaceId, groupId, reportId, note);
      }
      await fetchReports();
      await onReviewed?.();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setActionError(message);
    } finally {
      setActionLoading((prev) => {
        const next = { ...prev };
        delete next[reportId];
        return next;
      });
    }
  }

  async function reviewChange(changeRequestId: string, action: "approve" | "reject") {
    setChangeActionLoading((prev) => ({ ...prev, [changeRequestId]: action }));
    setActionError(null);

    try {
      const note = adminNotes[changeRequestId] ?? "";
      if (action === "approve") {
        const request = changeRequests.find((row) => row.id === changeRequestId);
        const targetShiftSlotId = request ? getAvailableTargetSlotId(request) || null : null;
        if (!targetShiftSlotId) {
          setActionError(t("changeTargetRequired"));
          return;
        }
        await approveShiftChangeRequest(spaceId, groupId, changeRequestId, note, targetShiftSlotId);
      } else {
        await rejectShiftChangeRequest(spaceId, groupId, changeRequestId, note);
      }
      await fetchReports();
      await onReviewed?.();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setActionError(message);
    } finally {
      setChangeActionLoading((prev) => {
        const next = { ...prev };
        delete next[changeRequestId];
        return next;
      });
    }
  }

  async function reviewLeave(requestId: string, action: "approve" | "reject") {
    setLeaveActionLoading((prev) => ({ ...prev, [requestId]: action }));
    setActionError(null);

    try {
      const note = adminNotes[requestId] ?? "";
      if (action === "approve") {
        await approveSpecialLeaveRequest(spaceId, requestId, note);
      } else {
        await rejectSpecialLeaveRequest(spaceId, requestId, note);
      }
      await fetchReports();
      await onReviewed?.();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setActionError(message);
    } finally {
      setLeaveActionLoading((prev) => {
        const next = { ...prev };
        delete next[requestId];
        return next;
      });
    }
  }

  function formatTargetSlot(slot: AvailableSlotDto) {
    return `${formatSlotDate(slot.date)} - ${formatTime24h(slot.startTime)}-${formatTime24h(slot.endTime)} - ${slot.taskName} (${slot.currentFillCount}/${slot.capacity})`;
  }

  function getAvailableTargetSlotId(request: ShiftChangeRequestDto) {
    const targetShiftSlotId = changeTargetSlots[request.id] ?? request.requestedShiftSlotId ?? "";
    return changeSlotOptions.some((slot) => slot.id === targetShiftSlotId && slot.id !== request.originalShiftSlotId)
      ? targetShiftSlotId
      : "";
  }

  function formatLeaveDate(value: string) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;

    return new Intl.DateTimeFormat(undefined, {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }).format(date);
  }

  if (loading) return <LoadingCard rows={4} variant="list" />;
  if (error) return <ErrorRetry message={error} onRetry={fetchReports} />;

  const visibleReports = filterReviewItems(reports, queueFilter);
  const visibleChangeRequests = filterReviewItems(changeRequests, queueFilter);
  const visibleLeaveRequests = filterReviewItems(leaveRequests, queueFilter);
  const sortedReports = sortAbsenceReportsForReview(visibleReports);
  const sortedChangeRequests = pendingFirst(visibleChangeRequests);
  const sortedLeaveRequests = pendingFirst(visibleLeaveRequests);
  const reportPendingCount = countPending(reports);
  const changePendingCount = countPending(changeRequests);
  const leavePendingCount = countPending(leaveRequests);
  const recentActivity: ReviewActivityItem[] = [
    ...reports
      .filter((report) => report.status !== "Pending")
      .map((report) => ({
        id: `absence-${report.id}`,
        kind: "absence" as const,
        personName: report.personName,
        status: report.status as "Approved" | "Rejected",
        summary: `${formatSlotDate(report.date)} | ${formatTime24h(report.startTime)}-${formatTime24h(report.endTime)} | ${report.taskName}`,
        occurredAt: report.reviewedAt ?? report.reportedAt,
        note: report.adminNote,
      })),
    ...changeRequests
      .filter((request) => request.status !== "Pending")
      .map((request) => ({
        id: `change-${request.id}`,
        kind: "change" as const,
        personName: request.personName,
        status: request.status as "Approved" | "Rejected" | "Cancelled",
        summary: request.requestedSlotDate
          ? `${formatSlotDate(request.originalSlotDate)} -> ${formatSlotDate(request.requestedSlotDate)}`
          : `${formatSlotDate(request.originalSlotDate)} -> ${t("changeFlexibleTarget")}`,
        occurredAt: request.reviewedAt ?? request.requestedAt,
        note: request.adminNote,
      })),
    ...leaveRequests
      .filter((request) => request.status !== "Pending")
      .map((request) => ({
        id: `leave-${request.id}`,
        kind: "leave" as const,
        personName: request.personName,
        status: request.status as "Approved" | "Rejected" | "Cancelled",
        summary: `${formatLeaveDate(request.startsAt)} - ${formatLeaveDate(request.endsAt)}`,
        occurredAt: request.processedAt ?? request.updatedAt,
        note: request.adminNote,
      })),
  ]
    .sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime())
    .slice(0, 5);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-slate-700">{t("title")}</h2>
        <button
          type="button"
          onClick={fetchReports}
          className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 transition-colors hover:bg-slate-50"
        >
          {t("refresh")}
        </button>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-200 bg-white px-3 py-2">
        <span className="text-xs font-medium text-slate-500">{t("filterLabel")}</span>
        <div className="inline-flex rounded-lg border border-slate-200 bg-slate-50 p-1" role="group" aria-label={t("filterAria")}>
          {REVIEW_FILTERS.map((filter) => (
            <button
              key={filter}
              type="button"
              onClick={() => setQueueFilter(filter)}
              className={`rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                queueFilter === filter
                  ? "bg-white text-slate-900 shadow-sm"
                  : "text-slate-500 hover:text-slate-800"
              }`}
              aria-pressed={queueFilter === filter}
            >
              {t(`filter.${filter}`)}
            </button>
          ))}
        </div>
      </div>

      {actionError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700">
          {actionError}
        </div>
      )}

      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">{t("activityTitle")}</h3>
            <p className="text-xs text-slate-500">{t("activityDescription")}</p>
          </div>
          <span className="inline-flex w-fit rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-600">
            {t("activityCount", { count: recentActivity.length })}
          </span>
        </div>

        {recentActivity.length === 0 ? (
          <p className="mt-4 rounded-lg border border-dashed border-slate-200 bg-slate-50 px-3 py-4 text-center text-xs text-slate-400">
            {t("activityEmpty")}
          </p>
        ) : (
          <div className="mt-4 divide-y divide-slate-100">
            {recentActivity.map((item) => (
              <div key={item.id} className="grid gap-2 py-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[item.status]}`}>
                      {t(`status${item.status}`)}
                    </span>
                    <span className="text-xs font-medium text-slate-500">
                      {t(`activityKind${item.kind}`)}
                    </span>
                    <span className="text-sm font-semibold text-slate-900">{item.personName}</span>
                  </div>
                  <p className="mt-1 truncate text-xs text-slate-500">{item.summary}</p>
                  {item.note && (
                    <p className="mt-1 truncate text-xs text-slate-500">{t("adminNote")}: {item.note}</p>
                  )}
                </div>
                <span className="text-xs text-slate-500">{formatActivityTime(item.occurredAt)}</span>
              </div>
            ))}
          </div>
        )}
      </div>

      <QueueHeader
        title={t("absenceReportsTitle")}
        pending={reportPendingCount}
        summaryLabel={t("pendingSummary", { pending: reportPendingCount, total: reports.length })}
      />

      {visibleReports.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-xl border border-slate-200 bg-white py-12 text-center">
          <p className="text-sm text-slate-400">{t(queueFilter === "pending" ? "emptyPending" : "empty")}</p>
        </div>
      ) : (
        <div className="space-y-3">
          {sortedReports.map((report) => (
            <div key={report.id} className="rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900">{report.personName}</p>
                    <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[report.status]}`}>
                      {t(`status${report.status}`)}
                    </span>
                    {report.isLate && (
                      <span className="rounded-full border border-orange-200 bg-orange-50 px-2 py-0.5 text-xs font-medium text-orange-700">
                        {t("late")}
                      </span>
                    )}
                  </div>
                  <p className="mt-1 text-xs text-slate-500">
                    {formatSlotDate(report.date)} | {formatTime24h(report.startTime)}-{formatTime24h(report.endTime)} | {report.taskName}
                  </p>
                  <p className="mt-2 text-sm text-slate-700">{report.reason}</p>
                  {report.status === "Pending" && (
                    <p className="mt-2 rounded-lg border border-sky-100 bg-sky-50 px-3 py-2 text-xs leading-5 text-sky-700">
                      {t("absenceReleasedNotice")}
                    </p>
                  )}
                  {report.adminNote && (
                    <p className="mt-2 text-xs text-slate-500">{t("adminNote")}: {report.adminNote}</p>
                  )}
                </div>

                {report.status === "Pending" && (
                  <div className="w-full shrink-0 space-y-2 sm:w-64">
                    <input
                      value={adminNotes[report.id] ?? ""}
                      onChange={(e) => setAdminNotes((prev) => ({ ...prev, [report.id]: e.target.value }))}
                      maxLength={500}
                      placeholder={t("adminNotePlaceholder")}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-sky-400"
                    />
                    <div className="flex gap-2">
                      <MutationButton
                        onClick={() => review(report.id, "approve")}
                        loading={actionLoading[report.id] === "approve"}
                        disabled={!!actionLoading[report.id]}
                        label={t("approve")}
                        loadingLabel={t("approving")}
                        variant="primary"
                      />
                      <MutationButton
                        onClick={() => review(report.id, "reject")}
                        loading={actionLoading[report.id] === "reject"}
                        disabled={!!actionLoading[report.id]}
                        label={t("reject")}
                        loadingLabel={t("rejecting")}
                        variant="danger"
                      />
                    </div>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      <QueueHeader
        title={t("changeRequestsTitle")}
        pending={changePendingCount}
        summaryLabel={t("pendingSummary", { pending: changePendingCount, total: changeRequests.length })}
      />

      {visibleChangeRequests.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-xl border border-slate-200 bg-white py-12 text-center">
          <p className="text-sm text-slate-400">{t(queueFilter === "pending" ? "changeRequestsEmptyPending" : "changeRequestsEmpty")}</p>
        </div>
      ) : (
        <div className="space-y-3">
          {sortedChangeRequests.map((request) => (
            <div key={request.id} className="rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900">{request.personName}</p>
                    <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[request.status]}`}>
                      {t(`status${request.status}`)}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-slate-500">
                    {t("changeFrom")}: {formatSlotDate(request.originalSlotDate)} | {formatTime24h(request.originalSlotStartTime)}-{formatTime24h(request.originalSlotEndTime)} | {request.originalTaskName}
                  </p>
                  <p className="mt-1 text-xs text-slate-500">
                    {t("changeTo")}: {request.requestedSlotDate
                      ? `${formatSlotDate(request.requestedSlotDate)} | ${formatTime24h(request.requestedSlotStartTime ?? "")}-${formatTime24h(request.requestedSlotEndTime ?? "")} | ${request.requestedTaskName ?? ""}`
                      : t("changeFlexibleTarget")}
                  </p>
                  <p className="mt-2 text-sm text-slate-700">{request.reason}</p>
                  {request.adminNote && (
                    <p className="mt-2 text-xs text-slate-500">{t("adminNote")}: {request.adminNote}</p>
                  )}
                </div>

                {request.status === "Pending" && (
                  <div className="w-full shrink-0 space-y-2 sm:w-64">
                    <label className="block space-y-1">
                      <span className="text-xs font-medium text-slate-600">{t("changeTargetShift")}</span>
                      <select
                        value={getAvailableTargetSlotId(request)}
                        onChange={(e) => setChangeTargetSlots((prev) => ({ ...prev, [request.id]: e.target.value }))}
                        className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs text-slate-700 focus:outline-none focus:ring-2 focus:ring-sky-400"
                      >
                        <option value="">{t("changeTargetRequired")}</option>
                        {changeSlotOptions
                          .filter((slot) => slot.id !== request.originalShiftSlotId)
                          .map((slot) => (
                            <option key={slot.id} value={slot.id}>
                              {formatTargetSlot(slot)}
                            </option>
                          ))}
                      </select>
                    </label>
                    <input
                      value={adminNotes[request.id] ?? ""}
                      onChange={(e) => setAdminNotes((prev) => ({ ...prev, [request.id]: e.target.value }))}
                      maxLength={500}
                      placeholder={t("adminNotePlaceholder")}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-sky-400"
                    />
                    <div className="flex gap-2">
                      <MutationButton
                        onClick={() => reviewChange(request.id, "approve")}
                        loading={changeActionLoading[request.id] === "approve"}
                        disabled={!!changeActionLoading[request.id] || !getAvailableTargetSlotId(request)}
                        label={t("approve")}
                        loadingLabel={t("approving")}
                        variant="primary"
                      />
                      <MutationButton
                        onClick={() => reviewChange(request.id, "reject")}
                        loading={changeActionLoading[request.id] === "reject"}
                        disabled={!!changeActionLoading[request.id]}
                        label={t("reject")}
                        loadingLabel={t("rejecting")}
                        variant="danger"
                      />
                    </div>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      <QueueHeader
        title={t("leaveRequestsTitle")}
        pending={leavePendingCount}
        summaryLabel={t("pendingSummary", { pending: leavePendingCount, total: leaveRequests.length })}
      />

      {visibleLeaveRequests.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-xl border border-slate-200 bg-white py-12 text-center">
          <p className="text-sm text-slate-400">{t(queueFilter === "pending" ? "leaveRequestsEmptyPending" : "leaveRequestsEmpty")}</p>
        </div>
      ) : (
        <div className="space-y-3">
          {sortedLeaveRequests.map((request) => (
            <div key={request.id} className="rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900">{request.personName}</p>
                    <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[request.status]}`}>
                      {t(`status${request.status}`)}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-slate-500">
                    {formatLeaveDate(request.startsAt)} - {formatLeaveDate(request.endsAt)}
                  </p>
                  <p className="mt-2 text-sm text-slate-700">{request.reason}</p>
                  {request.adminNote && (
                    <p className="mt-2 text-xs text-slate-500">{t("adminNote")}: {request.adminNote}</p>
                  )}
                </div>

                {request.status === "Pending" && (
                  <div className="w-full shrink-0 space-y-2 sm:w-64">
                    <input
                      value={adminNotes[request.id] ?? ""}
                      onChange={(e) => setAdminNotes((prev) => ({ ...prev, [request.id]: e.target.value }))}
                      maxLength={500}
                      placeholder={t("adminNotePlaceholder")}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-sky-400"
                    />
                    <div className="flex gap-2">
                      <MutationButton
                        onClick={() => reviewLeave(request.id, "approve")}
                        loading={leaveActionLoading[request.id] === "approve"}
                        disabled={!!leaveActionLoading[request.id]}
                        label={t("approve")}
                        loadingLabel={t("approving")}
                        variant="primary"
                      />
                      <MutationButton
                        onClick={() => reviewLeave(request.id, "reject")}
                        loading={leaveActionLoading[request.id] === "reject"}
                        disabled={!!leaveActionLoading[request.id]}
                        label={t("reject")}
                        loadingLabel={t("rejecting")}
                        variant="danger"
                      />
                    </div>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function QueueHeader({
  title,
  pending,
  summaryLabel,
}: {
  title: string;
  pending: number;
  summaryLabel: string;
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
      <h2 className="text-sm font-semibold text-slate-700">{title}</h2>
      <span
        className={`rounded-full border px-2.5 py-1 text-xs font-medium ${
          pending > 0
            ? "border-amber-200 bg-amber-50 text-amber-700"
            : "border-slate-200 bg-slate-50 text-slate-500"
        }`}
      >
        {summaryLabel}
      </span>
    </div>
  );
}
