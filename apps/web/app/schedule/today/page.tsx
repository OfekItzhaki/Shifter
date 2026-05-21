"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import ScheduleTaskTable, { type TaskAssignment } from "@/components/schedule/ScheduleTaskTable";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { useGroups } from "@/lib/query/hooks/useGroups";
import { useGroupSchedule } from "@/lib/query/hooks/useGroupSchedule";

export default function TodayPage() {
  const t = useTranslations("schedule");
  const tNav = useTranslations("nav");
  const { currentSpaceId } = useSpaceStore();
  const { displayName, timezoneId } = useAuthStore();
  const [selectedGroupId, setSelectedGroupId] = useState("");
  const [todayLabel, setTodayLabel] = useState("");

  const today = new Date().toISOString().split("T")[0];

  // Hydration-safe date label
  useEffect(() => {
    setTodayLabel(new Date().toLocaleDateString(undefined, {
      weekday: "long", year: "numeric", month: "long", day: "numeric",
      timeZone: timezoneId || "Asia/Jerusalem",
    }));
  }, [timezoneId]);

  // React Query — groups list
  const { data: groups = [], isLoading: groupsLoading } = useGroups(currentSpaceId);

  // Auto-select first group when groups load
  useEffect(() => {
    if (groups.length > 0 && !selectedGroupId) {
      setSelectedGroupId(groups[0].id);
    }
  }, [groups, selectedGroupId]);

  // React Query — schedule for selected group
  const {
    data: scheduleResponse,
    isLoading: scheduleLoading,
    isError,
  } = useGroupSchedule(currentSpaceId, selectedGroupId || null);

  const rawAssignments = scheduleResponse?.assignments ?? [];

  const assignments: TaskAssignment[] = rawAssignments.map(a => ({
    id: a.id,
    personId: a.personId,
    personName: a.personName,
    taskTypeName: a.taskTypeName,
    slotStartsAt: a.slotStartsAt,
    slotEndsAt: a.slotEndsAt,
  }));

  const loading = groupsLoading || scheduleLoading;

  return (
    <AppShell>
      <div className="max-w-4xl space-y-6">
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">{t("title")} — {tNav("today")}</h1>
            <p className="text-sm text-slate-500 mt-1 capitalize">{todayLabel}</p>
          </div>
          <div className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 border border-blue-100 rounded-full">
            <span className="w-2 h-2 rounded-full bg-blue-500 animate-pulse" />
            <span className="text-xs font-medium text-blue-600">{t("live")}</span>
          </div>
        </div>

        {/* Group selector */}
        <div className="bg-white border border-slate-200 rounded-xl p-4 flex items-center gap-4">
          <label className="text-sm font-medium text-slate-700 whitespace-nowrap">{t("selectGroupLabel")}:</label>
          <select
            value={selectedGroupId}
            onChange={e => setSelectedGroupId(e.target.value)}
            className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">{t("selectGroup")}</option>
            {groups.map(g => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </select>
        </div>

        {!selectedGroupId && !groupsLoading && (
          <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
            <svg width="48" height="48" fill="none" viewBox="0 0 24 24" stroke="#cbd5e1" strokeWidth={1.5} style={{ marginBottom: 12 }}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            <p className="text-slate-500 text-sm">{t("selectGroup")}</p>
          </div>
        )}

        {loading && (
          <p className="text-slate-400 text-sm py-8">{t("loading")}</p>
        )}

        {selectedGroupId && !loading && (
          <>
            {isError && (
              <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-xl px-4 py-3">
                {t("loading")}
              </p>
            )}
            <ScheduleTaskTable
              assignments={assignments}
              filterDate={today}
              currentUserName={displayName ?? undefined}
            />
          </>
        )}
      </div>
    </AppShell>
  );
}
