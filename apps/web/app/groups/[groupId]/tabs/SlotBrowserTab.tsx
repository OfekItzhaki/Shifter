"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import { useTranslations } from "next-intl";
import {
  getAvailableSlots,
  submitShiftRequest,
  joinWaitlist,
  AvailableSlotDto,
  AvailableSlotsResponse,
} from "@/lib/api/selfService";
import {
  formatSlotDate,
  formatTime24h,
  getCapacityClass,
  HEBREW_DAY_NAMES,
} from "@/lib/utils/selfServiceFormat";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import { sortSlotsByDateTime } from "@/lib/utils/pickSlotSort";
import { LoadingCard, ErrorRetry } from "@/components/groups/selfService";

interface SlotBrowserTabProps {
  spaceId: string;
  groupId: string;
  isAdmin: boolean;
}

export default function SlotBrowserTab({ spaceId, groupId }: SlotBrowserTabProps) {
  const t = useTranslations("selfService.slotBrowser");
  const tSelfService = useTranslations("selfService");

  // ── State ────────────────────────────────────────────────────────────────
  const [slotsResponse, setSlotsResponse] = useState<AvailableSlotsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dateFilter, setDateFilter] = useState<string>("all");
  const [actionLoading, setActionLoading] = useState<string | null>(null); // slotId being acted on
  const [actionError, setActionError] = useState<{ slotId: string; message: string } | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // ── Fetch slots ──────────────────────────────────────────────────────────
  const fetchSlots = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAvailableSlots(spaceId, groupId, "current");
      setSlotsResponse(data);
    } catch {
      setError(tSelfService("error"));
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId, tSelfService]);

  useEffect(() => {
    void Promise.resolve().then(fetchSlots);
  }, [fetchSlots]);

  // ── Sorted and filtered slots ────────────────────────────────────────────
  const sortedSlots = useMemo(() => {
    if (!slotsResponse) return [];
    return sortSlotsByDateTime(slotsResponse.slots);
  }, [slotsResponse]);

  const filteredSlots = useMemo(() => {
    if (dateFilter === "all") return sortedSlots;
    return sortedSlots.filter((slot) => slot.date === dateFilter);
  }, [sortedSlots, dateFilter]);

  const uniqueDates = useMemo(() => {
    return [...new Set(sortedSlots.map((s) => s.date))].sort();
  }, [sortedSlots]);

  // ── Handlers ─────────────────────────────────────────────────────────────
  const handleRequest = async (slot: AvailableSlotDto) => {
    setActionLoading(slot.id);
    setActionError(null);
    setSuccessMessage(null);
    try {
      await submitShiftRequest(spaceId, groupId, slot.id);
      await fetchSlots();
      setSuccessMessage(t("requestSuccess"));
      setTimeout(() => setSuccessMessage(null), 3000);
    } catch (err) {
      const errorResult = getSelfServiceErrorMessage(err);
      setActionError({
        slotId: slot.id,
        message: errorResult.message,
      });
      await fetchSlots();
    } finally {
      setActionLoading(null);
    }
  };

  const handleJoinWaitlist = async (slot: AvailableSlotDto) => {
    setActionLoading(slot.id);
    setActionError(null);
    setSuccessMessage(null);
    try {
      const result = await joinWaitlist(spaceId, groupId, slot.id);
      await fetchSlots();
      setSuccessMessage(t("waitlistSuccess", { position: result.position }));
      setTimeout(() => setSuccessMessage(null), 3000);
    } catch (err) {
      const errorResult = getSelfServiceErrorMessage(err);
      setActionError({
        slotId: slot.id,
        message: errorResult.message,
      });
      await fetchSlots();
    } finally {
      setActionLoading(null);
    }
  };

  // ── Loading state ────────────────────────────────────────────────────────
  if (loading) {
    return <LoadingCard rows={5} variant="slots" />;
  }

  // ── Error state ──────────────────────────────────────────────────────────
  if (error) {
    return <ErrorRetry message={error} onRetry={fetchSlots} />;
  }

  if (!slotsResponse) return null;

  const { requestWindowOpen, requestWindowOpensAt } = slotsResponse;

  return (
    <div className="space-y-4">
      {/* Request window closed banner */}
      {!requestWindowOpen && (
        <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 flex items-center gap-3">
          <svg
            width={20}
            height={20}
            viewBox="0 0 24 24"
            fill="none"
            className="text-amber-500 flex-shrink-0"
            stroke="currentColor"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
          </svg>
          <div>
            <p className="text-sm font-medium text-amber-800">{t("windowClosed")}</p>
            {requestWindowOpensAt && (
              <p className="text-xs text-amber-600 mt-0.5">
                {t("windowOpensAt", { date: formatSlotDate(requestWindowOpensAt) })}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Success message */}
      {successMessage && (
        <div className="bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3 text-sm text-emerald-700">
          {successMessage}
        </div>
      )}

      {/* Date filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs text-slate-500 font-medium">{t("dateFilter")}:</span>
        <button
          onClick={() => setDateFilter("all")}
          className={`px-3 py-1.5 text-xs font-medium rounded-lg border transition-colors ${
            dateFilter === "all"
              ? "bg-sky-50 text-sky-700 border-sky-200"
              : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
          }`}
        >
          {t("allDates")}
        </button>
        {uniqueDates.map((date) => {
          const d = new Date(date + "T00:00:00");
          const dayName = HEBREW_DAY_NAMES[d.getDay()];
          const shortDate = d.toLocaleDateString("he-IL", {
            day: "numeric",
            month: "numeric",
          });
          return (
            <button
              key={date}
              onClick={() => setDateFilter(date)}
              className={`px-3 py-1.5 text-xs font-medium rounded-lg border transition-colors ${
                dateFilter === date
                  ? "bg-sky-50 text-sky-700 border-sky-200"
                  : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
              }`}
            >
              {dayName} {shortDate}
            </button>
          );
        })}
      </div>

      {/* Slots list */}
      {filteredSlots.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">{t("noSlots")}</p>
        </div>
      ) : (
        <div className="space-y-2">
          {filteredSlots.map((slot) => {
            const isFull = slot.currentFillCount >= slot.capacity;
            const capacityClass = getCapacityClass(slot.currentFillCount, slot.capacity);
            const isActing = actionLoading === slot.id;
            const slotError = actionError?.slotId === slot.id ? actionError.message : null;
            const d = new Date(slot.date + "T00:00:00");
            const dayName = HEBREW_DAY_NAMES[d.getDay()];

            return (
              <div
                key={slot.id}
                className={`bg-white border rounded-xl px-4 py-3 transition-colors ${
                  capacityClass === "high-availability"
                    ? "border-emerald-200"
                    : "border-amber-200"
                }`}
              >
                <div className="flex items-center justify-between gap-3">
                  {/* Slot info */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-medium text-slate-900">
                        {formatSlotDate(slot.date)}
                      </span>
                      <span className="text-xs text-slate-500">
                        {formatTime24h(slot.startTime)} – {formatTime24h(slot.endTime)}
                      </span>
                    </div>
                    <div className="flex items-center gap-2 mt-1">
                      <span className="text-xs text-slate-600">{slot.taskName}</span>
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${
                          capacityClass === "high-availability"
                            ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                            : "bg-amber-50 text-amber-700 border-amber-200"
                        }`}
                      >
                        {slot.currentFillCount}/{slot.capacity}
                      </span>
                      <span
                        className={`text-xs ${
                          capacityClass === "high-availability"
                            ? "text-emerald-600"
                            : "text-amber-600"
                        }`}
                      >
                        {capacityClass === "high-availability"
                          ? t("highAvailability")
                          : isFull
                          ? t("full")
                          : t("nearlyFull")}
                      </span>
                    </div>
                  </div>

                  {/* Action button */}
                  <div className="flex-shrink-0">
                    {requestWindowOpen ? (
                      isFull ? (
                        <button
                          onClick={() => handleJoinWaitlist(slot)}
                          disabled={isActing}
                          className="inline-flex items-center justify-center px-4 py-2 rounded-lg text-xs font-medium border border-amber-300 bg-amber-50 text-amber-700 hover:bg-amber-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          {isActing ? (
                            <svg
                              className="animate-spin h-4 w-4"
                              fill="none"
                              viewBox="0 0 24 24"
                            >
                              <circle
                                className="opacity-25"
                                cx="12"
                                cy="12"
                                r="10"
                                stroke="currentColor"
                                strokeWidth="4"
                              />
                              <path
                                className="opacity-75"
                                fill="currentColor"
                                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                              />
                            </svg>
                          ) : (
                            t("joinWaitlistButton")
                          )}
                        </button>
                      ) : (
                        <button
                          onClick={() => handleRequest(slot)}
                          disabled={isActing}
                          className="inline-flex items-center justify-center px-4 py-2 rounded-lg text-xs font-medium bg-sky-600 text-white hover:bg-sky-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          {isActing ? (
                            <svg
                              className="animate-spin h-4 w-4"
                              fill="none"
                              viewBox="0 0 24 24"
                            >
                              <circle
                                className="opacity-25"
                                cx="12"
                                cy="12"
                                r="10"
                                stroke="currentColor"
                                strokeWidth="4"
                              />
                              <path
                                className="opacity-75"
                                fill="currentColor"
                                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                              />
                            </svg>
                          ) : (
                            t("requestButton")
                          )}
                        </button>
                      )
                    ) : null}
                  </div>
                </div>

                {/* Error for this slot */}
                {slotError && (
                  <div className="mt-2 text-xs text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                    {slotError}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
