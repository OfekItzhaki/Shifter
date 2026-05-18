"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalTime } from "@/lib/utils/formatTime";
import { AssignmentDto } from "@/lib/api/schedule";

/** Task type name used for home-leave assignments from the solver */
const HOME_LEAVE_TASK_TYPE = "home_leave";

/** Display label for home-leave assignments */
const HOME_LEAVE_LABEL = "בבית";

/** Check if a task type represents a home-leave assignment */
function isHomeLeaveTask(taskTypeName: string): boolean {
  return taskTypeName === HOME_LEAVE_TASK_TYPE;
}

interface ScheduleTableProps {
  assignments: AssignmentDto[];
  filterDate?: string;
  title?: string;
}

export default function ScheduleTable({ assignments, filterDate, title }: ScheduleTableProps) {
  const t = useTranslations("schedule");
  const tCommon = useTranslations("common");
  const timezoneId = useAuthStore(s => s.timezoneId);
  const [search, setSearch] = useState("");

  const filtered = assignments
    .filter(a => !filterDate || a.slotStartsAt.startsWith(filterDate))
    .filter(a => !search ||
      a.personName.toLowerCase().includes(search.toLowerCase()) ||
      a.taskTypeName.toLowerCase().includes(search.toLowerCase()));

  const formatTime = (iso: string) => formatLocalTime(iso, timezoneId, "24h");

  return (
    <div className="space-y-3">
      {title && <h2 className="text-base font-semibold text-slate-900">{title}</h2>}

      {/* Search */}
      <div className="relative">
        <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="#94a3b8" strokeWidth={2}
          style={{ position: "absolute", left: 12, top: "50%", transform: "translateY(-50%)" }}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder={t("filterByName")}
          className="w-full border border-slate-200 rounded-xl pl-9 pr-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-500">{search ? tCommon("noResults") : t("noAssignments")}</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/80">
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("person")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("task")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("time")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("source")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filtered.map(a => {
                const isHomeLeave = isHomeLeaveTask(a.taskTypeName);
                return (
                <tr key={a.id} className={`transition-colors ${isHomeLeave ? "bg-emerald-50/40 hover:bg-emerald-50/80" : "hover:bg-slate-50/60"}`}>
                  <td className={`px-4 py-3.5 font-medium ${isHomeLeave ? "text-emerald-800" : "text-slate-900"}`}>{a.personName}</td>
                  <td className={`px-4 py-3.5 ${isHomeLeave ? "text-emerald-700 font-medium" : "text-slate-700"}`}>
                    {isHomeLeave ? (
                      <span className="inline-flex items-center gap-1.5">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="flex-shrink-0">
                          <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                        </svg>
                        {HOME_LEAVE_LABEL}
                      </span>
                    ) : a.taskTypeName}
                  </td>
                  <td className={`px-4 py-3.5 tabular-nums text-xs ${isHomeLeave ? "text-emerald-600" : "text-slate-500"}`}>
                    {formatTime(a.slotStartsAt)}<span className={`mx-1 ${isHomeLeave ? "text-emerald-300" : "text-slate-300"}`}>–</span>{formatTime(a.slotEndsAt)}
                  </td>
                  <td className="px-4 py-3.5">
                    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${
                      a.source === "Override"
                        ? "bg-amber-50 text-amber-700 border border-amber-200"
                        : isHomeLeave
                        ? "bg-emerald-100 text-emerald-700 border border-emerald-200"
                        : "bg-slate-100 text-slate-600"
                    }`}>
                      <span className={`w-1.5 h-1.5 rounded-full ${a.source === "Override" ? "bg-amber-500" : isHomeLeave ? "bg-emerald-500" : "bg-slate-400"}`} />
                      {a.source === "Override" ? t("sourceOverride") : t("sourceSolver")}
                    </span>
                  </td>
                </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
