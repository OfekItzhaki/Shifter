"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import {
  getHomeLeaveSchedule,
  type HomeLeaveScheduleEntry,
} from "@/lib/api/homeLeave";

interface Props {
  spaceId: string;
  groupId: string;
}

export default function HomeLeaveScheduleTable({ spaceId, groupId }: Props) {
  const t = useTranslations("groups.home_leave_schedule");
  const { fDateShort } = useDateFormat();
  const [entries, setEntries] = useState<HomeLeaveScheduleEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    getHomeLeaveSchedule(spaceId, groupId)
      .then((data) => {
        if (!cancelled) setEntries(data);
      })
      .catch(() => {
        if (!cancelled) setError(t("loadError"));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [spaceId, groupId]);

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-slate-400 text-sm py-4">
        <svg
          className="animate-spin h-4 w-4 text-blue-400"
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
        {t("loading")}
      </div>
    );
  }

  if (error) {
    return <p className="text-sm text-red-600">{error}</p>;
  }

  if (entries.length === 0) {
    return (
      <p className="text-sm text-slate-400 py-2">{t("noEntries")}</p>
    );
  }

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl overflow-hidden">
      <div className="px-4 py-3 border-b border-slate-100 dark:border-slate-700">
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
          {t("title")}
        </h3>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-slate-50 dark:bg-slate-750 text-slate-500 dark:text-slate-400">
              <th className="text-start px-4 py-2.5 font-medium">{t("name")}</th>
              <th className="text-start px-4 py-2.5 font-medium">{t("leaveStart")}</th>
              <th className="text-start px-4 py-2.5 font-medium">{t("returnDate")}</th>
              <th className="text-start px-4 py-2.5 font-medium">{t("status")}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-700">
            {entries.map((entry, idx) => (
              <tr key={`${entry.personId}-${idx}`} className="hover:bg-slate-50 dark:hover:bg-slate-750">
                <td className="px-4 py-2.5 text-slate-700 dark:text-slate-200">
                  {entry.personName}
                </td>
                <td className="px-4 py-2.5 text-slate-600 dark:text-slate-300">
                  {fDateShort(entry.startsAt)}
                </td>
                <td className="px-4 py-2.5 text-slate-600 dark:text-slate-300">
                  {fDateShort(entry.endsAt)}
                </td>
                <td className="px-4 py-2.5">
                  <StatusBadge status={entry.status} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: HomeLeaveScheduleEntry["status"] }) {
  const t = useTranslations("groups.home_leave_schedule");

  const config = {
    active: {
      label: t("statusActive"),
      className: "bg-emerald-100 text-emerald-700 border-emerald-200",
    },
    upcoming: {
      label: t("statusUpcoming"),
      className: "bg-blue-100 text-blue-700 border-blue-200",
    },
    completed: {
      label: t("statusCompleted"),
      className: "bg-slate-100 text-slate-500 border-slate-200",
    },
  };

  const { label, className } = config[status];

  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${className}`}
    >
      {label}
    </span>
  );
}
