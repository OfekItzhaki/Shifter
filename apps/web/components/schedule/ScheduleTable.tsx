"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { AssignmentDto } from "@/lib/api/schedule";

interface ScheduleTableProps {
  assignments: AssignmentDto[];
  filterDate?: string;
  title?: string;
}

export default function ScheduleTable({ assignments, filterDate, title }: ScheduleTableProps) {
  const t = useTranslations("schedule");
  const [search, setSearch] = useState("");

  const filtered = assignments
    .filter(a => !filterDate || a.slotStartsAt.startsWith(filterDate))
    .filter(a => !search || a.personName.toLowerCase().includes(search.toLowerCase()) || a.taskTypeName.toLowerCase().includes(search.toLowerCase()));

  const formatTime = (iso: string) =>
    new Date(iso).toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit" });

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
          placeholder="חיפוש לפי שם או משימה..."
          className="w-full border border-slate-200 rounded-xl pl-9 pr-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-500">{search ? "לא נמצאו תוצאות" : t("noAssignments")}</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/80">
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("person")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("task")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("time")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">מקור</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filtered.map(a => (
                <tr key={a.id} className="hover:bg-slate-50/60 transition-colors">
                  <td className="px-4 py-3.5 font-medium text-slate-900">{a.personName}</td>
                  <td className="px-4 py-3.5 text-slate-700">{a.taskTypeName}</td>
                  <td className="px-4 py-3.5 tabular-nums text-slate-500 text-xs">
                    {formatTime(a.slotStartsAt)}<span className="mx-1 text-slate-300">–</span>{formatTime(a.slotEndsAt)}
                  </td>
                  <td className="px-4 py-3.5">
                    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${
                      a.source === "Override" ? "bg-amber-50 text-amber-700 border border-amber-200" : "bg-slate-100 text-slate-600"
                    }`}>
                      <span className={`w-1.5 h-1.5 rounded-full ${a.source === "Override" ? "bg-amber-500" : "bg-slate-400"}`} />
                      {a.source === "Override" ? "עקיפה" : "סולבר"}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

interface ScheduleTableProps {
  assignments: AssignmentDto[];
  filterDate?: string;
  title?: string;
}

export default function ScheduleTable({ assignments, filterDate, title }: ScheduleTableProps) {
  const t = useTranslations("schedule");

  const filtered = filterDate
    ? assignments.filter((a) => a.slotStartsAt.startsWith(filterDate))
    : assignments;

  const formatTime = (iso: string) =>
    new Date(iso).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

  function sourceBadge(source: string) {
    if (source === "Override") {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200">
          <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
          Override
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-600">
        <span className="w-1.5 h-1.5 rounded-full bg-slate-400" />
        {source}
      </span>
    );
  }

  return (
    <div className="space-y-3">
      {title && (
        <h2 className="text-base font-semibold text-slate-900">{title}</h2>
      )}

      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <svg className="w-10 h-10 text-slate-300 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
          <p className="text-sm text-slate-500">{t("noAssignments")}</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/80">
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  {t("person")}
                </th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  {t("task")}
                </th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  {t("time")}
                </th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  Source
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filtered.map((a) => (
                <tr key={a.id} className="hover:bg-slate-50/60 transition-colors">
                  <td className="px-4 py-3.5 font-medium text-slate-900">{a.personName}</td>
                  <td className="px-4 py-3.5 text-slate-700">{a.taskTypeName}</td>
                  <td className="px-4 py-3.5 tabular-nums text-slate-500 text-xs">
                    {formatTime(a.slotStartsAt)}
                    <span className="mx-1 text-slate-300">–</span>
                    {formatTime(a.slotEndsAt)}
                  </td>
                  <td className="px-4 py-3.5">
                    {sourceBadge(a.source)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
