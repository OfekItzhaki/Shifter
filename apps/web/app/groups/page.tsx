"use client";

import { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useRouter, useSearchParams } from "next/navigation";
import { getAvatarColor, getAvatarLetter } from "@/lib/utils/groupAvatar";
import { useGroups, useDeletedGroups, useCreateGroup, useRestoreGroup } from "@/lib/query/hooks/useGroups";

export default function GroupsPage() {
  const t = useTranslations("groups");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const { currentSpaceId } = useSpaceStore();
  const router = useRouter();
  const searchParams = useSearchParams();
  const deletedRef = useRef<HTMLDivElement>(null);

  const [newGroupName, setNewGroupName] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);

  const { data: groups = [], isLoading: loading } = useGroups(currentSpaceId);
  const { data: deletedGroups = [], isLoading: deletedLoading } = useDeletedGroups(currentSpaceId);
  const createGroup = useCreateGroup(currentSpaceId);
  const restoreGroup = useRestoreGroup(currentSpaceId);

  // Scroll to deleted section if ?tab=deleted
  useEffect(() => {
    if (searchParams.get("tab") === "deleted" && deletedRef.current && !deletedLoading) {
      setTimeout(() => deletedRef.current?.scrollIntoView({ behavior: "smooth" }), 300);
    }
  }, [searchParams, deletedLoading]);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newGroupName.trim()) return;
    setCreateError(null);
    try {
      await createGroup.mutateAsync(newGroupName.trim());
      setNewGroupName("");
    } catch (err: unknown) {
      setCreateError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ||
        tErrors("errorCreateGroup")
      );
    }
  }

  async function handleRestore(id: string) {
    if (!currentSpaceId) return;
    try {
      await restoreGroup.mutateAsync(id);
    } catch { /* non-fatal */ }
  }

  return (
    <AppShell>
      <div className="max-w-4xl space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">{t("title")}</h1>
            <p className="text-sm text-slate-500 mt-1">{t("subtitle")}</p>
          </div>
        </div>

        {/* Create group */}
        <form onSubmit={handleCreate} className="flex gap-2 max-w-sm">
          <input
            value={newGroupName}
            onChange={e => setNewGroupName(e.target.value)}
            placeholder={t("newGroupPlaceholder")}
            className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button
            type="submit"
            disabled={createGroup.isPending}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 whitespace-nowrap"
          >
            {createGroup.isPending ? "..." : t("newGroup")}
          </button>
        </form>

        {createError && <p className="text-sm text-red-600">{createError}</p>}

        {/* Active groups */}
        {loading ? (
          <p className="text-slate-400 text-sm py-8">{tCommon("loading")}</p>
        ) : groups.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
            <svg className="w-10 h-10 text-slate-200 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            <p className="text-slate-400 text-sm">{t("noGroups")}</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {groups.map(g => (
              <button
                key={g.id}
                onClick={() => router.push(`/groups/${g.id}`)}
                className="text-start bg-white border border-slate-200 rounded-2xl p-5 hover:border-blue-300 hover:shadow-md transition-all group"
              >
                <div className="flex items-start justify-between mb-3">
                  <div
                    className="w-10 h-10 rounded-xl flex items-center justify-center text-white text-base font-bold flex-shrink-0"
                    style={{ background: getAvatarColor(g.name) }}
                  >
                    {getAvatarLetter(g.name)}
                  </div>
                  <svg className="w-4 h-4 text-slate-300 group-hover:text-blue-400 transition-colors mt-1" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                  </svg>
                </div>
                <h2 className="text-base font-semibold text-slate-900">{g.name}</h2>
                <p className="text-xs text-slate-400 mt-1">{g.memberCount} {t("members")}</p>
              </button>
            ))}
          </div>
        )}

        {/* Deleted groups */}
        <div ref={deletedRef} className="border-t border-slate-100 pt-6">
          <h2 className="text-base font-semibold text-slate-700 mb-3">{t("deletedGroups")}</h2>
          {deletedLoading ? (
            <p className="text-sm text-slate-400">{tCommon("loading")}</p>
          ) : deletedGroups.length === 0 ? (
            <p className="text-sm text-slate-400">{t("noDeletedGroups")}</p>
          ) : (
            <div className="space-y-2">
              {deletedGroups.map(g => (
                <div key={g.id} className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
                  <span className="text-sm text-slate-700">{g.name}</span>
                  <button
                    onClick={() => handleRestore(g.id)}
                    disabled={restoreGroup.isPending}
                    className="text-xs text-blue-600 border border-blue-200 hover:bg-blue-50 px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50"
                  >
                    {t("restore")}
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </AppShell>
  );
}
