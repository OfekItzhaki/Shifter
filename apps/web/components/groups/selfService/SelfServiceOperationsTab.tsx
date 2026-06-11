"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  getSelfServiceCycleStatus,
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
  return status.underfilledSlots.length;
}

export default function SelfServiceOperationsTab({
  spaceId,
  groupId,
  onNavigate,
}: SelfServiceOperationsTabProps) {
  const t = useTranslations("selfService.operations");
  const [status, setStatus] = useState<SelfServiceCycleStatusDto | null>(null);
  const [statusLoading, setStatusLoading] = useState(true);

  const loadStatus = useCallback(async () => {
    setStatusLoading(true);
    try {
      setStatus(await getSelfServiceCycleStatus(spaceId, groupId));
    } catch {
      setStatus(null);
    } finally {
      setStatusLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(loadStatus);
  }, [loadStatus]);

  const pendingReviewCount = getPendingReviewCount(status);
  const activeSignalCount = status
    ? pendingReviewCount + status.waitlistCount + status.underfilledSlots.length
    : 0;

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

      <CycleControlPanel spaceId={spaceId} groupId={groupId} onNavigate={onNavigate} />
    </div>
  );
}
