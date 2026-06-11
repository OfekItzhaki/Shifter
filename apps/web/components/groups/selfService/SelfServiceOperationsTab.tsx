"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  getAdminWaitlistEntries,
  getSelfServiceCycleStatus,
  AdminWaitlistEntryDto,
  SelfServiceCycleStatusDto,
} from "@/lib/api/selfService";
import CycleControlPanel from "./CycleControlPanel";

type SelfServiceOpsTarget =
  | "absence-reports"
  | "admin-overrides"
  | "shift-templates"
  | "self-service-config"
  | "waitlist";

interface SelfServiceOperationsTabProps {
  spaceId: string;
  groupId: string;
  onNavigate: (tab: SelfServiceOpsTarget) => void;
}

const ACTIONS: { target: SelfServiceOpsTarget; key: string; metric?: "reviews" | "waitlist" | "coverage" }[] = [
  { target: "absence-reports", key: "reviews", metric: "reviews" },
  { target: "waitlist", key: "waitlist", metric: "waitlist" },
  { target: "admin-overrides", key: "overrides", metric: "coverage" },
  { target: "shift-templates", key: "templates" },
  { target: "self-service-config", key: "policy" },
];

const GUIDE_STEPS = ["prepare", "open", "review", "improve"] as const;
const WORKFLOW_ITEMS = ["picking", "changes", "leave"] as const;
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
  metric: "reviews" | "waitlist" | "coverage" | undefined
): number | null {
  if (!status || !metric) return null;

  if (metric === "reviews") return getPendingReviewCount(status);
  if (metric === "waitlist") return status.waitlistCount;
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
  const [waitlistEntries, setWaitlistEntries] = useState<AdminWaitlistEntryDto[]>([]);
  const [statusLoading, setStatusLoading] = useState(true);

  const loadStatus = useCallback(async () => {
    setStatusLoading(true);
    try {
      const [nextStatus, nextWaitlistEntries] = await Promise.all([
        getSelfServiceCycleStatus(spaceId, groupId),
        getAdminWaitlistEntries(spaceId, groupId),
      ]);
      setStatus(nextStatus);
      setWaitlistEntries(nextWaitlistEntries);
    } catch {
      setStatus(null);
      setWaitlistEntries([]);
    } finally {
      setStatusLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(loadStatus);
  }, [loadStatus]);

  const pendingReviewCount = getPendingReviewCount(status);
  const activeSignalCount = status
    ? pendingReviewCount + status.waitlistCount + status.underfilledSlotCount
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
