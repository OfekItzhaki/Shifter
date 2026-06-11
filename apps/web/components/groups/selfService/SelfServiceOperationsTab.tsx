"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  downloadSelfServiceCycleCloseoutCsv,
  getAdminWaitlistEntries,
  getSelfServiceConfig,
  getSelfServiceCycleCloseout,
  getSelfServiceCycleStatus,
  AdminWaitlistEntryDto,
  SelfServiceCycleCloseoutDto,
  SelfServiceConfigDto,
  SelfServiceCycleStatusDto,
} from "@/lib/api/selfService";
import CycleControlPanel from "./CycleControlPanel";

type SelfServiceOpsTarget =
  | "absence-reports"
  | "admin-overrides"
  | "shift-templates"
  | "self-service-config"
  | "waitlist"
  | "swaps";

interface SelfServiceOperationsTabProps {
  spaceId: string;
  groupId: string;
  onNavigate: (tab: SelfServiceOpsTarget) => void;
}

const ACTIONS: { target: SelfServiceOpsTarget; key: string; metric?: "reviews" | "waitlist" | "swaps" | "coverage" }[] = [
  { target: "absence-reports", key: "reviews", metric: "reviews" },
  { target: "waitlist", key: "waitlist", metric: "waitlist" },
  { target: "swaps", key: "swaps", metric: "swaps" },
  { target: "admin-overrides", key: "overrides", metric: "coverage" },
  { target: "shift-templates", key: "templates" },
  { target: "self-service-config", key: "policy" },
];

const GUIDE_STEPS = ["prepare", "open", "review", "improve"] as const;
const WORKFLOW_ITEMS = ["picking", "changes", "leave"] as const;
const CLOSEOUT_METRICS = [
  { key: "coverage", valueKey: "filledCount", totalKey: "totalCapacity" },
  { key: "underfilled", valueKey: "underfilledSlotCount", totalKey: undefined },
  { key: "pending", valueKey: "issueCount", totalKey: undefined },
  { key: "overrides", valueKey: "adminOverrideAssignments", totalKey: undefined },
  { key: "lateAbsences", valueKey: "lateAbsenceReports", totalKey: undefined },
  { key: "noShows", valueKey: "noShowAttendanceRecords", totalKey: undefined },
] satisfies readonly {
  key: string;
  valueKey: keyof SelfServiceCycleCloseoutDto;
  totalKey?: keyof SelfServiceCycleCloseoutDto;
}[];
const POLICY_WORKFLOWS = [
  { key: "claims", configKey: "allowMemberShiftClaims" },
  { key: "waitlist", configKey: "allowWaitlist" },
  { key: "changes", configKey: "allowShiftChangeRequests" },
  { key: "absence", configKey: "allowAbsenceReports" },
  { key: "swaps", configKey: "allowShiftSwaps" },
] as const satisfies readonly {
  key: string;
  configKey: keyof Pick<
    SelfServiceConfigDto,
    | "allowMemberShiftClaims"
    | "allowWaitlist"
    | "allowShiftChangeRequests"
    | "allowAbsenceReports"
    | "allowShiftSwaps"
  >;
}[];
const WAITLIST_EXPIRY_WARNING_MINUTES = 30;
const REVIEW_BREAKDOWN = [
  {
    key: "absences",
    countKey: "pendingAbsenceReportCount",
    target: "absence-reports",
  },
  {
    key: "changes",
    countKey: "pendingShiftChangeRequestCount",
    target: "absence-reports",
  },
  {
    key: "leave",
    countKey: "pendingSpecialLeaveRequestCount",
    target: "absence-reports",
  },
] as const satisfies readonly {
  key: string;
  countKey: keyof Pick<
    SelfServiceCycleStatusDto,
    "pendingAbsenceReportCount" | "pendingShiftChangeRequestCount" | "pendingSpecialLeaveRequestCount"
  >;
  target: SelfServiceOpsTarget;
}[];

