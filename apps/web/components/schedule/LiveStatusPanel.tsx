"use client";

import { useEffect, useState, useCallback } from "react";
import { getGroupLiveStatus, MemberLiveStatusDto } from "@/lib/api/groups";

interface LiveStatusPanelProps {
  spaceId: string;
  groupId: string;
}

const STATUS_CONFIG: Record<
  MemberLiveStatusDto["status"],
  { label: string; dot: string; badge: string }
> = {
  on_mission:   { label: "במשימה",    dot: "bg-blue-500",    badge: "bg-blue-50 text-blue-700 border-blue-200" },
  at_home:      { label: "בבית",      dot: "bg-amber-400",   badge: "bg-amber-50 text-amber-700 border-amber-200" },
  blocked:      { label: "לא זמין",   dot: "bg-red-500",     badge: "bg-red-50 text-red-700 border-red-200" },
  free_in_base: { label: "פנוי בבסיס", dot: "bg-emerald-500", badge: "bg-emerald-50 text-emerald-700 border-emerald-200" },
};

function formatTime(iso: string | null): string {
  if (!iso) return "";
  return new Date(iso).toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit" });
}

export default function LiveStatusPanel({ spaceId, groupId }: LiveStatusPanelProps) {
  const [statuses, setStatuses] = useState<MemberLiveStatusDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  const fetchStatus = useCallback(async () => {
    try {
      const data = await getGroupLiveStatus(spaceId, groupId);
      setStatuses(data);
      setLastUpdated(new Date());
      setError(null);
    } catch {
      setError("שגיאה בטעינת הסטטוס");
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
        <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        טוען סטטוס...
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
          <span className="text-sm font-medium text-slate-700">סטטוס נוכחי</span>
        </div>
        <div className="flex items-center gap-2">
          {lastUpdated && (
            <span className="text-xs text-slate-400">
              עודכן: {lastUpdated.toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit", second: "2-digit" })}
            </span>
          )}
          <button
            onClick={fetchStatus}
            className="text-xs text-blue-600 hover:text-blue-700 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-2.5 py-1 rounded-lg transition-colors"
          >
            רענן
          </button>
        </div>
      </div>

      {statuses.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">אין חברים בקבוצה</p>
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
                                <span className="text-slate-400"> · עד {formatTime(m.slotEndsAt)}</span>
                              )}
                            </p>
                          )}
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
