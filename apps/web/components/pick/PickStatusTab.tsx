"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import {
  getMyShiftChangeRequests,
  getMyShiftRequests,
  getMySwaps,
  getMyWaitlistEntries,
  MyShiftsResponse,
  ShiftChangeRequestDto,
  SwapRequestDto,
  WaitlistEntryDto,
} from "@/lib/api/selfService";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import { formatSlotDate, formatTime24h } from "@/lib/utils/selfServiceFormat";
import LoadingCard from "@/components/groups/selfService/LoadingCard";
import ErrorRetry from "@/components/groups/selfService/ErrorRetry";
import type { PickerTab } from "./PickerTabs";

interface PickStatusTabProps {
  spaceId: string;
  groupId: string;
  onNavigate: (tab: PickerTab) => void;
}

interface StatusData {
  shifts: MyShiftsResponse;
  waitlist: WaitlistEntryDto[];
  swaps: SwapRequestDto[];
  changes: ShiftChangeRequestDto[];
}

export default function PickStatusTab({ spaceId, groupId, onNavigate }: PickStatusTabProps) {
  const t = useTranslations("pick.status");
  const [data, setData] = useState<StatusData | null>(null);
  const [nowMs, setNowMs] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const [shifts, waitlist, swaps, changes] = await Promise.all([
        getMyShiftRequests(spaceId, groupId),
        getMyWaitlistEntries(spaceId, groupId),
        getMySwaps(spaceId, groupId),
        getMyShiftChangeRequests(spaceId, groupId),
      ]);
      setNowMs(Date.now());
      setData({ shifts, waitlist, swaps, changes });
    } catch (err) {
      setError(getSelfServiceErrorMessage(err).message);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(load);
  }, [load]);

  const summary = useMemo(() => {
    if (!data) return null;

    const approved = data.shifts.requests.filter((request) => request.status === "Approved");
    const pendingShifts = data.shifts.requests.filter((request) => request.status === "Pending");
    const offeredWaitlist = data.waitlist.filter((entry) => entry.status === "Offered");
    const waitingWaitlist = data.waitlist.filter((entry) => entry.status === "Waiting");
    const pendingSwaps = data.swaps.filter((swap) => swap.status === "Pending");
    const pendingChanges = data.changes.filter((change) => change.status === "Pending");
    const nextShift = approved
      .filter((request) => {
        const startsAt = new Date(`${request.slotDate}T${request.slotStartTime}`);
        return !Number.isNaN(startsAt.getTime()) && startsAt.getTime() > nowMs;
      })
      .sort((a, b) => `${a.slotDate}T${a.slotStartTime}`.localeCompare(`${b.slotDate}T${b.slotStartTime}`))[0] ?? null;

    return {
      approved,
      pendingShifts,
      offeredWaitlist,
      waitingWaitlist,
      pendingSwaps,
      pendingChanges,
      nextShift,
    };
  }, [data, nowMs]);

  if (loading) return <LoadingCard rows={4} variant="list" />;
  if (error) return <ErrorRetry message={error} onRetry={load} />;
  if (!data || !summary) return null;

  const isUnderMinimum = data.shifts.currentShiftCount < data.shifts.minShiftsPerCycle;
  const openSlots = Math.max(data.shifts.maxShiftsPerCycle - data.shifts.currentShiftCount, 0);

  return (
    <div className="space-y-4">
      <section className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-base font-semibold text-slate-900">{t("title")}</h2>
            <p className="mt-1 text-sm text-slate-500">
              {t("shiftProgress", {
                current: data.shifts.currentShiftCount,
                min: data.shifts.minShiftsPerCycle,
                max: data.shifts.maxShiftsPerCycle,
              })}
            </p>
          </div>
          <span className={`rounded-full border px-2.5 py-1 text-xs font-medium ${
            isUnderMinimum
              ? "border-amber-200 bg-amber-50 text-amber-700"
              : "border-emerald-200 bg-emerald-50 text-emerald-700"
          }`}>
            {isUnderMinimum ? t("belowMinimum") : t("onTrack")}
          </span>
        </div>

        <div className="mt-4 grid grid-cols-2 gap-2">
          <Metric label={t("approved")} value={`${summary.approved.length}`} />
          <Metric label={t("openSlots")} value={`${openSlots}`} />
          <Metric label={t("pendingRequests")} value={`${summary.pendingShifts.length}`} tone={summary.pendingShifts.length > 0 ? "warning" : "default"} />
          <Metric label={t("activeWaitlist")} value={`${summary.waitingWaitlist.length + summary.offeredWaitlist.length}`} tone={summary.offeredWaitlist.length > 0 ? "warning" : "default"} />
        </div>

        {summary.nextShift ? (
          <div className="mt-4 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
            <p className="text-xs font-medium text-slate-500">{t("nextShift")}</p>
            <p className="mt-1 text-sm font-semibold text-slate-900">
              {formatSlotDate(summary.nextShift.slotDate)} | {formatTime24h(summary.nextShift.slotStartTime)}-{formatTime24h(summary.nextShift.slotEndTime)}
            </p>
            <p className="text-xs text-slate-500">{summary.nextShift.taskName}</p>
          </div>
        ) : (
          <p className="mt-4 rounded-lg border border-dashed border-slate-200 bg-slate-50 px-3 py-3 text-sm text-slate-500">
            {t("noUpcomingShift")}
          </p>
        )}
      </section>

      <div className="grid gap-3">
        <ActionCard
          title={t("cards.pick.title")}
          description={isUnderMinimum ? t("cards.pick.underMinimum") : t("cards.pick.description")}
          count={openSlots}
          button={t("cards.pick.button")}
          onClick={() => onNavigate("slots")}
        />
        <ActionCard
          title={t("cards.waitlist.title")}
          description={summary.offeredWaitlist.length > 0 ? t("cards.waitlist.offer") : t("cards.waitlist.description")}
          count={summary.waitingWaitlist.length + summary.offeredWaitlist.length}
          tone={summary.offeredWaitlist.length > 0 ? "warning" : "default"}
          button={t("cards.waitlist.button")}
          onClick={() => onNavigate("waitlist")}
        />
        <ActionCard
          title={t("cards.swaps.title")}
          description={t("cards.swaps.description")}
          count={summary.pendingSwaps.length}
          tone={summary.pendingSwaps.length > 0 ? "warning" : "default"}
          button={t("cards.swaps.button")}
          onClick={() => onNavigate("swaps")}
        />
        <ActionCard
          title={t("cards.requests.title")}
          description={t("cards.requests.description", { count: summary.pendingChanges.length })}
          count={summary.pendingShifts.length + summary.pendingChanges.length}
          tone={summary.pendingChanges.length > 0 ? "warning" : "default"}
          button={t("cards.requests.button")}
          onClick={() => onNavigate("my-shifts")}
        />
      </div>
    </div>
  );
}

