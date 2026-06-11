"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import {
  getMySwaps,
  proposeSwap,
  acceptSwap,
  declineSwap,
  cancelSwap,
  getAdminSwaps,
  getMyShiftRequests,
  getMemberApprovedShifts,
  SwapRequestDto,
  ShiftRequestDto,
} from "@/lib/api/selfService";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import { formatSlotDate, formatTime24h, formatCountdown } from "@/lib/utils/selfServiceFormat";
import { classifySwapsForPerson } from "@/lib/utils/selfServiceSwaps";
import type { GroupMemberDto } from "@/lib/api/groups";
import { LoadingCard, ErrorRetry, MutationButton } from "@/components/groups/selfService";

interface Props {
  spaceId: string;
  groupId: string;
  members: GroupMemberDto[];
  isAdmin?: boolean;
  onSwapsChanged?: () => void | Promise<void>;
}

type ProposeStep = "idle" | "selectMyShift" | "selectTargetMember" | "selectTargetShift";

function getShiftStartTime(request: ShiftRequestDto): number {
  return new Date(`${request.slotDate}T${request.slotStartTime}`).getTime();
}

function isFutureApprovedShift(request: ShiftRequestDto): boolean {
  const startsAt = getShiftStartTime(request);
  return request.status === "Approved" && Number.isFinite(startsAt) && startsAt > Date.now();
}

function sortShiftsByStart(requests: ShiftRequestDto[]): ShiftRequestDto[] {
  return [...requests].sort((a, b) => getShiftStartTime(a) - getShiftStartTime(b));
}

function formatSlotTimeRange(value: string): string {
  const [start, end] = value.split("-");
  if (!start || !end) return formatTime24h(value);

  return `${formatTime24h(start)}-${formatTime24h(end)}`;
}

/**
 * SwapsTab displays the member's swap requests (incoming and outgoing)
 * and provides a "Propose Swap" flow.
 *
 * Validates: Requirements 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10
 */