function getPendingReviewCount(status: SelfServiceCycleStatusDto | null): number {
  if (!status) return 0;

  return status.pendingAbsenceReportCount
    + status.pendingShiftChangeRequestCount
    + status.pendingSpecialLeaveRequestCount;
}

function getActionCount(
  status: SelfServiceCycleStatusDto | null,
  metric: "reviews" | "waitlist" | "swaps" | "coverage" | undefined
): number | null {
  if (!status || !metric) return null;

  if (metric === "reviews") return getPendingReviewCount(status);
  if (metric === "waitlist") return status.waitlistCount;
  if (metric === "swaps") return status.pendingSwapRequestCount;
  return status.underfilledSlotCount;
}

function countExpiringWaitlistOffers(entries: AdminWaitlistEntryDto[]): number {
  const now = Date.now();
  const warningWindowMs = WAITLIST_EXPIRY_WARNING_MINUTES * 60 * 1000;

  return entries.filter((entry) => {
    if (entry.status !== "Offered" || !entry.expiresAt) return false;

    const expiresAt = new Date(entry.expiresAt).getTime();
    return Number.isFinite(expiresAt)
      && expiresAt > now
      && expiresAt - now <= warningWindowMs;
  }).length;
}

export default function SelfServiceOperationsTab({
  spaceId,
  groupId,
  onNavigate,
}: SelfServiceOperationsTabProps) {
  const t = useTranslations("selfService.operations");
  const [status, setStatus] = useState<SelfServiceCycleStatusDto | null>(null);
  const [closeout, setCloseout] = useState<SelfServiceCycleCloseoutDto | null>(null);
  const [config, setConfig] = useState<SelfServiceConfigDto | null>(null);
  const [waitlistEntries, setWaitlistEntries] = useState<AdminWaitlistEntryDto[]>([]);
  const [statusLoading, setStatusLoading] = useState(true);
  const [exportingCloseout, setExportingCloseout] = useState(false);
  const [closeoutExportError, setCloseoutExportError] = useState<string | null>(null);

  const loadStatus = useCallback(async () => {
    setStatusLoading(true);
    try {
      const [nextStatus, nextCloseout, nextConfig, nextWaitlistEntries] = await Promise.all([
        getSelfServiceCycleStatus(spaceId, groupId),
        getSelfServiceCycleCloseout(spaceId, groupId),
        getSelfServiceConfig(spaceId, groupId),
        getAdminWaitlistEntries(spaceId, groupId),
      ]);
      setStatus(nextStatus);
      setCloseout(nextCloseout);
      setConfig(nextConfig);
      setWaitlistEntries(nextWaitlistEntries);
    } catch {
      setStatus(null);
      setCloseout(null);
      setConfig(null);
      setWaitlistEntries([]);
    } finally {
      setStatusLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(loadStatus);
  }, [loadStatus]);

  async function handleExportCloseout() {
    setExportingCloseout(true);
    setCloseoutExportError(null);

    try {
      await downloadSelfServiceCycleCloseoutCsv(spaceId, groupId, closeout?.cycleId ?? null);
    } catch {
      setCloseoutExportError(t("closeout.exportError"));
    } finally {
      setExportingCloseout(false);
    }
  }

  const pendingReviewCount = getPendingReviewCount(status);
  const activeSignalCount = status
    ? pendingReviewCount + status.waitlistCount + status.pendingSwapRequestCount + status.underfilledSlotCount
    : 0;
  const expiringWaitlistOfferCount = countExpiringWaitlistOffers(waitlistEntries);
  const prioritySignals = status
    ? [
        {
          key: "lateReports",
          count: status.latePendingAbsenceReportCount,
          target: "absence-reports" as const,
          tone: "danger" as const,
        },
        {
          key: "expiringWaitlist",
          count: expiringWaitlistOfferCount,
          target: "waitlist" as const,
          tone: "warning" as const,
        },
        {
          key: "pendingSwaps",
          count: status.pendingSwapRequestCount,
          target: "swaps" as const,
          tone: "warning" as const,
        },
        {
          key: "underfilled",
          count: status.underfilledSlotCount,
          target: "admin-overrides" as const,
          tone: "warning" as const,
        },
      ].filter((signal) => signal.count > 0)
    : [];

  return (
    <div className="space-y-5">
      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="max-w-3xl">
            <h2 className="text-base font-semibold text-slate-900">{t("title")}</h2>
            <p className="mt-1 text-sm text-slate-500">{t("description")}</p>
          </div>
          <span className={`inline-flex w-fit rounded-full border px-2.5 py-1 text-xs font-medium ${
            activeSignalCount > 0
              ? "border-amber-200 bg-amber-50 text-amber-800"
              : "border-emerald-200 bg-emerald-50 text-emerald-700"
          }`}>
            {statusLoading
              ? t("statusLoading")
              : activeSignalCount > 0
                ? t("activeSignals", { count: activeSignalCount })
                : t("allClear")}
          </span>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {ACTIONS.map((action) => {
            const count = getActionCount(status, action.metric);
            const hasAttention = count !== null && count > 0;

            return (
              <button
                key={action.target}
                type="button"
                onClick={() => onNavigate(action.target)}
                className={`rounded-lg border px-4 py-3 text-left transition-colors focus:outline-none focus:ring-2 focus:ring-sky-400 ${
                  hasAttention
                    ? "border-amber-200 bg-amber-50 hover:border-amber-300 hover:bg-amber-100"
                    : "border-slate-200 bg-slate-50 hover:border-sky-200 hover:bg-sky-50"
                }`}
              >
                <span className="flex items-start justify-between gap-3">
                  <span className="text-sm font-semibold text-slate-900">
                    {t(`actions.${action.key}.title`)}
                  </span>
                  {count !== null && (
                    <span className={`shrink-0 rounded-full border px-2 py-0.5 text-xs font-medium ${
                      hasAttention
                        ? "border-amber-300 bg-white text-amber-800"
                        : "border-slate-200 bg-white text-slate-500"
                    }`}>
                      {statusLoading ? "-" : count}
                    </span>
                  )}
                </span>
                <span className="mt-1 block text-xs leading-5 text-slate-500">
                  {t(`actions.${action.key}.description`)}
                </span>
                {count !== null && (
                  <span className="mt-2 block text-xs font-medium text-slate-600">
                    {t(`actions.${action.key}.metric`, { count: statusLoading ? 0 : count })}
                  </span>
                )}
              </button>
            );
          })}
        </div>

        <div className="mt-5 rounded-xl border border-slate-200 bg-slate-50 p-4">
          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">{t("policy.title")}</h3>
              <p className="text-xs leading-5 text-slate-500">{t("policy.description")}</p>
            </div>
            <button
              type="button"
              onClick={() => onNavigate("self-service-config")}
              className="mt-2 inline-flex w-fit rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-sky-700 transition hover:border-sky-200 hover:bg-sky-50 focus:outline-none focus:ring-2 focus:ring-sky-400 sm:mt-0"
            >
              {t("policy.edit")}
            </button>
          </div>

          <div className="mt-3 grid gap-2 md:grid-cols-2 xl:grid-cols-5">
            {POLICY_WORKFLOWS.map((workflow) => {
              const enabled = config ? config[workflow.configKey] : null;
              return (
                <div
                  key={workflow.key}
                  className={`rounded-lg border px-3 py-2 ${
                    enabled === false
                      ? "border-slate-300 bg-white text-slate-500"
                      : "border-emerald-200 bg-white text-emerald-800"
                  }`}
                >
                  <p className="text-xs font-semibold text-slate-700">
                    {t(`policy.workflows.${workflow.key}`)}
                  </p>
                  <p className="mt-1 text-xs font-medium">
                    {statusLoading || enabled === null
                      ? t("policy.loading")
                      : enabled
                        ? t("policy.enabled")
                        : t("policy.disabled")}
                  </p>
                </div>
              );
            })}
          </div>

          <div className="mt-3 grid gap-2 md:grid-cols-3">
            <PolicyMetric label={t("policy.metrics.shiftLimit")} value={
              config ? t("policy.metrics.shiftLimitValue", {
                min: config.minShiftsPerCycle,
                max: config.maxShiftsPerCycle,
              }) : t("policy.loading")
            } />
            <PolicyMetric label={t("policy.metrics.absenceLimit")} value={
              config ? t("policy.metrics.absenceLimitValue", {
                max: config.maxLateCancellationsPerCycle,
                hours: config.lateCancellationWindowHours,
              }) : t("policy.loading")
            } />
            <PolicyMetric label={t("policy.metrics.cutoff")} value={
              config ? t("policy.metrics.cutoffValue", {
                hours: config.cancellationCutoffHours,
                minutes: config.waitlistOfferMinutes,
              }) : t("policy.loading")
            } />
          </div>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">{t("closeout.title")}</h3>
            <p className="text-sm text-slate-500">{t("closeout.description")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={handleExportCloseout}
              disabled={statusLoading || !closeout || exportingCloseout}
              className="inline-flex rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-sky-700 transition hover:border-sky-200 hover:bg-sky-50 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {exportingCloseout ? t("closeout.exporting") : t("closeout.exportCsv")}
            </button>
            <span className={`inline-flex w-fit rounded-full border px-2.5 py-1 text-xs font-medium ${
              statusLoading
                ? "border-slate-200 bg-slate-50 text-slate-500"
                : closeout && closeout.issueCount > 0
                  ? "border-amber-200 bg-amber-50 text-amber-800"
                  : "border-emerald-200 bg-emerald-50 text-emerald-700"
            }`}>
              {statusLoading
                ? t("statusLoading")
                : closeout && closeout.issueCount > 0
                  ? t("closeout.needsReview", { count: closeout.issueCount })
                  : t("closeout.ready")}
            </span>
          </div>
        </div>

        {closeoutExportError && (
          <p className="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
            {closeoutExportError}
          </p>
        )}

        <div className="mt-4 grid gap-3 md:grid-cols-3 xl:grid-cols-6">
          {CLOSEOUT_METRICS.map((metric) => {
            const value = closeout ? Number(closeout[metric.valueKey]) : 0;
            const total = closeout && metric.totalKey ? Number(closeout[metric.totalKey]) : null;
            return (
              <div key={metric.key} className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3">
                <p className="text-xs font-medium text-slate-500">{t(`closeout.metrics.${metric.key}`)}</p>
                <p className="mt-1 text-lg font-semibold text-slate-900">
                  {statusLoading ? "-" : total !== null ? `${value}/${total}` : value}
                </p>
              </div>
            );
          })}
        </div>

        <div className="mt-4 grid gap-3 lg:grid-cols-4">
          <CloseoutDetail
            label={t("closeout.details.assignments")}
            value={statusLoading || !closeout
              ? "-"
              : t("closeout.details.assignmentsValue", {
                approved: closeout.approvedAssignments,
                cancelled: closeout.cancelledAssignments,
                rejected: closeout.rejectedRequests,
              })}
          />
          <CloseoutDetail
            label={t("closeout.details.absences")}
            value={statusLoading || !closeout
              ? "-"
              : t("closeout.details.absencesValue", {
                approved: closeout.approvedAbsenceReports,
                rejected: closeout.rejectedAbsenceReports,
                pending: closeout.pendingAbsenceReports,
              })}
          />
          <CloseoutDetail
            label={t("closeout.details.attendance")}
            value={statusLoading || !closeout
              ? "-"
              : t("closeout.details.attendanceValue", {
                present: closeout.presentAttendanceRecords,
                noshow: closeout.noShowAttendanceRecords,
                unconfirmed: closeout.unconfirmedAttendanceCount,
              })}
          />
          <CloseoutDetail
            label={t("closeout.details.changes")}
            value={statusLoading || !closeout
              ? "-"
              : t("closeout.details.changesValue", {
                approved: closeout.approvedChangeRequests,
                rejected: closeout.rejectedChangeRequests,
                pending: closeout.pendingChangeRequests,
              })}
          />
          <CloseoutDetail
            label={t("closeout.details.waitlist")}
            value={statusLoading || !closeout
              ? "-"
              : t("closeout.details.waitlistValue", {
                active: closeout.activeWaitlistEntries,
                accepted: closeout.acceptedWaitlistEntries,
                expired: closeout.expiredWaitlistEntries,
              })}
          />
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">{t("reviews.title")}</h3>
            <p className="text-sm text-slate-500">{t("reviews.description")}</p>
          </div>
          <span className={`inline-flex w-fit rounded-full border px-2.5 py-1 text-xs font-medium ${
            statusLoading
              ? "border-slate-200 bg-slate-50 text-slate-500"
              : pendingReviewCount > 0
                ? "border-amber-200 bg-amber-50 text-amber-800"
                : "border-emerald-200 bg-emerald-50 text-emerald-700"
          }`}>
            {statusLoading
              ? t("statusLoading")
              : pendingReviewCount > 0
                ? t("reviews.count", { count: pendingReviewCount })
                : t("reviews.clear")}
          </span>
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-3">
          {REVIEW_BREAKDOWN.map((item) => {
            const count = statusLoading || !status ? 0 : status[item.countKey];
            const hasPending = count > 0;

            return (
              <button
                key={item.key}
                type="button"
                onClick={() => onNavigate(item.target)}
                className={`rounded-lg border px-4 py-3 text-left transition-colors focus:outline-none focus:ring-2 focus:ring-sky-400 ${
                  hasPending
                    ? "border-amber-200 bg-amber-50 hover:border-amber-300 hover:bg-amber-100"
                    : "border-slate-200 bg-slate-50 hover:border-sky-200 hover:bg-sky-50"
                }`}
              >
                <span className="flex items-start justify-between gap-3">
                  <span className="text-sm font-semibold text-slate-900">
                    {t(`reviews.items.${item.key}.title`)}
                  </span>
                  <span className={`shrink-0 rounded-full border bg-white px-2 py-0.5 text-xs font-medium ${
                    hasPending
                      ? "border-amber-300 text-amber-800"
                      : "border-slate-200 text-slate-500"
                  }`}>
                    {statusLoading ? "-" : count}
                  </span>
                </span>
                <span className="mt-1 block text-xs leading-5 text-slate-600">
                  {t(`reviews.items.${item.key}.description`, { count })}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">{t("priority.title")}</h3>
            <p className="text-sm text-slate-500">{t("priority.description")}</p>
          </div>
          <span className={`inline-flex w-fit rounded-full border px-2.5 py-1 text-xs font-medium ${
            statusLoading
              ? "border-slate-200 bg-slate-50 text-slate-500"
              : prioritySignals.length > 0
                ? "border-red-200 bg-red-50 text-red-700"
                : "border-emerald-200 bg-emerald-50 text-emerald-700"
          }`}>
            {statusLoading
              ? t("statusLoading")
              : prioritySignals.length > 0
                ? t("priority.count", { count: prioritySignals.length })
                : t("priority.clear")}
          </span>
        </div>

        {statusLoading ? (
          <div className="mt-4 h-20 animate-pulse rounded-lg bg-slate-100" />
        ) : prioritySignals.length === 0 ? (
          <p className="mt-4 rounded-lg border border-dashed border-slate-200 bg-slate-50 px-3 py-4 text-center text-sm text-slate-500">
            {t("priority.empty")}
          </p>
        ) : (
          <div className="mt-4 grid gap-3 lg:grid-cols-3">
            {prioritySignals.map((signal) => (
              <button
                key={signal.key}
                type="button"
                onClick={() => onNavigate(signal.target)}
                className={`rounded-lg border px-4 py-3 text-left transition-colors focus:outline-none focus:ring-2 focus:ring-sky-400 ${
                  signal.tone === "danger"
                    ? "border-red-200 bg-red-50 hover:border-red-300 hover:bg-red-100"
                    : "border-amber-200 bg-amber-50 hover:border-amber-300 hover:bg-amber-100"
                }`}
              >
                <span className="flex items-start justify-between gap-3">
                  <span className="text-sm font-semibold text-slate-900">
                    {t(`priority.signals.${signal.key}.title`)}
                  </span>
                  <span className={`shrink-0 rounded-full border bg-white px-2 py-0.5 text-xs font-medium ${
                    signal.tone === "danger"
                      ? "border-red-200 text-red-700"
                      : "border-amber-300 text-amber-800"
                  }`}>
                    {signal.count}
                  </span>
                </span>
                <span className="mt-1 block text-xs leading-5 text-slate-600">
                  {t(`priority.signals.${signal.key}.description`, {
                    count: signal.count,
                    minutes: WAITLIST_EXPIRY_WARNING_MINUTES,
                  })}
                </span>
                <span className="mt-2 block text-xs font-medium text-sky-700">
                  {t("priority.open")}
                </span>
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="max-w-3xl">
          <h3 className="text-sm font-semibold text-slate-900">{t("guide.title")}</h3>
          <p className="mt-1 text-sm text-slate-500">{t("guide.description")}</p>
        </div>
        <ol className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {GUIDE_STEPS.map((step, index) => (
            <li key={step} className="rounded-lg border border-slate-200 bg-slate-50 p-4">
              <div className="flex items-center gap-2">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-sky-100 text-xs font-semibold text-sky-700">
                  {index + 1}
                </span>
                <span className="text-sm font-semibold text-slate-900">{t(`guide.steps.${step}.title`)}</span>
              </div>
              <p className="mt-2 text-xs leading-5 text-slate-500">{t(`guide.steps.${step}.description`)}</p>
            </li>
          ))}
        </ol>
        <div className="mt-5 grid gap-3 lg:grid-cols-3">
          {WORKFLOW_ITEMS.map((item) => (
            <div key={item} className="rounded-lg border border-slate-200 bg-white p-4">
              <h4 className="text-sm font-semibold text-slate-900">
                {t(`guide.workflows.${item}.title`)}
              </h4>
              <dl className="mt-3 space-y-2 text-xs leading-5">
                <div>
                  <dt className="font-medium text-slate-500">{t("guide.workflows.member")}</dt>
                  <dd className="text-slate-700">{t(`guide.workflows.${item}.member`)}</dd>
                </div>
                <div>
                  <dt className="font-medium text-slate-500">{t("guide.workflows.admin")}</dt>
                  <dd className="text-slate-700">{t(`guide.workflows.${item}.admin`)}</dd>
                </div>
                <div>
                  <dt className="font-medium text-slate-500">{t("guide.workflows.result")}</dt>
                  <dd className="text-slate-700">{t(`guide.workflows.${item}.result`)}</dd>
                </div>
              </dl>
            </div>
          ))}
        </div>
      </div>

      <CycleControlPanel
        spaceId={spaceId}
        groupId={groupId}
        onNavigate={onNavigate}
        onStatusChanged={loadStatus}
      />
    </div>
  );
}

function PolicyMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2">
      <p className="text-xs font-medium text-slate-500">{label}</p>
      <p className="mt-1 text-sm font-semibold text-slate-900">{value}</p>
    </div>
  );
}

function CloseoutDetail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2">
      <p className="text-xs font-medium text-slate-500">{label}</p>
      <p className="mt-1 text-xs font-semibold leading-5 text-slate-900">{value}</p>
    </div>
  );
}