function Metric({
  label,
  value,
  tone = "default",
}: {
  label: string;
  value: string;
  tone?: "default" | "warning";
}) {
  const toneClass = tone === "warning"
    ? "border-amber-200 bg-amber-50 text-amber-900"
    : "border-slate-200 bg-slate-50 text-slate-900";

  return (
    <div className={`rounded-lg border px-3 py-2 ${toneClass}`}>
      <p className="text-xs text-slate-500">{label}</p>
      <p className="mt-1 text-lg font-semibold">{value}</p>
    </div>
  );
}

function ActionCard({
  title,
  description,
  count,
  button,
  onClick,
  tone = "default",
}: {
  title: string;
  description: string;
  count: number;
  button: string;
  onClick: () => void;
  tone?: "default" | "warning";
}) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="text-sm font-semibold text-slate-900">{title}</h3>
          <p className="mt-1 text-xs leading-5 text-slate-500">{description}</p>
        </div>
        <span className={`shrink-0 rounded-full border px-2.5 py-1 text-xs font-semibold ${
          tone === "warning"
            ? "border-amber-200 bg-amber-50 text-amber-700"
            : "border-slate-200 bg-slate-50 text-slate-600"
        }`}>
          {count}
        </span>
      </div>
      <button
        type="button"
        onClick={onClick}
        className="mt-3 min-h-[40px] w-full rounded-lg border border-sky-200 bg-sky-50 px-3 py-2 text-sm font-medium text-sky-700 transition-colors hover:bg-sky-100 focus:outline-none focus:ring-2 focus:ring-sky-400"
      >
        {button}
      </button>
    </div>
  );
}
