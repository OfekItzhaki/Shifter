"use client";

import { useState } from "react";
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
 * - Cannot select a group that is a child of this group (would make it both parent and child)
 * - A group that is already a parent of other groups cannot become a child
 */
export default function LinkedGroupSelector({ groupId, currentParentId, allGroups, onUpdate }: Props) {
  const tLinked = useTranslations("groups.linkedGroup");
  const { currentSpaceId } = useSpaceStore();
  const [saving, setSaving] = useState(false);

  // Determine which groups are already parents (have children)
  const groupsWithChildren = new Set(
    allGroups
      .filter(g => g.parentGroupId !== null)
      .map(g => g.parentGroupId!)
  );

  // This group is a parent if any other group has it as parentGroupId
  const currentGroupIsParent = groupsWithChildren.has(groupId);

  // Filter eligible parent groups
  const eligibleParents = allGroups.filter(g => {
    if (g.id === groupId) return false; // Can't be own parent
    if (g.parentGroupId !== null) return false; // Can't select a child group as parent (single-level)
    return true;
  });

  // Groups that would be disabled (shown but not selectable) — groups that are parents of other groups
  // A group that is already a parent cannot become a child (single-level hierarchy)
  const disabledParentIds = new Set(
    eligibleParents
      .filter(g => groupsWithChildren.has(g.id))
      .map(g => g.id)
  );

  async function handleChange(parentId: string | null) {
    if (!currentSpaceId) return;
    if (parentId && disabledParentIds.has(parentId)) return; // Prevent selecting disabled options
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

  async function handleUnlink() {
    if (!currentSpaceId || !currentParentId) return;
    setSaving(true);
    try {
      await unlinkParentGroup(currentSpaceId, groupId);
      onUpdate();
    } catch { /* handled by interceptor */ }
    finally { setSaving(false); }
  }

  const currentParentName = currentParentId
    ? allGroups.find(g => g.id === currentParentId)?.name ?? "—"
    : null;

  // If this group is already a parent, it cannot become a child
  if (currentGroupIsParent && !currentParentId) {
    return (
      <div className="space-y-2">
        <label className="block text-xs font-medium text-slate-600 dark:text-slate-300">
          {tLinked("title")}
        </label>
        <p className="text-xs text-amber-600 dark:text-amber-400">
          {tLinked("cannotBeChild")}
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <label className="block text-xs font-medium text-slate-600 dark:text-slate-300">
        {tLinked("title")}
      </label>
      <select
        value={currentParentId ?? ""}
        onChange={e => handleChange(e.target.value || null)}
        disabled={saving}
        className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-sky-500 disabled:opacity-50"
      >
        <option value="">{tLinked("none")}</option>
        {eligibleParents.map(g => (
          <option
            key={g.id}
            value={g.id}
            disabled={disabledParentIds.has(g.id)}
          >
            {g.name}{disabledParentIds.has(g.id) ? ` (${tLinked("alreadyParent")})` : ""}
          </option>
        ))}
      </select>
      {currentParentId && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {tLinked("linkedTo")}: <span className="font-medium">{currentParentName}</span>
          </p>
          <button
            type="button"
            onClick={handleUnlink}
            disabled={saving}
            className="text-xs text-red-600 dark:text-red-400 hover:text-red-700 dark:hover:text-red-300 border border-red-200 dark:border-red-800 hover:bg-red-50 dark:hover:bg-red-900/30 px-2.5 py-1 rounded-lg transition-colors disabled:opacity-50"
          >
            {tLinked("unlink")}
          </button>
        </div>
      )}
    </div>
  );
}
