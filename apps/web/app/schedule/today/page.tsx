"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import ScheduleTable from "@/components/schedule/ScheduleTable";
import { getCurrentSchedule, ScheduleVersionDetailDto } from "@/lib/api/schedule";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { apiClient } from "@/lib/api/client";

interface GroupDto { id: string; name: string; groupTypeName: string; }

export default function TodayPage() {
  const t = useTranslations("schedule");
  const { currentSpaceId } = useSpaceStore();
  const [data, setData] = useState<ScheduleVersionDetailDto | null>(null);
  const [groups, setGroups] = useState<GroupDto[]>([]);
  const [selectedGroupId, setSelectedGroupId] = useState("");
  const [loading, setLoading] = useState(true);

  const today = new Date().toISOString().split("T")[0];
  const todayLabel = new Date().toLocaleDateString("he-IL", {
    weekday: "long", year: "numeric", month: "long", day: "numeric"
  });

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    apiClient.get(`/spaces/${currentSpaceId}/groups`)
      .then(r => setGroups(r.data))
      .finally(() => setLoading(false));
  }, [currentSpaceId]);

  useEffect(() => {
    if (!currentSpaceId || !selectedGroupId) { setData(null); return; }
    setLoading(true);
    getCurrentSchedule(currentSpaceId)
      .then(setData)
      .finally(() => setLoading(false));
  }, [currentSpaceId, selectedGroupId]);

  // Filter assignments by group members
  const filteredAssignments = data?.assignments ?? [];

  return (
    <AppShell>
      <div className="max-w-4xl space-y-6">
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">{t("title")} — היום</h1>
            <p className="text-sm text-slate-500 mt-1 capitalize">{todayLabel}</p>
          </div>
          <div className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 border border-blue-100 rounded-full">
            <span className="w-2 h-2 rounded-full bg-blue-500 animate-pulse" />
            <span className="text-xs font-medium text-blue-600">{t("live")}</span>
          </div>
        </div>

        {/* Group selector — required */}
        <div className="bg-white border border-slate-200 rounded-xl p-4 flex items-center gap-4">
          <label className="text-sm font-medium text-slate-700 whitespace-nowrap">{t("selectGroupLabel")}:</label>
          <select
            value={selectedGroupId}
            onChange={e => setSelectedGroupId(e.target.value)}
            className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">{t("selectGroup")}</option>
            {groups.map(g => (
              <option key={g.id} value={g.id}>{g.name} ({g.groupTypeName})</option>
            ))}
          </select>
        </div>

        {!selectedGroupId && (
          <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
            <svg width="48" height="48" fill="none" viewBox="0 0 24 24" stroke="#cbd5e1" strokeWidth={1.5} style={{ marginBottom: 12 }}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            <p className="text-slate-500 text-sm">{t("selectGroup")}</p>
          </div>
        )}

        {selectedGroupId && loading && (
          <p className="text-slate-400 text-sm py-8">{t("loading")}</p>
        )}

        {selectedGroupId && !loading && data && (
          <ScheduleTable assignments={filteredAssignments} filterDate={today} />
        )}

        {selectedGroupId && !loading && !data && (
          <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
            <p className="text-slate-500 text-sm">{t("noAssignments")}</p>
          </div>
        )}
      </div>
    </AppShell>
  );
}
