"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalTime } from "@/lib/utils/formatTime";
import { getGroupLiveStatus, MemberLiveStatusDto } from "@/lib/api/groups";
import { formatRestDuration, type SupportedLocale } from "@/lib/utils/restDuration";

interface LiveStatusPanelProps {
  spaceId: string;
  groupId: string;
  isAdmin?: boolean;
}

function formatTime(iso: string | null, timezoneId: string | null): string {
  if (!iso) return "";
  return formatLocalTime(iso, timezoneId, "24h");
}

function getRestWindow(member: MemberLiveStatusDto, locale: SupportedLocale) {
  const restStartsAt = member.status === "on_mission" ? member.slotEndsAt : member.previousEndsAt;
  const restEndsAt = member.nextStartsAt;

  if (!restStartsAt || !restEndsAt) return null;

  const start = new Date(restStartsAt).getTime();
  const end = new Date(restEndsAt).getTime();
  if (Number.isNaN(start) || Number.isNaN(end)) return null;

  const hours = Math.max(0, (end - start) / 3_600_000);
  return {
    restStartsAt,
    restEndsAt,
    duration: formatRestDuration(hours, locale),
  };
}

export default function LiveStatusPanel({ spaceId, groupId, isAdmin = false }: LiveStatusPanelProps) {
  const t = useTranslations("liveStatus");
  const locale = useLocale() as SupportedLocale;
  const timezoneId = useAuthStore(s => s.timezoneId);
  const [statuses, setStatuses] = useState<MemberLiveStatusDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  const STATUS_CONFIG: Record<
    MemberLiveStatusDto["status"],
    { label: string; dot: string; badge: string }
  > = {
    on_mission:   { label: t("onMission"),   dot: "bg-sky-500",    badge: "bg-sky-50 text-sky-700 border-sky-200" },
    at_home:      { label: t("atHome"),      dot: "bg-amber-400",   badge: "bg-amber-50 text-amber-700 border-amber-200" },
    blocked:      { label: t("blocked"),     dot: "bg-red-500",     badge: "bg-red-50 text-red-700 border-red-200" },
    free_in_base: { label: t("freeInBase"),  dot: "bg-emerald-500", badge: "bg-emerald-50 text-emerald-700 border-emerald-200" },
  };

  const fetchStatus = useCallback(async () => {
    try {
      const data = await getGroupLiveStatus(spaceId, groupId);
      setStatuses(data);
      setLastUpdated(new Date());
      setError(null);
    } catch {
      setError(t("errorLoading"));
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  // Initial load + 30-second polling
  useEffect(() => {
    fetchStatus();
    const interval = setInterval(fetchStatus, 30_000);
    return () => clearInterval(interval);
  }, [fetchStatus]);

  if (loading) {
    return (
      <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
        <svg className="animate-spin h-5 w-5 text-sky-400" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        {t("loading")}
      </div>
    );
  }

  if (error) {
    return <p className="text-sm text-red-600 py-4">{error}</p>;
  }

  // Group by status for a cleaner display
  const onMission   = statuses.filter(s => s.status === "on_mission");
  const freeInBase  = statuses.filter(s => s.status === "free_in_base");
  const atHome      = statuses.filter(s => s.status === "at_home");
  const blocked     = statuses.filter(s => s.status === "blocked");

  const groups = (
    [
      { key: "on_mission"   as const, members: onMission },
      { key: "free_in_base" as const, members: freeInBase },
      { key: "at_home"      as const, members: atHome },
      { key: "blocked"      as const, members: blocked },
    ] as { key: MemberLiveStatusDto["status"]; members: MemberLiveStatusDto[] }[]
  ).filter(g => g.members.length > 0);

  return (
    <div className="space-y-4">
      {/* Header with last-updated */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
          <span className="text-sm font-medium text-slate-700">{t("currentStatus")}</span>
          <span className="text-xs text-slate-400">({statuses.length})</span>
        </div>
        <div className="flex items-center gap-2">
          {lastUpdated && (
            <span className="text-xs text-slate-400">
              {t("updated")}: {lastUpdated.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", second: "2-digit", timeZone: timezoneId || "Asia/Jerusalem" })}
            </span>
          )}
          <button
            onClick={fetchStatus}
            className="text-xs text-sky-600 hover:text-sky-700 border border-sky-200 bg-sky-50 hover:bg-sky-100 px-2.5 py-1 rounded-lg transition-colors"
          >
            {t("refresh")}
          </button>
        </div>
      </div>

      {statuses.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">{t("noMembers")}</p>
        </div>
      ) : (
        <div className="space-y-4">
          {groups.map(({ key, members }) => {
            const cfg = STATUS_CONFIG[key];
            return (
              <div key={key}>
                <div className="flex items-center gap-2 mb-2">
                  <span className={`w-2 h-2 rounded-full ${cfg.dot}`} />
                  <span className="text-xs font-semibold text-slate-500 uppercase tracking-wide">
                    {cfg.label} ({members.length})
                  </span>
                </div>
                <div className="space-y-2">
                  {members.map(m => (
                    <div
                      key={m.personId}
                      className="flex items-center justify-between gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3"
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        {/* Avatar */}
                        <div className="w-8 h-8 rounded-full bg-slate-200 flex items-center justify-center flex-shrink-0">
                          <span className="text-xs font-semibold text-slate-600">
                            {m.displayName.charAt(0).toUpperCase()}
                          </span>
                        </div>
                        <div className="min-w-0">
                          <p className="text-sm font-medium text-slate-900 truncate">{m.displayName}</p>
                          {m.status === "on_mission" && m.taskName && (
                            <p className="text-xs text-slate-500 truncate">
                              {m.taskName}
                              {m.slotEndsAt && (
                                <span className="text-slate-400"> · {t("until")} {formatTime(m.slotEndsAt, timezoneId)}</span>
                              )}
                            </p>
                          )}
                          {isAdmin && (() => {
                            const restWindow = getRestWindow(m, locale);
                            if (!restWindow) return null;

                            return (
                              <span
                                className="flex items-center gap-1 text-[11px] text-slate-400 mt-0.5"
                                title={`${formatTime(restWindow.restStartsAt, timezoneId)} - ${formatTime(restWindow.restEndsAt, timezoneId)}`}
                              >
                                <span>{t("restBeforeNext")}</span>
                                <span dir="ltr" className="font-medium text-slate-500 tabular-nums">{restWindow.duration}</span>
                              </span>
                            );
                          })()}
                        </div>
                      </div>
                      <span className={`flex-shrink-0 inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium border ${cfg.badge}`}>
                        <span className={`w-1.5 h-1.5 rounded-full ${cfg.dot}`} />
                        {cfg.label}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
