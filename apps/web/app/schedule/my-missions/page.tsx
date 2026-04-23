"use client";

import { useEffect, useState } from "react";
import AppShell from "@/components/shell/AppShell";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";

interface MyAssignmentDto {
  id: string;
  groupId: string;
  groupName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
  source: string;
}

type Range = "today" | "week" | "month" | "year";

const RANGE_LABELS: Record<Range, string> = {
  today: "היום",
  week: "השבוע",
  month: "החודש",
  year: "השנה",
};

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit" });
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("he-IL", { weekday: "short", day: "numeric", month: "short" });
}

export default function MyMissionsPage() {
  const { currentSpaceId } = useSpaceStore();
  const [range, setRange] = useState<Range>("week");
  const [assignments, setAssignments] = useState<MyAssignmentDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    setLoading(true);
    apiClient.get(`/spaces/${currentSpaceId}/my-assignments?range=${range}`)
      .then(r => setAssignments(r.data))
      .catch(() => setAssignments([]))
      .finally(() => setLoading(false));
  }, [currentSpaceId, range]);

  // Group by date
  const byDate = assignments.reduce<Record<string, MyAssignmentDto[]>>((acc, a) => {
    const day = a.slotStartsAt.split("T")[0];
    if (!acc[day]) acc[day] = [];
    acc[day].push(a);
    return acc;
  }, {});

  return (
    <AppShell>
      <div className="max-w-3xl space-y-6">
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">המשימות שלי</h1>
            <p className="text-sm text-slate-500 mt-1">כל המשימות שלך בכל הקבוצות</p>
          </div>
        </div>

        {/* Range selector */}
        <div className="flex gap-1 bg-slate-100 p-1 rounded-xl w-fit">
          {(Object.keys(RANGE_LABELS) as Range[]).map(r => (
            <button key={r} onClick={() => setRange(r)}
              className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-all ${
                range === r
                  ? "bg-white text-slate-900 shadow-sm"
                  : "text-slate-500 hover:text-slate-700"
              }`}>
              {RANGE_LABELS[r]}
            </button>
          ))}
        </div>

        {loading ? (
          <p className="text-slate-400 text-sm py-8">טוען...</p>
        ) : assignments.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
            <svg className="w-10 h-10 text-slate-200 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
            <p className="text-slate-400 text-sm">אין משימות ב{RANGE_LABELS[range]}</p>
          </div>
        ) : (
          <div className="space-y-6">
            {Object.entries(byDate).map(([day, items]) => (
              <div key={day}>
                <h2 className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">
                  {formatDate(day + "T00:00:00")}
                </h2>
                <div className="space-y-2">
                  {items.map(a => (
                    <div key={a.id}
                      className="flex items-center gap-4 bg-white border border-slate-200 rounded-xl px-4 py-3 shadow-sm">
                      <div className="text-xs tabular-nums text-slate-500 w-24 shrink-0">
                        {formatTime(a.slotStartsAt)}<span className="mx-1 text-slate-300">–</span>{formatTime(a.slotEndsAt)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{a.taskTypeName}</p>
                        <p className="text-xs text-slate-400 truncate">{a.groupName}</p>
                      </div>
                      <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                        a.source === "Override"
                          ? "bg-amber-50 text-amber-700 border border-amber-200"
                          : "bg-slate-100 text-slate-500"
                      }`}>
                        {a.source === "Override" ? "עקיפה" : "סולבר"}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
