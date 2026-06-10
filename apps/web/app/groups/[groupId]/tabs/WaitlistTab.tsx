"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import Modal from "@/components/Modal";
import {
  adminAssignMember,
  getAdminWaitlistEntries,
  getMyWaitlistEntries,
  acceptWaitlistOffer,
  leaveWaitlist,
  AdminWaitlistEntryDto,
  WaitlistEntryDto,
} from "@/lib/api/selfService";
import { formatSlotDate, formatTime24h, formatCountdown } from "@/lib/utils/selfServiceFormat";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import { LoadingCard, ErrorRetry, MutationButton } from "@/components/groups/selfService";

interface WaitlistTabProps {
  spaceId: string;
  groupId: string;
  isAdmin?: boolean;
}

const STATUS_BADGE_STYLES: Record<WaitlistEntryDto["status"], string> = {
  Waiting: "bg-amber-50 text-amber-700 border-amber-200",
  Offered: "bg-sky-50 text-sky-700 border-sky-200",
  Accepted: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Expired: "bg-slate-100 text-slate-500 border-slate-200",
  Declined: "bg-red-50 text-red-600 border-red-200",
  Removed: "bg-slate-100 text-slate-500 border-slate-200",
};

export default function WaitlistTab({ spaceId, groupId, isAdmin = false }: WaitlistTabProps) {
  const t = useTranslations("selfService.waitlist");

  const [entries, setEntries] = useState<WaitlistEntryDto[]>([]);
  const [adminEntries, setAdminEntries] = useState<AdminWaitlistEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Action loading states
  const [acceptingId, setAcceptingId] = useState<string | null>(null);
  const [decliningId, setDecliningId] = useState<string | null>(null);
  const [leavingId, setLeavingId] = useState<string | null>(null);
  const [adminAssigningId, setAdminAssigningId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  // Leave confirmation dialog
  const [leaveConfirmEntry, setLeaveConfirmEntry] = useState<WaitlistEntryDto | null>(null);

  // Countdown refresh
  const [, setTick] = useState(0);

  const fetchEntries = useCallback(async () => {
    try {
      setError(null);
      const [myEntries, allEntries] = await Promise.all([
        getMyWaitlistEntries(spaceId, groupId),
        isAdmin ? getAdminWaitlistEntries(spaceId, groupId) : Promise.resolve([]),
      ]);
      setEntries(myEntries);
      setAdminEntries(allEntries);
    } catch {
      setError(t("title")); // generic error — will show retry
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId, isAdmin, t]);

  useEffect(() => {
    void Promise.resolve().then(fetchEntries);
  }, [fetchEntries]);

  // Refresh countdown timers every 30 seconds
  useEffect(() => {
    const hasOffered =
      entries.some((e) => e.status === "Offered" && e.expiresAt)
      || adminEntries.some((e) => e.status === "Offered" && e.expiresAt);
    if (!hasOffered) return;

    const interval = setInterval(() => {
      setTick((prev) => prev + 1);
    }, 30_000);

    return () => clearInterval(interval);
  }, [entries, adminEntries]);

  const handleAccept = async (entry: WaitlistEntryDto) => {
    setAcceptingId(entry.id);
    setActionError(null);
    try {
      await acceptWaitlistOffer(spaceId, groupId, entry.shiftSlotId);
      // Refetch to get updated state
      await fetchEntries();
    } catch (err) {
      const errorResult = getSelfServiceErrorMessage(err);
      setActionError(errorResult.message);
    } finally {
      setAcceptingId(null);
    }
  };

  const handleDecline = async (entry: WaitlistEntryDto) => {
    setDecliningId(entry.id);
    setActionError(null);
    try {
      // Decline is handled by leaving the waitlist for the offered slot
      await leaveWaitlist(spaceId, groupId, entry.shiftSlotId);
      // Refetch to get updated state
      await fetchEntries();
    } catch (err) {
      const errorResult = getSelfServiceErrorMessage(err);
      setActionError(errorResult.message);
    } finally {
      setDecliningId(null);
    }
  };

  const handleLeaveConfirm = async () => {
    if (!leaveConfirmEntry) return;
    setLeavingId(leaveConfirmEntry.id);
    setActionError(null);
    try {
      await leaveWaitlist(spaceId, groupId, leaveConfirmEntry.shiftSlotId);
      setLeaveConfirmEntry(null);
      await fetchEntries();
    } catch (err) {
      const errorResult = getSelfServiceErrorMessage(err);
      setActionError(errorResult.message);
    } finally {
      setLeavingId(null);
    }
  };

  const handleAdminAssign = async (entry: AdminWaitlistEntryDto) => {
    setAdminAssigningId(entry.id);
    setActionError(null);
    try {
      await adminAssignMember(spaceId, groupId, entry.shiftSlotId, entry.personId);
      await fetchEntries();
    } catch (err) {
      const errorResult = getSelfServiceErrorMessage(err);
      setActionError(errorResult.message);
    } finally {
      setAdminAssigningId(null);
    }
  };

  const getStatusLabel = (status: WaitlistEntryDto["status"]): string => {
    const statusMap: Record<WaitlistEntryDto["status"], string> = {
      Waiting: t("statusWaiting"),
      Offered: t("statusOffered"),
      Accepted: t("statusAccepted"),
      Expired: t("statusExpired"),
      Declined: t("statusDeclined"),
      Removed: t("statusRemoved"),
    };
    return statusMap[status];
  };

  // Loading skeleton
  if (loading) {
    return <LoadingCard rows={3} variant="list" />;
  }

  // Error state with retry
  if (error) {
    return <ErrorRetry message={error} onRetry={fetchEntries} />;
  }

  // Empty state
  if (entries.length === 0 && adminEntries.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
        <p className="text-slate-400 text-sm">{t("noEntries")}</p>
      </div>
    );
  }

  // Separate offered entries (highlighted) from others
  const offeredEntries = entries.filter((e) => e.status === "Offered");
  const otherEntries = entries.filter((e) => e.status !== "Offered");

  return (
    <div className="space-y-4">
      {/* Action error banner */}
      {actionError && (
        <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">
          {actionError}
        </div>
      )}

      {/* Offered entries — highlighted prominently */}
      {isAdmin && adminEntries.length > 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-4">
          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">{t("adminTitle")}</h3>
              <p className="text-xs text-slate-500">{t("adminDescription")}</p>
            </div>
            <span className="inline-flex w-fit rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-600">
              {t("adminCount", { count: adminEntries.length })}
            </span>
          </div>

          <div className="mt-4 divide-y divide-slate-100">
            {adminEntries.map((entry) => (
              <div
                key={entry.id}
                className="grid gap-3 py-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span
                      className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_BADGE_STYLES[entry.status]}`}
                    >
                      {getStatusLabel(entry.status)}
                    </span>
                    <span className="text-sm font-semibold text-slate-900">{entry.personName}</span>
                    <span className="text-xs text-slate-500">
                      {t("position", { position: entry.position })}
                    </span>
                  </div>
                  <p className="mt-1 truncate text-sm text-slate-600">
                    {entry.taskName} - {formatSlotDate(entry.slotDate)} - {formatTime24h(entry.slotStartTime)}-{formatTime24h(entry.slotEndTime)}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                  {entry.status === "Offered" && entry.expiresAt && (
                    <div className="text-sm font-medium text-sky-700">
                      {t("offerExpires", { time: formatCountdown(entry.expiresAt) })}
                    </div>
                  )}
                  <button
                    type="button"
                    onClick={() => handleAdminAssign(entry)}
                    disabled={adminAssigningId === entry.id}
                    className="inline-flex items-center rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {adminAssigningId === entry.id ? t("adminAssigning") : t("adminAssignButton")}
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {offeredEntries.length > 0 && (
        <div className="space-y-3">
          {offeredEntries.map((entry) => (
            <div
              key={entry.id}
              className="bg-sky-50 border-2 border-sky-300 rounded-2xl p-4 space-y-3"
            >
              {/* Header with status badge */}
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span
                    className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${STATUS_BADGE_STYLES[entry.status]}`}
                  >
                    {getStatusLabel(entry.status)}
                  </span>
                  <span className="text-sm font-semibold text-slate-900">
                    {entry.taskName}
                  </span>
                </div>
              </div>

              {/* Slot details */}
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-700">
                <span>{formatSlotDate(entry.slotDate)}</span>
                <span>
                  {formatTime24h(entry.slotStartTime)} – {formatTime24h(entry.slotEndTime)}
                </span>
                <span className="text-slate-500">
                  {t("position", { position: entry.position })}
                </span>
              </div>

              {/* Countdown timer */}
              {entry.expiresAt && (
                <div className="text-sm font-medium text-sky-700">
                  {t("offerExpires", { time: formatCountdown(entry.expiresAt) })}
                </div>
              )}

              {/* Accept / Decline buttons */}
              <div className="flex gap-2">
                <button
                  onClick={() => handleAccept(entry)}
                  disabled={acceptingId === entry.id}
                  className="bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors"
                >
                  {acceptingId === entry.id ? t("accepting") : t("acceptButton")}
                </button>
                <button
                  onClick={() => handleDecline(entry)}
                  disabled={decliningId === entry.id}
                  className="text-sm text-red-600 border border-red-200 bg-red-50 hover:bg-red-100 px-4 py-2 rounded-xl disabled:opacity-50 transition-colors"
                >
                  {decliningId === entry.id ? t("declining") : t("declineButton")}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Other entries (Waiting, Accepted, Expired, Declined, Removed) */}
      {otherEntries.length > 0 && (
        <div className="space-y-3">
          {otherEntries.map((entry) => (
            <div
              key={entry.id}
              className="bg-white border border-slate-200 rounded-2xl p-4 space-y-2"
            >
              {/* Header with status badge */}
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span
                    className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${STATUS_BADGE_STYLES[entry.status]}`}
                  >
                    {getStatusLabel(entry.status)}
                  </span>
                  <span className="text-sm font-semibold text-slate-900">
                    {entry.taskName}
                  </span>
                </div>

                {/* Leave button for waiting entries */}
                {entry.status === "Waiting" && (
                  <button
                    onClick={() => setLeaveConfirmEntry(entry)}
                    disabled={leavingId === entry.id}
                    className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2.5 py-1 rounded-lg hover:bg-red-50 transition-colors disabled:opacity-50"
                  >
                    {leavingId === entry.id ? t("leaving") : t("leaveButton")}
                  </button>
                )}
              </div>

              {/* Slot details */}
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-600">
                <span>{formatSlotDate(entry.slotDate)}</span>
                <span>
                  {formatTime24h(entry.slotStartTime)} – {formatTime24h(entry.slotEndTime)}
                </span>
                <span className="text-slate-500">
                  {t("position", { position: entry.position })}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Leave confirmation dialog */}
      <Modal
        title={t("leaveConfirmTitle")}
        open={!!leaveConfirmEntry}
        onClose={() => setLeaveConfirmEntry(null)}
        maxWidth={400}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-700">{t("leaveConfirmMessage")}</p>
          <div className="flex gap-2">
            <button
              onClick={handleLeaveConfirm}
              disabled={!!leavingId}
              className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
            >
              {leavingId ? t("leaving") : t("leaveConfirmYes")}
            </button>
            <button
              onClick={() => setLeaveConfirmEntry(null)}
              className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors"
            >
              {t("leaveConfirmNo")}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
