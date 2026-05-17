"use client";

import { useCallback, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { useSandboxStore, PersonEligibilityDto } from "@/lib/store/sandboxStore";

interface SandboxMembersTabProps {
  memberExclusions: Set<string>;
  baseline: ReturnType<typeof useSandboxStore.getState>["baseline"];
}

/**
 * SandboxMembersTab — Members tab content for the sandbox settings panel.
 *
 * Displays all members from the baseline with toggle controls to include/exclude
 * each member from the simulation. Excluded members are visually dimmed and
 * removed from the override payload's People list.
 *
 * Requirements: 4.1, 4.2, 4.3, 4.4
 */
export default function SandboxMembersTab({ memberExclusions, baseline }: SandboxMembersTabProps) {
  const t = useTranslations("sandbox");
  const toggleMember = useSandboxStore((s) => s.toggleMember);
  const [searchQuery, setSearchQuery] = useState("");

  const people: PersonEligibilityDto[] = baseline?.people ?? [];
  const totalMembers = people.length;
  const excludedCount = memberExclusions.size;
  const activeCount = totalMembers - excludedCount;

  const filteredPeople = useMemo(() => {
    if (!searchQuery.trim()) return people;
    const query = searchQuery.toLowerCase();
    return people.filter(
      (p) =>
        p.personId.toLowerCase().includes(query) ||
        p.roleIds.some((r) => r.toLowerCase().includes(query)) ||
        p.qualificationIds.some((q) => q.toLowerCase().includes(query))
    );
  }, [people, searchQuery]);

  const handleToggle = useCallback(
    (personId: string) => {
      toggleMember(personId);
    },
    [toggleMember]
  );

  return (
    <div className="space-y-3">
      {/* Header with active/total count */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
          {t("tabs.members")}
        </h3>
        <span className="text-xs text-slate-500 dark:text-slate-400">
          {activeCount}/{totalMembers} {t("active")}
        </span>
      </div>

      {/* Search input */}
      {totalMembers > 5 && (
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder={t("members.searchPlaceholder")}
          className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-none focus:ring-1 focus:ring-blue-400"
        />
      )}

      {/* Member list */}
      {totalMembers === 0 ? (
        <p className="text-xs text-slate-400 dark:text-slate-500">
          {t("members.noMembers")}
        </p>
      ) : (
        <div className="space-y-1 max-h-[calc(100vh-320px)] overflow-y-auto">
          {filteredPeople.map((person) => {
            const isExcluded = memberExclusions.has(person.personId);
            return (
              <MemberRow
                key={person.personId}
                person={person}
                isExcluded={isExcluded}
                onToggle={handleToggle}
                t={t}
              />
            );
          })}
          {filteredPeople.length === 0 && searchQuery && (
            <p className="text-xs text-slate-400 dark:text-slate-500 text-center py-2">
              {t("members.noResults")}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

// ── Member Row Component ──────────────────────────────────────────────────────

function MemberRow({
  person,
  isExcluded,
  onToggle,
  t,
}: {
  person: PersonEligibilityDto;
  isExcluded: boolean;
  onToggle: (personId: string) => void;
  t: ReturnType<typeof useTranslations>;
}) {
  return (
    <label
      className={`flex items-center gap-3 px-3 py-2 rounded-xl border cursor-pointer transition-colors ${
        isExcluded
          ? "border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/50 opacity-50"
          : "border-blue-200 dark:border-blue-800 bg-blue-50/50 dark:bg-blue-900/20 hover:bg-blue-50 dark:hover:bg-blue-900/30"
      }`}
    >
      <input
        type="checkbox"
        checked={!isExcluded}
        onChange={() => onToggle(person.personId)}
        className="accent-blue-500 w-4 h-4 rounded"
      />
      <div className={`flex-1 min-w-0 ${isExcluded ? "line-through" : ""}`}>
        <span className="text-sm text-slate-800 dark:text-slate-200 block truncate">
          {person.personId}
        </span>
        {(person.roleIds.length > 0 || person.qualificationIds.length > 0) && (
          <div className="flex flex-wrap gap-1 mt-0.5">
            {person.roleIds.map((roleId) => (
              <span
                key={roleId}
                className="inline-block px-1.5 py-0.5 text-[10px] font-medium rounded bg-indigo-100 dark:bg-indigo-900/40 text-indigo-700 dark:text-indigo-300"
              >
                {roleId}
              </span>
            ))}
            {person.qualificationIds.map((qualId) => (
              <span
                key={qualId}
                className="inline-block px-1.5 py-0.5 text-[10px] font-medium rounded bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300"
              >
                {qualId}
              </span>
            ))}
          </div>
        )}
      </div>
      <span
        className={`text-[10px] font-medium px-1.5 py-0.5 rounded ${
          isExcluded
            ? "bg-red-100 dark:bg-red-900/40 text-red-600 dark:text-red-400"
            : "bg-green-100 dark:bg-green-900/40 text-green-600 dark:text-green-400"
        }`}
      >
        {isExcluded ? t("members.excluded") : t("members.included")}
      </span>
    </label>
  );
}
