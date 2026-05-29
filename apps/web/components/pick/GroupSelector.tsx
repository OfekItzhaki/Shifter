"use client";

import { useTranslations } from "next-intl";
import type { GroupWithMemberCountDto } from "@/lib/api/groups";

export interface GroupSelectorProps {
  groups: GroupWithMemberCountDto[];
  onSelect: (groupId: string, groupName: string) => void;
}

/**
 * Displays a list of self-service group cards for the shift picker.
 * Each card shows the group name and member count.
 * When no groups are available, shows a localized empty-state message.
 */
export default function GroupSelector({ groups, onSelect }: GroupSelectorProps) {
  const t = useTranslations("pick");

  if (groups.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 bg-white rounded-xl border border-slate-200">
        <div className="flex items-center justify-center w-12 h-12 rounded-full bg-slate-100 mb-3">
          <svg
            width={24}
            height={24}
            viewBox="0 0 24 24"
            fill="none"
            className="text-slate-400"
            stroke="currentColor"
            strokeWidth={1.5}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
            <circle cx="9" cy="7" r="4" />
            <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
            <path d="M16 3.13a4 4 0 0 1 0 7.75" />
          </svg>
        </div>
        <p className="text-base text-slate-600">{t("noGroups")}</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      {groups.map((group) => (
        <button
          key={group.id}
          type="button"
          onClick={() => onSelect(group.id, group.name)}
          className="flex items-center justify-between w-full min-h-[44px] px-4 py-3 bg-white border border-slate-200 rounded-xl text-start hover:border-sky-300 hover:bg-sky-50 transition-colors focus:outline-none focus:ring-2 focus:ring-sky-500 focus:ring-offset-2"
        >
          <span className="text-base font-medium text-slate-900">
            {group.name}
          </span>
          <span className="text-sm text-slate-500">
            {t("memberCount", { count: group.memberCount })}
          </span>
        </button>
      ))}
    </div>
  );
}
