"use client";

import { useState, useEffect, useCallback } from "react";
import { useTranslations, useLocale } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import {
  getSpaceDetail,
  updateSpace,
  getSpaceMembers,
  SpaceDetailDto,
  SpaceMemberDto,
} from "@/lib/api/spaces";
import SpaceBillingCard from "@/components/billing/SpaceBillingCard";
import InviteCodeCard from "@/components/spaces/InviteCodeCard";
import RoleAssignmentCard from "@/components/spaces/RoleAssignmentCard";
import ManagementTimeoutCard from "@/components/spaces/ManagementTimeoutCard";
import DangerZoneCard from "@/components/spaces/DangerZoneCard";
import { isRtl as isRtlLocale } from "@/lib/i18n/locales";

export default function SpaceSettingsPage() {
  const t = useTranslations("spaces");
  const locale = useLocale();
  const isRtl = isRtlLocale(locale);
  const { currentSpaceId, setCurrentSpace } = useSpaceStore();
  const { userId } = useAuthStore();

  const [space, setSpace] = useState<SpaceDetailDto | null>(null);
  const [members, setMembers] = useState<SpaceMemberDto[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);

  useEffect(() => {
    if (!currentSpaceId) return;
    setLoading(true);
    setLoadError(false);
    Promise.all([
      getSpaceDetail(currentSpaceId),
      getSpaceMembers(currentSpaceId),
    ]).then(([detail, memberList]) => {
      setSpace(detail);
      setMembers(memberList);
      setName(detail.name);
      setDescription(detail.description ?? "");
    }).catch(() => { setLoadError(true); }).finally(() => setLoading(false));
  }, [currentSpaceId]);

  const handleSave = useCallback(async () => {
    if (!currentSpaceId || !space) return;
    setSaving(true);
    setSaved(false);
    try {
      await updateSpace(currentSpaceId, { name: name.trim(), description: description.trim() || null, locale: space.locale });
      setSaved(true);
      // Update the space store so the sidebar reflects the new name
      setCurrentSpace(currentSpaceId, name.trim());
    } catch { /* handled by interceptor */ }
    finally { setSaving(false); }
  }, [currentSpaceId, name, description, space, setCurrentSpace]);

  if (loading) {
    return (
      <AppShell>
        <div className="flex items-center justify-center py-20 text-slate-500 dark:text-slate-400">
          Loading...
        </div>
      </AppShell>
    );
  }

  if (!space) {
    // API error — the global OfflineBanner handles server-down messaging
    if (loadError) {
      return (
        <AppShell>
          <div className="w-full max-w-md mx-auto py-16">
            <p className="text-sm text-slate-400 text-center">{t("loading")}</p>
          </div>
        </AppShell>
      );
    }

    // No currentSpaceId at all
    return (
      <AppShell>
        <div className="w-full max-w-md mx-auto py-16 text-center space-y-4">
          <p className="text-slate-500 dark:text-slate-400">
            {t("noSpace")}
          </p>
          <a
            href="/onboarding"
            className="inline-block px-5 py-2.5 rounded-xl bg-sky-500 hover:bg-sky-600 text-white font-semibold text-sm transition-colors no-underline"
          >
            {t("createNew")}
          </a>
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div style={{ maxWidth: 720, direction: isRtl ? "rtl" : "ltr" }} className="w-full">
        {/* Header */}
        <div style={{ marginBottom: "1.5rem" }}>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white" style={{ margin: "0 0 0.25rem" }}>
            {t("settings")}
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400" style={{ margin: 0 }}>
            {t("memberCount", { count: space.memberCount })} · {t("groupCount", { count: space.groupCount })}
          </p>
        </div>

        <div className="space-y-4">
          {/* Name & Description */}
          <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
            {!space.isOwner && (
              <p className="text-xs text-amber-600 dark:text-amber-400 mb-3">{t("ownerOnly")}</p>
            )}
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                  {t("nameLabel")}
                </label>
                <input
                  type="text"
                  value={name}
                  onChange={e => { setName(e.target.value); setSaved(false); }}
                  disabled={!space.isOwner}
                  maxLength={100}
                  className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                  {t("descriptionLabel")}
                </label>
                <textarea
                  value={description}
                  onChange={e => { setDescription(e.target.value); setSaved(false); }}
                  disabled={!space.isOwner}
                  maxLength={500}
                  rows={3}
                  placeholder={t("descriptionPlaceholder")}
                  className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 resize-none focus:outline-none focus:border-sky-500"
                />
              </div>
              {space.isOwner && (
                <button
                  onClick={handleSave}
                  disabled={saving || name.trim().length < 2}
                  className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600 text-white font-semibold text-sm transition-colors"
                >
                  {saving ? t("saving") : saved ? t("saved") : t("save")}
                </button>
              )}
            </div>
          </div>

          {/* Invite Code */}
          <InviteCodeCard
            spaceId={currentSpaceId!}
            inviteCode={space.inviteCode}
            isOwner={space.isOwner}
            onCodeRegenerated={(newCode) =>
              setSpace(prev => prev ? { ...prev, inviteCode: newCode } : prev)
            }
          />

          {/* Members */}
          <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
              {t("members")} ({members.length})
            </h2>
            <div className="space-y-2">
              {members.map(member => (
                <div
                  key={member.userId}
                  className="flex items-center gap-3 p-2 rounded-lg"
                >
                  <div className="w-8 h-8 rounded-full bg-sky-500 flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
                    {(member.displayName ?? "?").charAt(0).toUpperCase()}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="text-sm font-medium text-slate-900 dark:text-white truncate">
                      {member.displayName ?? "—"}
                    </div>
                    <div className="text-xs text-slate-500 dark:text-slate-400">
                      {member.email ?? ""}
                    </div>
                  </div>
                  <div className="text-xs text-slate-400 dark:text-slate-500">
                    {new Date(member.joinedAt).toLocaleDateString()}
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Role Assignment — permission-gated to space owner */}
          <RoleAssignmentCard
            spaceId={currentSpaceId!}
            isOwner={space.isOwner}
          />

          {/* Management Timeout — permission-gated to space owner */}
          <ManagementTimeoutCard
            spaceId={currentSpaceId!}
            currentTimeout={space.managementTimeoutMinutes}
            isOwner={space.isOwner}
          />

          {/* Billing — permission-gated to space owner (BillingManage) */}
          <SpaceBillingCard
            spaceId={currentSpaceId!}
            hasBillingPermission={space.isOwner}
          />

          {/* Danger Zone — permission-gated to space owner */}
          <DangerZoneCard
            spaceId={currentSpaceId!}
            isOwner={space.isOwner}
            members={members}
            currentOwnerId={userId ?? ""}
          />
        </div>
      </div>
    </AppShell>
  );
}
