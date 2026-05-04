"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import ScheduleTaskTable, { type TaskAssignment } from "@/components/schedule/ScheduleTaskTable";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import { useMyAssignments, type AssignmentRange } from "@/lib/query/hooks/useMyAssignments";

function getCurrentWeekDays(): string[] {
  const today = new Date();
  const dayOfWeek = today.getUTCDay();
  const sunday = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate() - dayOfWeek));
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(sunday);
    d.setUTCDate(sunday.getUTCDate() + i);
    return d.toISOString().split("T")[0];
  });
}

export default function MyMissionsPage() {
  const t = useTranslations("schedule");
  const tMy = useTranslations("schedule.myMissions");
  const { currentSpaceId } = useSpaceStore();
  const { displayName } = useAuthStore();
  const { fDateLong } = useDateFormat();
  const [range, setRange] = useState<AssignmentRange>("week");
  const [search, setSearch] = useState("");
  const [selectedDay, setSelectedDay] = useState<string>(new Date().toISOString().split("T")[0]);

  const todayStr = new Date().toISOString().split("T")[0];
  const weekDays = getCurrentWeekDays();

  const { data: assignments = [], isLoading: loading } = useMyAssignments(currentSpaceId, range);

  const RANGE_LABELS: Record<AssignmentRange, string> = {
    today: tMy("rangeToday"),
    week: tMy("rangeWeek"),
    month: tMy("rangeMonth"),
    year: tMy("rangeYear"),
  };

  const DAY_NAMES = tMy.raw("dayNames") as string[];

  // Auto-select today when switching to week range
  useEffect(() => {
    if (range === "week") setSelectedDay(todayStr);
  }, [range]);

  const filtered = assignments.filter(a =>
    !search ||
    a.taskTypeName.toLowerCase().includes(search.toLowerCase()) ||
    a.groupName.toLowerCase().includes(search.toLowerCase())
  );

  // For the per-task table: convert to TaskAssignment shape
  const tableAssignments: TaskAssignment[] = filtered.map(a => ({
    personName: displayName ?? "me",
    taskTypeName: `${a.taskTypeName} (${a.groupName})`,
    slotStartsAt: a.slotStartsAt,
    slotEndsAt: a.slotEndsAt,
  }));

  // Dates that have assignments
  const datesWithAssignments = new Set(filtered.map(a => a.slotStartsAt.split("T")[0]));

  // For non-week ranges: group by date and show each date's table
  const sortedDates = Array.from(new Set(filtered.map(a => a.slotStartsAt.split("T")[0]))).sort();

  return (
    <AppShell>
      <div className="max-w-3xl space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">{tMy("title")}</h1>
          <p className="text-sm text-slate-500 mt-1">{tMy("subtitle")}</p>
        </div>

        {/* Range selector */}
        <div className="flex gap-1 bg-slate-100 p-1 rounded-xl w-fit">
          {(Object.keys(RANGE_LABELS) as AssignmentRange[]).map(r => (
            <button
              key={r}
              onClick={() => setRange(r)}
              className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-all ${
                range === r ? "bg-white text-slate-900 shadow-sm" : "text-slate-500 hover:text-slate-700"
              }`}
            >
              {RANGE_LABELS[r]}
            </button>
          ))}
        </div>

        {/* Week day buttons */}
        {range === "week" && (
          <div className="flex flex-wrap gap-2">
            {weekDays.map((dayIso, i) => {
              const hasMissions = datesWithAssignments.has(dayIso);
              const isToday = dayIso === todayStr;
              const isSelected = dayIso === selectedDay;
              return (
                <button
                  key={dayIso}
                  onClick={() => setSelectedDay(dayIso)}
                  className={`relative px-4 py-2 rounded-xl text-sm font-semibold transition-all border ${
                    isSelected
                      ? "bg-blue-500 text-white border-blue-500 shadow-sm"
                      : isToday
                      ? "bg-blue-50 text-blue-700 border-blue-300"
                      : hasMissions
                      ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                      : "bg-white text-slate-500 border-slate-200"
                  }`}
                >
                  {DAY_NAMES[i]}
                  {hasMissions && !isSelected && (
                    <span className="absolute -top-1 -right-1 w-2 h-2 rounded-full bg-emerald-500 border-2 border-white" />
                  )}
                </button>
              );
            })}
          </div>
        )}

        {/* Search */}
        <div className="relative max-w-sm">
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="#94a3b8" strokeWidth={2}
            className="absolute left-3 top-1/2 -translate-y-1/2">
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder={tMy("searchPlaceholder")}
            className="w-full border border-slate-200 rounded-xl pl-9 pr-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {loading ? (
          <p className="text-slate-400 text-sm py-8">{t("loading")}</p>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
            <svg className="w-10 h-10 text-slate-200 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
            <p className="text-slate-400 text-sm">{tMy("noMissionsInRange", { range: RANGE_LABELS[range] })}</p>
          </div>
        ) : range === "week" ? (
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-3">
              {fDateLong(selectedDay + "T00:00:00")}
            </p>
            <ScheduleTaskTable
              assignments={tableAssignments}
              filterDate={selectedDay}
              currentUserName={displayName ?? undefined}
            />
          </div>
        ) : (
          <div className="space-y-8">
            {sortedDates.map(date => (
              <div key={date}>
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-3">
                  {fDateLong(date + "T00:00:00")}
                </p>
                <ScheduleTaskTable
                  assignments={tableAssignments}
                  filterDate={date}
                  currentUserName={displayName ?? undefined}
                />
              </div>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
