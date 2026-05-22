"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { linkParentGroup, unlinkParentGroup } from "@/lib/api/spaces";

interface GroupOption {
  id: string;
  name: string;
  parentGroupId: string | null;
}

interface Props {
  groupId: string;
  currentParentId: string | null;
  allGroups: GroupOption[];
  onUpdate: () => void;
}

/**
 * Dropdown to select/remove a parent group for the current group.
 * Rules:
 * - Cannot select itself
 * - Cannot select a group that already has a parent (single-level only)
 * - Cannot select a group that is a child of this group
 */
export default function LinkedGroupSelector({ groupId, currentParentId, allGroups, onUpdate }: Props) {
  const t = useTranslations("groups.settings_tab");
  const { currentSpaceId } = useSpaceStore();
  const [saving, setSaving] = useState(false);

  // Filter eligible parent groups
  const eligibleParents = allGroups.filter(g => {
    if (g.id === groupId) return false; // Can't be own parent
    if (g.parentGroupId !== null) return false; // Can't select a child group as parent
    // Can't select a group that has this group as parent (would make it both parent and child)
    const isChildOfCurrent = allGroups.some(child => child.parentGroupId === groupId && child.id === g.id);
    if (isChildOfCurrent) return false;
    return true;
  });

  async function handleChange(parentId: string | null) {
    if (!currentSpaceId) return;
    setSaving(true);
    try {
      if (parentId) {
        await linkParentGroup(currentSpaceId, groupId, parentId);
      } else {
        await unlinkParentGroup(currentSpaceId, groupId);
      }
      onUpdate();
    } catch { /* handled by interceptor */ }
    finally { setSaving(false); }
  }

  const currentParentName = currentParentId
    ? allGroups.find(g => g.id === currentParentId)?.name ?? "—"
    : null;

  return (
    <div className="space-y-2">
      <label className="block text-xs font-medium text-slate-600 dark:text-slate-300">
        Parent Group
      </label>
      <select
        value={currentParentId ?? ""}
        onChange={e => handleChange(e.target.value || null)}
        disabled={saving}
        className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-sky-500 disabled:opacity-50"
      >
        <option value="">None (independent group)</option>
        {eligibleParents.map(g => (
          <option key={g.id} value={g.id}>{g.name}</option>
        ))}
      </select>
      {currentParentId && (
        <p className="text-xs text-slate-500 dark:text-slate-400">
          Linked to: <span className="font-medium">{currentParentName}</span>
        </p>
      )}
    </div>
  );
}