export default function SwapsTab({
  spaceId,
  groupId,
  members,
  isAdmin = false,
  onSwapsChanged,
}: Props) {
  const t = useTranslations("selfService.swaps");
  const tCommon = useTranslations("selfService");
  const { userId } = useAuthStore();
  const storedSpaceId = useSpaceStore((s) => s.currentSpaceId);
  const currentSpaceId = spaceId || storedSpaceId;

  const [swaps, setSwaps] = useState<SwapRequestDto[]>([]);
  const [adminSwaps, setAdminSwaps] = useState<SwapRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Action loading states
  const [actionLoading, setActionLoading] = useState<Record<string, string>>({});
  const [actionError, setActionError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Propose swap flow
  const [proposeStep, setProposeStep] = useState<ProposeStep>("idle");
  const [myShifts, setMyShifts] = useState<ShiftRequestDto[]>([]);
  const [myShiftsLoading, setMyShiftsLoading] = useState(false);
  const [selectedMyShift, setSelectedMyShift] = useState<ShiftRequestDto | null>(null);
  const [selectedTargetMember, setSelectedTargetMember] = useState<GroupMemberDto | null>(null);
  const [targetShifts, setTargetShifts] = useState<ShiftRequestDto[]>([]);
  const [targetShiftsLoading, setTargetShiftsLoading] = useState(false);
  const [proposing, setProposing] = useState(false);
  const [proposeError, setProposeError] = useState<string | null>(null);

  const fetchSwaps = useCallback(async (showLoading = true) => {
    if (!currentSpaceId || !groupId) return;
    if (showLoading) setLoading(true);
    setError(null);
    try {
      const [myData, adminData] = await Promise.all([
        getMySwaps(currentSpaceId, groupId),
        isAdmin ? getAdminSwaps(currentSpaceId, groupId, "Pending", 50) : Promise.resolve([]),
      ]);
      setSwaps(myData);
      setAdminSwaps(adminData);
    } catch {
      setError(tCommon("error"));
    } finally {
      if (showLoading) setLoading(false);
    }
  }, [currentSpaceId, groupId, isAdmin, tCommon]);

  useEffect(() => {
    void Promise.resolve().then(() => fetchSwaps());
  }, [fetchSwaps]);

  const currentPersonId = members.find((m) => m.linkedUserId === userId)?.personId ?? null;

  const { incomingSwaps, outgoingSwaps, completedSwaps } = classifySwapsForPerson(
    swaps,
    currentPersonId
  );
  const swappableMembers = members.filter((m) => m.personId !== currentPersonId);

  async function handleAccept(swapId: string) {
    if (!currentSpaceId) return;
    setActionLoading((prev) => ({ ...prev, [swapId]: "accepting" }));
    setActionError(null);
    setSuccessMessage(null);
    try {
      await acceptSwap(currentSpaceId, groupId, swapId);
      await fetchSwaps();
      await onSwapsChanged?.();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setActionError(message);
      await fetchSwaps(false);
    } finally {
      setActionLoading((prev) => {
        const next = { ...prev };
        delete next[swapId];
        return next;
      });
    }
  }

  async function handleDecline(swapId: string) {
    if (!currentSpaceId) return;
    setActionLoading((prev) => ({ ...prev, [swapId]: "declining" }));
    setActionError(null);
    setSuccessMessage(null);
    try {
      await declineSwap(currentSpaceId, groupId, swapId);
      await fetchSwaps();
      await onSwapsChanged?.();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setActionError(message);
      await fetchSwaps(false);
    } finally {
      setActionLoading((prev) => {
        const next = { ...prev };
        delete next[swapId];
        return next;
      });
    }
  }

  async function handleCancel(swapId: string) {
    if (!currentSpaceId) return;
    setActionLoading((prev) => ({ ...prev, [swapId]: "cancelling" }));
    setActionError(null);
    setSuccessMessage(null);
    try {
      await cancelSwap(currentSpaceId, groupId, swapId);
      await fetchSwaps();
      await onSwapsChanged?.();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setActionError(message);
      await fetchSwaps(false);
    } finally {
      setActionLoading((prev) => {
        const next = { ...prev };
        delete next[swapId];
        return next;
      });
    }
  }

  async function startProposeFlow() {
    if (!currentSpaceId) return;
    setProposeStep("selectMyShift");
    setProposeError(null);
    setMyShiftsLoading(true);
    try {
      const data = await getMyShiftRequests(currentSpaceId, groupId);
      setMyShifts(sortShiftsByStart(data.requests.filter(isFutureApprovedShift)));
    } catch {
      setProposeError(tCommon("error"));
    } finally {
      setMyShiftsLoading(false);
    }
  }

  function handleSelectMyShift(shift: ShiftRequestDto) {
    setSelectedMyShift(shift);
    setProposeStep("selectTargetMember");
  }

  function handleSelectTargetMember(member: GroupMemberDto) {
    setSelectedTargetMember(member);
    setProposeStep("selectTargetShift");
    fetchTargetShifts(member.personId);
  }

  async function fetchTargetShifts(personId: string) {
    if (!currentSpaceId) return;
    setTargetShiftsLoading(true);
    try {
      const data = await getMemberApprovedShifts(currentSpaceId, groupId, personId);
      setTargetShifts(sortShiftsByStart(data.filter(isFutureApprovedShift)));
    } catch {
      setProposeError(tCommon("error"));
    } finally {
      setTargetShiftsLoading(false);
    }
  }

  async function handlePropose(targetShiftRequestId: string) {
    if (!currentSpaceId || !selectedMyShift) return;
    setProposing(true);
    setProposeError(null);
    setSuccessMessage(null);
    try {
      await proposeSwap(
        currentSpaceId,
        groupId,
        selectedMyShift.id,
        targetShiftRequestId
      );
      resetProposeFlow();
      setSuccessMessage(t("proposeSuccess"));
      await fetchSwaps();
      await onSwapsChanged?.();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setProposeError(message);
      await fetchSwaps(false);
    } finally {
      setProposing(false);
    }
  }

  function resetProposeFlow() {
    setProposeStep("idle");
    setSelectedMyShift(null);
    setSelectedTargetMember(null);
    setTargetShifts([]);
    setProposeError(null);
  }

  if (loading) {
    return <LoadingCard rows={3} variant="list" />;
  }

  if (error) {
    return <ErrorRetry message={error} onRetry={() => fetchSwaps()} />;
  }

  return (
    <div className="space-y-5">
      {/* Header with Propose button */}
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-slate-700">{t("title")}</h2>
        {proposeStep === "idle" && (
          <button
            onClick={startProposeFlow}
            data-testid="self-service-propose-swap"
            className="rounded-lg bg-sky-600 px-4 py-2 text-xs font-semibold text-white hover:bg-sky-700 transition-colors"
          >
            {t("proposeButton")}
          </button>
        )}
      </div>

      {/* Action error banner */}
      {actionError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700">
          {actionError}
        </div>
      )}

      {successMessage && (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-700">
          {successMessage}
        </div>
      )}

      {/* Propose Swap Flow */}
      {proposeStep !== "idle" && (
        <div className="bg-white border border-sky-200 rounded-xl p-4 space-y-3">
          {/* Step: Select my shift */}
          {proposeStep === "selectMyShift" && (
            <div>
              <div className="flex items-center justify-between mb-3">
                <p className="text-sm font-medium text-slate-700">{t("selectYourShift")}</p>
                <button
                  onClick={resetProposeFlow}
                  aria-label={t("close")}
                  className="text-xs text-slate-500 hover:text-slate-700"
                >
                  x
                </button>
              </div>
              {myShiftsLoading ? (
                <div className="flex items-center gap-2 text-slate-400 text-xs">
                  <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                  </svg>
                  {tCommon("loading")}
                </div>
              ) : myShifts.length === 0 ? (
                <p className="text-xs text-slate-400">{t("noApprovedShifts")}</p>
              ) : (
                <div className="space-y-2">
                  {myShifts.map((shift) => (
                    <button
                      key={shift.id}
                      onClick={() => handleSelectMyShift(shift)}
                      data-testid="self-service-swap-my-shift"
                      data-shift-request-id={shift.id}
                      className="w-full text-right rounded-lg border border-slate-200 p-3 hover:border-sky-300 hover:bg-sky-50 transition-colors"
                    >
                      <p className="text-xs font-medium text-slate-700">
                        {formatSlotDate(shift.slotDate)} | {formatTime24h(shift.slotStartTime)}-{formatTime24h(shift.slotEndTime)}
                      </p>
                      <p className="text-xs text-slate-500">{shift.taskName}</p>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Step: Select target member */}
          {proposeStep === "selectTargetMember" && (
            <div>
              <div className="flex items-center justify-between mb-3">
                <p className="text-sm font-medium text-slate-700">{t("selectTargetShift")}</p>
                <button
                  onClick={resetProposeFlow}
                  aria-label={t("close")}
                  className="text-xs text-slate-500 hover:text-slate-700"
                >
                  x
                </button>
              </div>
              <p className="text-xs text-slate-500 mb-2">{t("selectTargetMember")}</p>
              {swappableMembers.length === 0 ? (
                <p className="text-xs text-slate-400">{t("noTargetMembers")}</p>
              ) : (
                <div className="space-y-1 max-h-48 overflow-y-auto">
                  {swappableMembers.map((member) => (
                    <button
                      key={member.personId}
                      onClick={() => handleSelectTargetMember(member)}
                      data-testid="self-service-swap-target-member"
                      data-person-id={member.personId}
                      className="w-full text-right rounded-lg border border-slate-200 px-3 py-2 hover:border-sky-300 hover:bg-sky-50 transition-colors text-xs font-medium text-slate-700"
                    >
                      {member.displayName || member.fullName}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Step: Select target shift */}
          {proposeStep === "selectTargetShift" && (
            <div>
              <div className="flex items-center justify-between mb-3">
                <p className="text-sm font-medium text-slate-700">
                  {t("selectTargetShift")} - {selectedTargetMember?.displayName || selectedTargetMember?.fullName}
                </p>
                <button
                  onClick={resetProposeFlow}
                  aria-label={t("close")}
                  className="text-xs text-slate-500 hover:text-slate-700"
                >
                  x
                </button>
              </div>
              {targetShiftsLoading ? (
                <div className="flex items-center gap-2 text-slate-400 text-xs">
                  <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                  </svg>
                  {tCommon("loading")}
                </div>
              ) : targetShifts.length === 0 ? (
                <p className="text-xs text-slate-400">{t("noTargetShifts")}</p>
              ) : (
                <div className="space-y-2">
                  {targetShifts.map((shift) => (
                    <button
                      key={shift.id}
                      onClick={() => handlePropose(shift.id)}
                      data-testid="self-service-swap-target-shift"
                      data-shift-request-id={shift.id}
                      disabled={proposing}
                      className="w-full text-right rounded-lg border border-slate-200 p-3 hover:border-sky-300 hover:bg-sky-50 transition-colors disabled:opacity-50"
                    >
                      <p className="text-xs font-medium text-slate-700">
                        {formatSlotDate(shift.slotDate)} | {formatTime24h(shift.slotStartTime)}-{formatTime24h(shift.slotEndTime)}
                      </p>
                      <p className="text-xs text-slate-500">{shift.taskName}</p>
                    </button>
                  ))}
                </div>
              )}
              {proposing && (
                <p className="text-xs text-slate-400 mt-2">{t("proposing")}</p>
              )}
            </div>
          )}

          {/* Propose error */}
          {proposeError && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-2 text-xs text-red-700">
              {proposeError}
            </div>
          )}
        </div>
      )}

      {isAdmin && (
        <div>
          <div className="mb-2 flex items-center justify-between gap-3">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {t("adminOverview")}
            </h3>
            <span className="rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-xs font-medium text-slate-600">
              {t("adminPendingCount", { count: String(adminSwaps.length) })}
            </span>
          </div>
          {adminSwaps.length === 0 ? (
            <div className="rounded-xl border border-slate-200 bg-white px-4 py-6 text-center">
              <p className="text-sm text-slate-400">{t("adminNoPending")}</p>
            </div>
          ) : (
            <div className="space-y-2">
              {adminSwaps.map((swap) => (
                <SwapCard
                  key={`admin-${swap.id}`}
                  swap={swap}
                  direction="admin"
                  t={t}
                />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Incoming swaps */}
      {incomingSwaps.length > 0 && (
        <div>
          <h3 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-2">
            {t("incoming")}
          </h3>
          <div className="space-y-2">
            {incomingSwaps.map((swap) => (
              <SwapCard
                key={swap.id}
                swap={swap}
                direction="incoming"
                actionLoading={actionLoading[swap.id]}
                onAccept={() => handleAccept(swap.id)}
                onDecline={() => handleDecline(swap.id)}
                t={t}
              />
            ))}
          </div>
        </div>
      )}

      {/* Outgoing swaps */}
      {outgoingSwaps.length > 0 && (
        <div>
          <h3 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-2">
            {t("outgoing")}
          </h3>
          <div className="space-y-2">
            {outgoingSwaps.map((swap) => (
              <SwapCard
                key={swap.id}
                swap={swap}
                direction="outgoing"
                actionLoading={actionLoading[swap.id]}
                onCancel={() => handleCancel(swap.id)}
                t={t}
              />
            ))}
          </div>
        </div>
      )}

      {/* Completed/historical swaps */}
      {completedSwaps.length > 0 && (
        <div>
          <h3 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-2">
            {t("history")}
          </h3>
          <div className="space-y-2">
            {completedSwaps.map((swap) => (
              <SwapCard
                key={swap.id}
                swap={swap}
                direction="history"
                t={t}
              />
            ))}
          </div>
        </div>
      )}

      {/* Empty state */}
      {swaps.length === 0 && (
        <div className="flex flex-col items-center justify-center py-12 bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">{t("noSwaps")}</p>
        </div>
      )}
    </div>
  );
}


interface SwapCardProps {
  swap: SwapRequestDto;
  direction: "incoming" | "outgoing" | "history" | "admin";
  actionLoading?: string;
  onAccept?: () => void;
  onDecline?: () => void;
  onCancel?: () => void;
  t: (key: string, values?: Record<string, string>) => string;
}

function SwapCard({ swap, direction, actionLoading, onAccept, onDecline, onCancel, t }: SwapCardProps) {
  const counterpartName = direction === "admin"
    ? t("adminSwapPair", {
        initiator: swap.initiatorPersonName,
        target: swap.targetPersonName,
      })
    : direction === "incoming"
      ? swap.initiatorPersonName
      : swap.targetPersonName;

  return (
    <div
      data-testid="self-service-swap-card"
      data-swap-request-id={swap.id}
      className="bg-white border border-slate-200 rounded-xl p-4"
    >
      {/* Header: status + counterpart */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <StatusBadge status={swap.status} t={t} />
          <span className="text-xs text-slate-600">{counterpartName}</span>
        </div>
        {swap.status === "Pending" && swap.expiresAt && (
          <span className="text-xs text-amber-600">
            {t("countdown", { time: formatCountdown(swap.expiresAt) })}
          </span>
        )}
      </div>

      {/* Shift details */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {/* Offered shift (initiator's shift) */}
        <div className="rounded-lg bg-slate-50 p-2.5">
          <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-1">
            {t("offeredShift")}
          </p>
          <p className="text-xs font-medium text-slate-700">
            {formatSlotDate(swap.initiatorSlotDate)}
          </p>
          <p className="text-xs text-slate-600">
            {formatSlotTimeRange(swap.initiatorSlotTime)} | {swap.initiatorTaskName}
          </p>
        </div>

        {/* Requested shift (target's shift) */}
        <div className="rounded-lg bg-slate-50 p-2.5">
          <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-1">
            {t("requestedShift")}
          </p>
          <p className="text-xs font-medium text-slate-700">
            {formatSlotDate(swap.targetSlotDate)}
          </p>
          <p className="text-xs text-slate-600">
            {formatSlotTimeRange(swap.targetSlotTime)} | {swap.targetTaskName}
          </p>
        </div>
      </div>

      {/* Action buttons */}
      {direction === "incoming" && swap.status === "Pending" && (
        <div className="flex items-center gap-2 mt-3">
          <button
            onClick={onAccept}
            data-testid="self-service-accept-swap"
            disabled={!!actionLoading}
            className="rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 transition-colors disabled:opacity-50"
          >
            {actionLoading === "accepting" ? t("accepting") : t("acceptButton")}
          </button>
          <button
            onClick={onDecline}
            data-testid="self-service-decline-swap"
            disabled={!!actionLoading}
            className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 transition-colors disabled:opacity-50"
          >
            {actionLoading === "declining" ? t("declining") : t("declineButton")}
          </button>
        </div>
      )}

      {direction === "outgoing" && swap.status === "Pending" && (
        <div className="flex items-center gap-2 mt-3">
          <button
            onClick={onCancel}
            data-testid="self-service-cancel-swap"
            disabled={!!actionLoading}
            className="rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 transition-colors disabled:opacity-50"
          >
            {actionLoading === "cancelling" ? t("cancelling") : t("cancelButton")}
          </button>
        </div>
      )}
    </div>
  );
}


function StatusBadge({ status, t }: { status: SwapRequestDto["status"]; t: (key: string) => string }) {
  const styles: Record<SwapRequestDto["status"], string> = {
    Pending: "bg-amber-50 text-amber-700 border-amber-200",
    Accepted: "bg-emerald-50 text-emerald-700 border-emerald-200",
    Declined: "bg-red-50 text-red-700 border-red-200",
    Cancelled: "bg-slate-100 text-slate-600 border-slate-200",
    Expired: "bg-slate-100 text-slate-500 border-slate-200",
  };
  const labels: Record<SwapRequestDto["status"], string> = {
    Pending: t("statusPending"),
    Accepted: t("statusAccepted"),
    Declined: t("statusDeclined"),
    Cancelled: t("statusCancelled"),
    Expired: t("statusExpired"),
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${styles[status]}`}>
      {labels[status]}
    </span>
  );
}
