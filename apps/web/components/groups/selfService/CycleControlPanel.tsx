"use client";

import { useCallback, useEffect, useState } from "react";
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

export default function CycleControlPanel({ spaceId, groupId }: CycleControlPanelProps) {
  const [status, setStatus] = useState<SelfServiceCycleStatusDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [action, setAction] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [underScheduled, setUnderScheduled] = useState<UnderScheduledMemberDto[] | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setStatus(await getSelfServiceCycleStatus(spaceId, groupId));
    } catch (err) {
      setError(getSelfServiceErrorMessage(err).message);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(load);
  }, [load]);

  async function run(name: string, fn: () => Promise<SelfServiceCycleStatusDto | void>) {
    setAction(name);
    setError(null);
    setUnderScheduled(null);
    try {
      const next = await fn();
      if (next) setStatus(next);
      else await load();
    } catch (err) {
      setError(getSelfServiceErrorMessage(err).message);
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
    } catch (err) {
      setError(getSelfServiceErrorMessage(err).message);
    } finally {
      setAction(null);
    }
  }

  const hasCycle = !!status?.cycleId;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">Cycle controls</h3>
          <p className="text-sm text-slate-500">Generate slots, manage the request window, and check shift coverage.</p>
        </div>
        <span className={`inline-flex w-fit rounded-full px-2.5 py-1 text-xs font-medium ${
          status?.requestWindowOpen
            ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
            : "bg-slate-100 text-slate-600 border border-slate-200"
        }`}>
          {status?.requestWindowOpen ? "Window open" : "Window closed"}
        </span>
      </div>

      {loading ? (
        <div className="mt-4 h-24 animate-pulse rounded-lg bg-slate-100" />
      ) : (
        <div className="mt-5 space-y-5">
          {hasCycle ? (
            <>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <Metric label="Cycle starts" value={formatDateTime(status.startsAt)} />
                <Metric label="Window closes" value={formatDateTime(status.requestWindowClosesAt)} />
                <Metric label="Slots" value={`${status.slotCount}`} />
                <Metric label="Filled" value={`${status.filledCount}/${status.totalCapacity}`} />
                <Metric label="Coverage" value={formatCoverage(status)} />
                <Metric label="Open seats" value={`${Math.max(status.totalCapacity - status.filledCount, 0)}`} />
                <Metric label="Approved" value={`${status.approvedCount}`} />
                <Metric label="Pending" value={`${status.pendingCount}`} />
                <Metric label="Waitlist" value={`${status.waitlistCount}`} />
                <Metric label="Absence review" value={`${status.pendingAbsenceReportCount}`} tone={status.pendingAbsenceReportCount > 0 ? "warning" : "default"} />
                <Metric label="Late reports" value={`${status.latePendingAbsenceReportCount}`} tone={status.latePendingAbsenceReportCount > 0 ? "danger" : "default"} />
                <Metric label="Change review" value={`${status.pendingShiftChangeRequestCount}`} tone={status.pendingShiftChangeRequestCount > 0 ? "warning" : "default"} />
                <Metric label="Generated" value={status.isGenerated ? "Yes" : "No"} />
              </div>

              {status.underfilledSlots.length > 0 && (
                <div className="rounded-lg border border-amber-200 bg-amber-50/70 p-3">
                  <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
                    <p className="text-sm font-semibold text-amber-900">Under-filled slots</p>
                    <p className="text-xs text-amber-700">Showing the next {status.underfilledSlots.length} gaps</p>
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
                          <p className="text-sm font-semibold text-amber-800">{slot.openSeats} open</p>
                          <p className="text-xs text-slate-500">{slot.currentFillCount}/{slot.capacity}</p>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              <div className="flex flex-wrap gap-2">
                <MutationButton
                  onClick={() => run("open", () => openSelfServiceCycleWindow(spaceId, groupId, status.cycleId!, 24))}
                  loading={action === "open"}
                  label="Open 24h"
                  loadingLabel="Opening..."
                  variant="secondary"
                  disabled={!!action}
                />
                <MutationButton
                  onClick={() => run("close", () => closeSelfServiceCycleWindow(spaceId, groupId, status.cycleId!))}
                  loading={action === "close"}
                  label="Close window"
                  loadingLabel="Closing..."
                  variant="secondary"
                  disabled={!!action}
                />
                <MutationButton
                  onClick={handleUnderScheduled}
                  loading={action === "under"}
                  label="Check under-scheduled"
                  loadingLabel="Checking..."
                  variant="primary"
                  disabled={!!action}
                />
              </div>
            </>
          ) : (
            <div className="rounded-lg border border-dashed border-slate-300 bg-slate-50 p-4">
              <p className="text-sm text-slate-600">No upcoming self-service cycle exists yet.</p>
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            <MutationButton
              onClick={() => run("generate", () => generateNextSelfServiceCycle(spaceId, groupId))}
              loading={action === "generate"}
              label={hasCycle ? "Generate next cycle" : "Generate first cycle"}
              loadingLabel="Generating..."
              variant="primary"
              disabled={!!action}
            />
            <MutationButton
              onClick={load}
              loading={action === "refresh"}
              label="Refresh"
              loadingLabel="Refreshing..."
              variant="secondary"
              disabled={!!action}
            />
          </div>

          {underScheduled && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
              <p className="text-sm font-medium text-amber-800">
                {underScheduled.length === 0
                  ? "Everyone meets the minimum shift requirement."
                  : `${underScheduled.length} member(s) are below the minimum.`}
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

function Metric({
  label,
  value,
  tone = "default",
}: {
  label: string;
  value: string;
  tone?: "default" | "warning" | "danger";
}) {
  const toneClass =
    tone === "danger"
      ? "border-red-200 bg-red-50 text-red-900"
      : tone === "warning"
        ? "border-amber-200 bg-amber-50 text-amber-900"
        : "border-slate-200 bg-slate-50 text-slate-900";

  return (
    <div className={`rounded-lg border px-3 py-2 ${toneClass}`}>
      <div className="text-xs text-slate-500">{label}</div>
      <div className="mt-1 text-sm font-semibold">{value}</div>
    </div>
  );
}
