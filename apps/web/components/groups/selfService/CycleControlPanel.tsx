"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  checkUnderScheduledMembers,
  closeSelfServiceCycleWindow,
  generateNextSelfServiceCycle,
  getSelfServiceCycleStatus,
  openSelfServiceCycleWindow,
  SelfServiceCycleStatusDto,
  UnderScheduledMemberDto,
} from "@/lib/api/selfService";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import MutationButton from "./MutationButton";

interface CycleControlPanelProps {
  spaceId: string;
  groupId: string;
  onNavigate?: (tab: "absence-reports" | "waitlist") => void;
  onStatusChanged?: () => void | Promise<void>;
}

function formatDateTime(value: string | null): string {
  if (!value) return "-";
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
  }).format(new Date(value));
}

function formatCoverage(status: SelfServiceCycleStatusDto): string {
  if (status.totalCapacity <= 0) return "0%";
  return `${Math.round((status.filledCount / status.totalCapacity) * 100)}%`;
}

export default function CycleControlPanel({ spaceId, groupId, onNavigate, onStatusChanged }: CycleControlPanelProps) {
  const t = useTranslations("selfService.operations.cycle");
  const [status, setStatus] = useState<SelfServiceCycleStatusDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [action, setAction] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [underScheduled, setUnderScheduled] = useState<UnderScheduledMemberDto[] | null>(null);

  const load = useCallback(async (showLoading = true) => {
    if (showLoading) {
      setLoading(true);
    }
    setError(null);
    try {
      setStatus(await getSelfServiceCycleStatus(spaceId, groupId));
    } catch (err) {
      setError(getSelfServiceErrorMessage(err).message);
    } finally {
      if (showLoading) {
        setLoading(false);
      }
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(() => load());
  }, [load]);

  const refreshStatusAfterFailure = useCallback(async () => {
    try {
      setStatus(await getSelfServiceCycleStatus(spaceId, groupId));
      await onStatusChanged?.();
    } catch {
      // Keep the original action error visible when the follow-up refresh fails.
    }
  }, [spaceId, groupId, onStatusChanged]);

  async function run(name: string, fn: () => Promise<SelfServiceCycleStatusDto | void>) {
    setAction(name);
    setError(null);
    setUnderScheduled(null);
    try {
      const next = await fn();
      if (next) setStatus(next);
      else await load();
      await onStatusChanged?.();
    } catch (err) {
      setError(getSelfServiceErrorMessage(err).message);
      await refreshStatusAfterFailure();
    } finally {
      setAction(null);
    }
  }

  async function handleRefresh() {
    setAction("refresh");
    setUnderScheduled(null);
    try {
      await load(false);
      await onStatusChanged?.();
    } finally {
      setAction(null);
    }
  }

  async function handleUnderScheduled() {
    if (!status?.cycleId) return;

    setAction("under");
    setError(null);
    try {
      const result = await checkUnderScheduledMembers(spaceId, groupId, status.cycleId);
      setUnderScheduled(result.underScheduledMembers);
      await load();
      await onStatusChanged?.();
    } catch (err) {
      setError(getSelfServiceErrorMessage(err).message);
      await refreshStatusAfterFailure();
    } finally {
      setAction(null);
    }
  }

  const hasCycle = !!status?.cycleId;
  const pendingReviewCount = status
    ? status.pendingAbsenceReportCount
      + status.pendingShiftChangeRequestCount
      + status.pendingSpecialLeaveRequestCount
    : 0;
  const hasUnderScheduledResult = underScheduled !== null;
  const underScheduledCount = underScheduled?.length ?? 0;
  const closeChecklistItems = status
    ? [
        {
          key: "coverage",
          state: status.underfilledSlotCount > 0 ? "warning" : "ok",
          label: t("closeChecklist.items.coverage.label"),
          value: status.underfilledSlotCount > 0
            ? t("closeChecklist.items.coverage.warning", { count: status.underfilledSlotCount })
            : t("closeChecklist.items.coverage.ok"),
          onClick: undefined,
        },
        {
          key: "reviews",
          state: pendingReviewCount > 0 ? "warning" : "ok",
          label: t("closeChecklist.items.reviews.label"),
          value: pendingReviewCount > 0
            ? t("closeChecklist.items.reviews.warning", { count: pendingReviewCount })
            : t("closeChecklist.items.reviews.ok"),
          onClick: pendingReviewCount > 0 && onNavigate ? () => onNavigate("absence-reports") : undefined,
        },
        {
          key: "waitlist",
          state: status.waitlistCount > 0 ? "warning" : "ok",
          label: t("closeChecklist.items.waitlist.label"),
          value: status.waitlistCount > 0
            ? t("closeChecklist.items.waitlist.warning", { count: status.waitlistCount })
            : t("closeChecklist.items.waitlist.ok"),
          onClick: status.waitlistCount > 0 && onNavigate ? () => onNavigate("waitlist") : undefined,
        },
        {
          key: "underScheduled",
          state: !hasUnderScheduledResult ? "unknown" : underScheduledCount > 0 ? "warning" : "ok",
          label: t("closeChecklist.items.underScheduled.label"),
          value: !hasUnderScheduledResult
            ? t("closeChecklist.items.underScheduled.unknown")
            : underScheduledCount > 0
              ? t("closeChecklist.items.underScheduled.warning", { count: underScheduledCount })
              : t("closeChecklist.items.underScheduled.ok"),
          onClick: undefined,
        },
      ]
    : [];
  const closeChecklistWarningCount = closeChecklistItems.filter((item) => item.state === "warning").length;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{t("title")}</h3>
          <p className="text-sm text-slate-500">{t("description")}</p>
        </div>
        <span className={`inline-flex w-fit rounded-full px-2.5 py-1 text-xs font-medium ${
          status?.requestWindowOpen
            ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
            : "bg-slate-100 text-slate-600 border border-slate-200"
        }`}>
          {status?.requestWindowOpen ? t("windowOpen") : t("windowClosed")}
        </span>
      </div>

      {loading ? (
        <div className="mt-4 h-24 animate-pulse rounded-lg bg-slate-100" />
      ) : (
        <div className="mt-5 space-y-5">
          {hasCycle ? (
            <>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <Metric label={t("metrics.cycleStarts")} value={formatDateTime(status.startsAt)} />
                <Metric label={t("metrics.windowCloses")} value={formatDateTime(status.requestWindowClosesAt)} />
                <Metric label={t("metrics.slots")} value={`${status.slotCount}`} />
                <Metric label={t("metrics.filled")} value={`${status.filledCount}/${status.totalCapacity}`} />
                <Metric label={t("metrics.coverage")} value={formatCoverage(status)} />
                <Metric label={t("metrics.openSeats")} value={`${Math.max(status.totalCapacity - status.filledCount, 0)}`} />
                <Metric label={t("metrics.approved")} value={`${status.approvedCount}`} />
                <Metric label={t("metrics.pending")} value={`${status.pendingCount}`} />
                <Metric
                  label={t("metrics.waitlist")}
                  value={`${status.waitlistCount}`}
                  tone={status.waitlistCount > 0 ? "warning" : "default"}
                  actionLabel={t("openReviewQueue", { queue: t("metrics.waitlist") })}
                  onClick={onNavigate ? () => onNavigate("waitlist") : undefined}
                />
                <Metric
                  label={t("metrics.absenceReview")}
                  value={`${status.pendingAbsenceReportCount}`}
                  tone={status.pendingAbsenceReportCount > 0 ? "warning" : "default"}
                  actionLabel={t("openReviewQueue", { queue: t("metrics.absenceReview") })}
                  onClick={onNavigate ? () => onNavigate("absence-reports") : undefined}
                />
                <Metric
                  label={t("metrics.lateReports")}
                  value={`${status.latePendingAbsenceReportCount}`}
                  tone={status.latePendingAbsenceReportCount > 0 ? "danger" : "default"}
                  actionLabel={t("openReviewQueue", { queue: t("metrics.lateReports") })}
                  onClick={onNavigate ? () => onNavigate("absence-reports") : undefined}
                />
                <Metric
                  label={t("metrics.changeReview")}
                  value={`${status.pendingShiftChangeRequestCount}`}
                  tone={status.pendingShiftChangeRequestCount > 0 ? "warning" : "default"}
                  actionLabel={t("openReviewQueue", { queue: t("metrics.changeReview") })}
                  onClick={onNavigate ? () => onNavigate("absence-reports") : undefined}
                />
                <Metric
                  label={t("metrics.leaveReview")}
                  value={`${status.pendingSpecialLeaveRequestCount}`}
                  tone={status.pendingSpecialLeaveRequestCount > 0 ? "warning" : "default"}
                  actionLabel={t("openReviewQueue", { queue: t("metrics.leaveReview") })}
                  onClick={onNavigate ? () => onNavigate("absence-reports") : undefined}
                />
                <Metric label={t("metrics.generated")} value={status.isGenerated ? t("yes") : t("no")} />
              </div>

              {status.underfilledSlots.length > 0 && (
                <div className="rounded-lg border border-amber-200 bg-amber-50/70 p-3">
                  <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
                    <p className="text-sm font-semibold text-amber-900">{t("underfilledTitle")}</p>
                    <p className="text-xs text-amber-700">{t("underfilledCount", { count: status.underfilledSlots.length })}</p>
                  </div>
                  <div className="mt-3 grid gap-2 lg:grid-cols-2">
                    {status.underfilledSlots.map((slot) => (
                      <div
                        key={slot.shiftSlotId}
                        className="flex items-center justify-between gap-3 rounded-md border border-amber-200 bg-white px-3 py-2"
                      >
                        <div className="min-w-0">
                          <p className="truncate text-sm font-medium text-slate-900">{slot.taskName}</p>
                          <p className="text-xs text-slate-500">
                            {formatDate(slot.date)} | {slot.startTime}-{slot.endTime}
                          </p>
                        </div>
                        <div className="shrink-0 text-right">
                          <p className="text-sm font-semibold text-amber-800">{t("openSeatCount", { count: slot.openSeats })}</p>
                          <p className="text-xs text-slate-500">{slot.currentFillCount}/{slot.capacity}</p>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
                <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold text-slate-900">{t("closeChecklist.title")}</p>
                    <p className="text-xs text-slate-500">{t("closeChecklist.description")}</p>
                  </div>
                  <span className={`inline-flex w-fit rounded-full border px-2.5 py-1 text-xs font-medium ${
                    closeChecklistWarningCount > 0
                      ? "border-amber-200 bg-amber-50 text-amber-800"
                      : hasUnderScheduledResult
                        ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                        : "border-slate-200 bg-white text-slate-600"
                  }`}>
                    {closeChecklistWarningCount > 0
                      ? t("closeChecklist.warningCount", { count: closeChecklistWarningCount })
                      : hasUnderScheduledResult
                        ? t("closeChecklist.ready")
                        : t("closeChecklist.needsCheck")}
                  </span>
                </div>

                <div className="mt-3 grid gap-2 lg:grid-cols-2">
                  {closeChecklistItems.map((item) => (
                    <ChecklistItem
                      key={item.key}
                      label={item.label}
                      value={item.value}
                      state={item.state as "ok" | "warning" | "unknown"}
                      actionLabel={item.onClick ? t("closeChecklist.open") : undefined}
                      onClick={item.onClick}
                    />
                  ))}
                </div>
              </div>

              <div className="flex flex-wrap gap-2">
                <MutationButton
                  onClick={() => run("open", () => openSelfServiceCycleWindow(spaceId, groupId, status.cycleId!, 24))}
                  loading={action === "open"}
                  label={t("buttons.open")}
                  loadingLabel={t("buttons.opening")}
                  variant="secondary"
                  disabled={!!action}
                />
                <MutationButton
                  onClick={() => run("close", () => closeSelfServiceCycleWindow(spaceId, groupId, status.cycleId!))}
                  loading={action === "close"}
                  label={t("buttons.close")}
                  loadingLabel={t("buttons.closing")}
                  variant="secondary"
                  disabled={!!action}
                />
                <MutationButton
                  onClick={handleUnderScheduled}
                  loading={action === "under"}
                  label={t("buttons.checkUnder")}
                  loadingLabel={t("buttons.checking")}
                  variant="primary"
                  disabled={!!action}
                />
              </div>
            </>
          ) : (
            <div className="rounded-lg border border-dashed border-slate-300 bg-slate-50 p-4">
              <p className="text-sm text-slate-600">{t("noCycle")}</p>
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            <MutationButton
              onClick={() => run("generate", () => generateNextSelfServiceCycle(spaceId, groupId))}
              loading={action === "generate"}
              label={hasCycle ? t("buttons.generateNext") : t("buttons.generateFirst")}
              loadingLabel={t("buttons.generating")}
              variant="primary"
              disabled={!!action}
            />
            <MutationButton
              onClick={handleRefresh}
              loading={action === "refresh"}
              label={t("buttons.refresh")}
              loadingLabel={t("buttons.refreshing")}
              variant="secondary"
              disabled={!!action}
            />
          </div>

          {underScheduled && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
              <p className="text-sm font-medium text-amber-800">
                {underScheduled.length === 0
                  ? t("underScheduledNone")
                  : t("underScheduledCount", { count: underScheduled.length })}
              </p>
              {underScheduled.length > 0 && (
                <ul className="mt-2 space-y-1 text-sm text-amber-700">
                  {underScheduled.map((member) => (
                    <li key={member.personId}>
                      {member.personName}: {member.approvedCount}/{member.minRequired}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {error && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-2.5">
              <p className="text-sm text-red-600">{error}</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ChecklistItem({
  label,
  value,
  state,
  actionLabel,
  onClick,
}: {
  label: string;
  value: string;
  state: "ok" | "warning" | "unknown";
  actionLabel?: string;
  onClick?: () => void;
}) {
  const dotClass =
    state === "ok"
      ? "bg-emerald-500"
      : state === "warning"
        ? "bg-amber-500"
        : "bg-slate-400";

  const content = (
    <>
      <div className="flex items-start gap-2">
        <span className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${dotClass}`} />
        <div className="min-w-0">
          <p className="text-sm font-medium text-slate-900">{label}</p>
          <p className="mt-0.5 text-xs leading-5 text-slate-500">{value}</p>
        </div>
      </div>
      {actionLabel && (
        <span className="shrink-0 text-xs font-medium text-sky-700">{actionLabel}</span>
      )}
    </>
  );

  if (onClick) {
    return (
      <button
        type="button"
        onClick={onClick}
        className="flex items-center justify-between gap-3 rounded-md border border-slate-200 bg-white px-3 py-2 text-left transition-colors hover:border-sky-300 hover:bg-sky-50 focus:outline-none focus:ring-2 focus:ring-sky-400"
      >
        {content}
      </button>
    );
  }

  return (
    <div className="flex items-center justify-between gap-3 rounded-md border border-slate-200 bg-white px-3 py-2">
      {content}
    </div>
  );
}

function Metric({
  label,
  value,
  tone = "default",
  actionLabel,
  onClick,
}: {
  label: string;
  value: string;
  tone?: "default" | "warning" | "danger";
  actionLabel?: string;
  onClick?: () => void;
}) {
  const toneClass =
    tone === "danger"
      ? "border-red-200 bg-red-50 text-red-900"
      : tone === "warning"
        ? "border-amber-200 bg-amber-50 text-amber-900"
        : "border-slate-200 bg-slate-50 text-slate-900";

  const content = (
    <>
      <div className="text-xs text-slate-500">{label}</div>
      <div className="mt-1 text-sm font-semibold">{value}</div>
    </>
  );

  if (onClick) {
    return (
      <button
        type="button"
        onClick={onClick}
        aria-label={actionLabel}
        title={actionLabel}
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
