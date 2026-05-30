"use client";

import { useState, useEffect, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  getSpacePermissionLevels,
  getSpaceMembers,
  assignSpaceRole,
  SpacePermissionLevel,
  SpacePermissionLevelDto,
  SpaceMemberDto,
} from "@/lib/api/spaces";

interface RoleAssignmentCardProps {
  spaceId: string;
  isOwner: boolean;
}

interface MemberWithRole {
  userId: string;
  displayName: string | null;
  email: string | null;
  permissionLevel: SpacePermissionLevel;
}

/** Assignable roles — SpaceOwner is not assignable via this UI */
const ASSIGNABLE_LEVELS = [
  SpacePermissionLevel.Member,
  SpacePermissionLevel.Admin,
  SpacePermissionLevel.GroupOwner,
] as const;

/**
 * Card component for assigning permission levels to space members.
 * Displays a list of members with a dropdown to change their role.
 * Only visible to the Space Owner.
 *
 * Validates: Requirements 4.6
 */
export default function RoleAssignmentCard({
  spaceId,
  isOwner,
}: RoleAssignmentCardProps) {
  const t = useTranslations("spaces");

  const [membersWithRoles, setMembersWithRoles] = useState<MemberWithRole[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [savingUserId, setSavingUserId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);

  // Auto-dismiss toast after 4 seconds
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 4000);
    return () => clearTimeout(timer);
  }, [toast]);

  const fetchData = useCallback(async () => {
    if (!spaceId) return;
    setLoading(true);
    setError(null);
    try {
      const [members, permissions] = await Promise.all([
        getSpaceMembers(spaceId),
        getSpacePermissionLevels(spaceId),
      ]);

      const permMap = new Map<string, SpacePermissionLevel>();
      permissions.forEach((p: SpacePermissionLevelDto) => {
        permMap.set(p.userId, p.permissionLevel);
      });

      const combined: MemberWithRole[] = members.map((m: SpaceMemberDto) => ({
        userId: m.userId,
        displayName: m.displayName,
        email: m.email,
        permissionLevel: permMap.get(m.userId) ?? SpacePermissionLevel.Member,
      }));

      // Sort: higher permission levels first, then alphabetically
      combined.sort((a, b) => {
        if (b.permissionLevel !== a.permissionLevel) {
          return b.permissionLevel - a.permissionLevel;
        }
        return (a.displayName ?? "").localeCompare(b.displayName ?? "");
      });

      setMembersWithRoles(combined);
    } catch {
      setError(t("roleAssignment.loadError"));
    } finally {
      setLoading(false);
    }
  }, [spaceId, t]);

  useEffect(() => {
    if (isOwner) {
      fetchData();
    }
  }, [isOwner, fetchData]);

  // Permission gate: hide entirely for non-owners
  if (!isOwner) return null;

  const handleRoleChange = async (userId: string, newLevel: SpacePermissionLevel) => {
    setSavingUserId(userId);
    setToast(null);
    try {
      await assignSpaceRole(spaceId, userId, newLevel);
      // Update local state
      setMembersWithRoles((prev) =>
        prev.map((m) =>
          m.userId === userId ? { ...m, permissionLevel: newLevel } : m
        )
      );
      setToast({ type: "success", message: t("roleAssignment.saved") });
    } catch {
      setToast({ type: "error", message: t("roleAssignment.saveError") });
    } finally {
      setSavingUserId(null);
    }
  };

  const getLevelLabel = (level: SpacePermissionLevel): string => {
    switch (level) {
      case SpacePermissionLevel.SpaceOwner:
        return t("roleAssignment.levels.spaceOwner");
      case SpacePermissionLevel.GroupOwner:
        return t("roleAssignment.levels.groupOwner");
      case SpacePermissionLevel.Admin:
        return t("roleAssignment.levels.admin");
      case SpacePermissionLevel.Member:
      default:
        return t("roleAssignment.levels.member");
    }
  };

  if (loading) {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          {t("roleAssignment.title")}
        </h2>
        <div className="flex items-center justify-center py-4 text-slate-500 dark:text-slate-400 text-sm">
          {t("roleAssignment.loading")}
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          {t("roleAssignment.title")}
        </h2>
        <div className="flex flex-col items-center gap-3 py-4">
          <p className="text-sm text-slate-500 dark:text-slate-400">{error}</p>
          <button
            onClick={fetchData}
            className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 text-white font-semibold text-sm transition-colors"
          >
            {t("roleAssignment.retry")}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-1">
        {t("roleAssignment.title")}
      </h2>
      <p className="text-xs text-slate-500 dark:text-slate-400 mb-4">
        {t("roleAssignment.description")}
      </p>

      <div className="space-y-2">
        {membersWithRoles.map((member) => {
          const isSpaceOwner = member.permissionLevel === SpacePermissionLevel.SpaceOwner;
          const isSaving = savingUserId === member.userId;

          return (
            <div
              key={member.userId}
              className="flex items-center gap-3 p-2 rounded-lg"
            >
              {/* Avatar */}
              <div className="w-8 h-8 rounded-full bg-sky-500 flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
                {(member.displayName ?? "?").charAt(0).toUpperCase()}
              </div>

              {/* Name & email */}
              <div className="flex-1 min-w-0">
                <div className="text-sm font-medium text-slate-900 dark:text-white truncate">
                  {member.displayName ?? "—"}
                </div>
                {member.email && (
                  <div className="text-xs text-slate-500 dark:text-slate-400 truncate">
                    {member.email}
                  </div>
                )}
              </div>

              {/* Role dropdown or badge */}
              {isSpaceOwner ? (
                <span className="text-xs font-medium px-2.5 py-1 rounded-full border bg-amber-50 dark:bg-amber-900/20 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-700">
                  {getLevelLabel(SpacePermissionLevel.SpaceOwner)}
                </span>
              ) : (
                <select
                  value={member.permissionLevel}
                  onChange={(e) =>
                    handleRoleChange(member.userId, e.target.value as SpacePermissionLevel)
                  }
                  disabled={isSaving}
                  aria-label={t("roleAssignment.selectLabel", { name: member.displayName ?? "" })}
                  className="px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-xs focus:outline-none focus:border-sky-500 disabled:opacity-50"
                >
                  {ASSIGNABLE_LEVELS.map((level) => (
                    <option key={level} value={level}>
                      {getLevelLabel(level)}
                    </option>
                  ))}
                </select>
              )}
            </div>
          );
        })}

        {membersWithRoles.length === 0 && (
          <p className="text-sm text-slate-500 dark:text-slate-400 text-center py-4">
            {t("roleAssignment.noMembers")}
          </p>
        )}
      </div>

      {/* Toast notification */}
      {toast && (
        <div
          role={toast.type === "error" ? "alert" : "status"}
          aria-live={toast.type === "error" ? "assertive" : "polite"}
          className={`mt-3 text-xs px-3 py-2 rounded-lg ${
            toast.type === "success"
              ? "bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300 border border-green-200 dark:border-green-700"
              : "bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-700"
          }`}
        >
          {toast.message}
        </div>
      )}
    </div>
  );
}
