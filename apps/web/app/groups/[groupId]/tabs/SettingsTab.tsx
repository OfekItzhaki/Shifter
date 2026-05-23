"use client";

import { useState, useEffect } from "react";
import { useTranslations, useLocale } from "next-intl";
import type { GroupMemberDto } from "@/lib/api/groups";
import { getJoinCode, regenerateJoinCode } from "@/lib/api/groups";
import SmartImportModal from "@/components/SmartImportModal";
import HomeLeaveConfigPanel from "@/components/home-leave/HomeLeaveConfigPanel";
import LinkedGroupSelector from "@/components/groups/LinkedGroupSelector";
import { FEATURE_VISIBILITY_MAP, type GroupTemplateType } from "@/lib/utils/templateFeatureConfig";

interface DraftVersion { id: string; status: string; }

interface Props {
  isAdmin: boolean;
  spaceId: string;
  groupId: string;
  templateType?: string | null;
  newGroupName: string;
  renameSaving: boolean;
  renameError: string | null;
  solverHorizon: number;
  savingSettings: boolean;
  settingsError: string | null;
  settingsSaved: boolean;
  solverStartDateTime: string | null;
  autoPublish: boolean;
  isClosedBase: boolean;
  minRestBetweenShiftsHours: number;
  managementTimeoutMinutes: number;
  allowMembersViewHistory: boolean;
  allowMembersViewStats: boolean;
  solverPolling: boolean;
  solverStatus: string | null;
  solverError: string | null;
  draftVersion: DraftVersion | null;
  members: GroupMemberDto[];
  transferPersonId: string;
  transferSaving: boolean;
  transferError: string | null;
  hasPendingTransfer: boolean;
  cancelTransferSaving: boolean;
  showDeleteConfirm: boolean;
  deleteSaving: boolean;
  deleteError: string | null;
  // Roles
  groupRoles?: never;
  groupRolesLoading?: never;
  onCreateRole?: never;
  onUpdateRole?: never;
  onDeactivateRole?: never;
  onGroupNameChange: (v: string) => void;
  onRenameGroup: () => void;
  onSolverHorizonChange: (v: number) => void;
  onSolverStartDateTimeChange: (v: string | null) => void;
  onAutoPublishChange: (v: boolean) => void;
  onClosedBaseChange: (v: boolean) => void;
  onMinRestBetweenShiftsChange: (v: number) => void;
  onManagementTimeoutChange: (v: number) => void;
  onAllowMembersViewHistoryChange: (v: boolean) => void;
  onAllowMembersViewStatsChange: (v: boolean) => void;
  onSaveSettings: () => void;
  onTriggerSolver: (startTime?: string) => void;
  onOpenDraftModal: () => void;
  onRestoreGroup?: never;
  onTransferPersonChange: (v: string) => void;
  onInitiateTransfer: () => void;
  onCancelTransfer: () => void;
  onShowDeleteConfirm: (v: boolean) => void;
  onDeleteGroup: () => void;
}

export default function SettingsTab({
  isAdmin, spaceId, groupId, templateType, newGroupName, renameSaving, renameError,
  solverHorizon, savingSettings, settingsError, settingsSaved,
  solverStartDateTime, autoPublish, isClosedBase, minRestBetweenShiftsHours, managementTimeoutMinutes, allowMembersViewHistory, allowMembersViewStats,
  solverPolling, solverStatus, solverError, draftVersion,
  members,
  transferPersonId, transferSaving, transferError, hasPendingTransfer, cancelTransferSaving,
  showDeleteConfirm, deleteSaving, deleteError,
  onGroupNameChange, onRenameGroup, onSolverHorizonChange, onSolverStartDateTimeChange, onAutoPublishChange, onClosedBaseChange, onMinRestBetweenShiftsChange, onManagementTimeoutChange, onAllowMembersViewHistoryChange, onAllowMembersViewStatsChange, onSaveSettings,
  onTriggerSolver, onOpenDraftModal,
  onTransferPersonChange, onInitiateTransfer, onCancelTransfer,
  onShowDeleteConfirm, onDeleteGroup,
}: Props) {
  const t = useTranslations("groups.settings_tab");
  const tCommon = useTranslations("common");
  const tImport = useTranslations("import");
  const tAdmin = useTranslations("admin");
  const locale = useLocale();

  const [importModalOpen, setImportModalOpen] = useState(false);
  const [allGroups, setAllGroups] = useState<{ id: string; name: string; parentGroupId: string | null }[]>([]);
  const [currentParentId, setCurrentParentId] = useState<string | null>(null);

  // Fetch all groups in this space for the parent group selector
  useEffect(() => {
    import("@/lib/api/groups").then(({ getGroups }) => {
      getGroups(spaceId).then(groups => {
        setAllGroups(groups.map(g => ({ id: g.id, name: g.name, parentGroupId: (g as any).parentGroupId ?? null })));
        const current = groups.find(g => g.id === groupId);
        if (current) setCurrentParentId((current as any).parentGroupId ?? null);
      }).catch(() => {});
    });
  }, [spaceId, groupId]);

  // Resolve feature visibility from template type
  const resolvedTemplateType: GroupTemplateType = (
    templateType && templateType in FEATURE_VISIBILITY_MAP
      ? templateType as GroupTemplateType
      : "Custom"
  );
  const visibility = FEATURE_VISIBILITY_MAP[resolvedTemplateType];
  const stayoverLabel = visibility.stayoverLabel[locale as keyof typeof visibility.stayoverLabel] ?? visibility.stayoverLabel.en;

  // Solver start time — defaults to midnight (00:00) of today.
  // Shifts always start from day boundaries for cleaner schedules.
  const [solverStartTime, setSolverStartTime] = useState(() => {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T00:00`;
  });

  if (!isAdmin) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
        <p className="text-slate-400 text-sm">{t("adminOnly")}</p>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {/* ═══ GENERAL ═══ */}
      <div className="space-y-4">
        <h3 className="text-xs font-bold text-slate-400 dark:text-slate-500 uppercase tracking-wider">{tCommon("general") ?? "General"}</h3>

        {/* Rename */}
        <Section title={t("groupName")}>
          <div className="flex gap-2">
            <input
              type="text"
              value={newGroupName}
              onChange={e => onGroupNameChange(e.target.value)}
              className="flex-1 border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
            />
            <button onClick={onRenameGroup} disabled={renameSaving} className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {renameSaving ? t("saving") : t("save")}
            </button>
          </div>
          {renameError && <p className="text-sm text-red-600 mt-2">{renameError}</p>}
        </Section>

        {/* Join Code */}
        <JoinCodeSection spaceId={spaceId} groupId={groupId} />

        {/* Parent Group Linking */}
        {allGroups.length > 1 && (
          <Section title={t("parentGroup") ?? "Parent Group"}>
            <LinkedGroupSelector
              groupId={groupId}
              currentParentId={currentParentId}
              allGroups={allGroups}
              onUpdate={() => {
                import("@/lib/api/groups").then(({ getGroups }) => {
                  getGroups(spaceId).then(groups => {
                    setAllGroups(groups.map(g => ({ id: g.id, name: g.name, parentGroupId: (g as any).parentGroupId ?? null })));
                    const current = groups.find(g => g.id === groupId);
                    if (current) setCurrentParentId((current as any).parentGroupId ?? null);
                  }).catch(() => {});
                });
              }}
            />
          </Section>
        )}
      </div>

      {/* ═══ SCHEDULING ═══ */}
      <div className="space-y-4">
        <h3 className="text-xs font-bold text-slate-400 dark:text-slate-500 uppercase tracking-wider">{t("planningHorizon")}</h3>

        <Section title={t("planningHorizon")}>
        <div className="space-y-4">
          {/* Horizon slider */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-600 dark:text-slate-300">{t("daysAhead")}: <strong>{solverHorizon}</strong></span>
            </div>
            <input
              type="range"
              min={1}
              max={7}
              value={solverHorizon}
              onChange={e => onSolverHorizonChange(Number(e.target.value))}
              className="w-full"
            />
          </div>

          {/* Start date/time */}
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <label className="text-sm text-slate-600 dark:text-slate-300 whitespace-nowrap">{t("solverStartFrom")}</label>
              <input
                type="datetime-local"
                value={solverStartDateTime ?? solverStartTime}
                onChange={e => { onSolverStartDateTimeChange(e.target.value || null); setSolverStartTime(e.target.value); }}
                className="flex-1 border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
              />
              {(solverStartDateTime || solverStartTime) && (
                <button
                  onClick={() => { onSolverStartDateTimeChange(null); setSolverStartTime(""); }}
                  className="text-xs text-slate-500 dark:text-slate-400 hover:text-red-500 dark:hover:text-red-400 border border-slate-200 dark:border-slate-600 rounded-lg px-2 py-1 transition-colors"
                >
                  {tCommon("clear") ?? "Clear"}
                </button>
              )}
            </div>
            <p className="text-xs text-slate-400 dark:text-slate-500">{t("solverStartFromHint")}</p>
          </div>

          {/* Auto-publish toggle */}
          <div className="flex items-center justify-between py-2 border-t border-slate-100 dark:border-slate-700">
            <p className="text-sm text-slate-600 dark:text-slate-300">{t("autoPublishDesc")}</p>
            <button
              role="switch"
              aria-checked={autoPublish}
              onClick={() => onAutoPublishChange(!autoPublish)}
              className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
                autoPublish ? "bg-sky-500" : "bg-slate-300 dark:bg-slate-600"
              }`}
            >
              <span className={`absolute h-[16px] w-[16px] rounded-full bg-white shadow transition-all ${autoPublish ? "left-[21px]" : "left-[3px]"}`} />
            </button>
          </div>

          {/* Auto-scheduler toggle — runs periodically to fill uncovered slots */}
          <div className="flex items-center justify-between py-2 border-t border-slate-100 dark:border-slate-700">
            <div>
              <p className="text-sm text-slate-600 dark:text-slate-300">{t("autoScheduler") ?? "Auto-scheduler"}</p>
              <p className="text-xs text-slate-400 dark:text-slate-500 mt-0.5">{t("autoSchedulerDesc") ?? "Runs every 6 hours to fill uncovered shifts with minimal changes"}</p>
            </div>
            <button
              role="switch"
              aria-checked={isClosedBase}
              onClick={() => onClosedBaseChange(!isClosedBase)}
              className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
                isClosedBase ? "bg-sky-500" : "bg-slate-300 dark:bg-slate-600"
              }`}
            >
              <span className={`absolute h-[16px] w-[16px] rounded-full bg-white shadow transition-all ${isClosedBase ? "left-[21px]" : "left-[3px]"}`} />
            </button>
          </div>

          {/* Errors / warnings */}
          {members.length < 2 && (
            <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl px-4 py-3">
              <p className="text-sm font-medium text-red-700 dark:text-red-400">{tAdmin("solverCannotRun")}</p>
              <p className="text-xs text-red-600 dark:text-red-400 mt-1">{tAdmin("solverNotEnoughMembers")}</p>
            </div>
          )}
          {solverError && !solverPolling && (
            <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl px-4 py-3">
              <p className="text-sm font-medium text-red-700 dark:text-red-400">{tAdmin("solverLastFailed")}</p>
              <p className="text-xs text-red-600 dark:text-red-400 mt-1">{solverError}</p>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-2">{tAdmin("solverSolutions")}</p>
            </div>
          )}
          {draftVersion && (
            <div className="flex items-center gap-2 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-xl px-4 py-3">
              <span className="text-sm text-amber-800 dark:text-amber-300">{t("draftPending")}</span>
              <button onClick={onOpenDraftModal} className="text-xs text-amber-700 dark:text-amber-300 border border-amber-300 dark:border-amber-700 hover:bg-amber-100 dark:hover:bg-amber-900/30 px-3 py-1.5 rounded-lg transition-colors font-medium">{t("viewDraft")}</button>
            </div>
          )}

          {/* Action buttons */}
          <div className="flex items-center gap-3 pt-1">
            <button onClick={onSaveSettings} disabled={savingSettings} className="bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-slate-700 dark:text-slate-200 text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {savingSettings ? t("saving") : t("saveSettings")}
            </button>
            <button
              onClick={() => onTriggerSolver(solverStartTime ? new Date(solverStartTime).toISOString() : undefined)}
              disabled={solverPolling || members.length < 2}
              className="flex items-center gap-2 bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
            >
              {solverPolling ? (
                <>
                  <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" /><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" /></svg>
                  {(() => {
                    const phase = solverStatus?.startsWith("Running:") ? solverStatus.split(":")[1] : null;
                    const phaseKey = phase ? `solverPhase_${phase}` : null;
                    return phaseKey ? tAdmin(phaseKey as never) : t("running");
                  })()}
                </>
              ) : t("runSchedule")}
            </button>
          </div>
          {settingsError && <p className="text-sm text-red-600 dark:text-red-400">{settingsError}</p>}
          {settingsSaved && <p className="text-sm text-emerald-600">✓</p>}
          {solverStatus && !solverError && !solverPolling && (
            <p className={`text-sm ${solverStatus === "Completed" ? "text-emerald-600" : solverStatus === "Failed" ? "text-red-600 dark:text-red-400" : "text-slate-600 dark:text-slate-400"}`}>
              {solverStatus === "Completed" ? tCommon("completed") + " ✓" : solverStatus === "TimedOut" ? tCommon("timedOut") : ""}
            </p>
          )}
        </div>
      </Section>

      {/* Minimum rest between shifts */}
      {visibility.minRestBetweenShifts && (
      <Section title={t("minRestBetweenShifts")}>
        <div className="space-y-2">
          <p className="text-sm text-slate-600 dark:text-slate-300">{t("minRestBetweenShiftsDesc")}</p>
          <div className="flex items-center gap-3">
            <input
              type="number"
              min={0}
              max={24}
              value={minRestBetweenShiftsHours}
              onChange={e => {
                const val = Math.max(0, Math.min(24, parseInt(e.target.value) || 0));
                onMinRestBetweenShiftsChange(val);
              }}
              className="w-20 border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white rounded-xl px-3 py-2.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-sky-500"
            />
            <span className="text-sm text-slate-500 dark:text-slate-400">{t("hours")}</span>
          </div>
          {minRestBetweenShiftsHours === 0 && (
            <p className="text-xs text-amber-600">{t("minRestZeroWarning")}</p>
          )}
        </div>
      </Section>
      )}
      </div>

      {/* ═══ PERMISSIONS ═══ */}
      <div className="space-y-4">
        <h3 className="text-xs font-bold text-slate-400 dark:text-slate-500 uppercase tracking-wider">{tCommon("permissions") ?? "Permissions"}</h3>

      {/* Allow members to view history toggle */}
      <Section title={t("allowMembersViewHistory")}>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-slate-600">{t("allowMembersViewHistoryDesc")}</p>
          </div>
          <button
            role="switch"
            aria-checked={allowMembersViewHistory}
            onClick={() => onAllowMembersViewHistoryChange(!allowMembersViewHistory)}
            className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
              allowMembersViewHistory ? "bg-sky-500" : "bg-slate-300"
            }`}
          >
            <span
              className={`absolute h-[16px] w-[16px] rounded-full bg-white shadow transition-all ${
                allowMembersViewHistory ? "left-[21px]" : "left-[3px]"
              }`}
            />
          </button>
        </div>
      </Section>

      {/* Allow members to view stats toggle */}
      <Section title={t("allowMembersViewStats")}>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-slate-600">{t("allowMembersViewStatsDesc")}</p>
          </div>
          <button
            role="switch"
            aria-checked={allowMembersViewStats}
            onClick={() => onAllowMembersViewStatsChange(!allowMembersViewStats)}
            className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
              allowMembersViewStats ? "bg-sky-500" : "bg-slate-300"
            }`}
          >
            <span
              className={`absolute h-[16px] w-[16px] rounded-full bg-white shadow transition-all ${
                allowMembersViewStats ? "left-[21px]" : "left-[3px]"
              }`}
            />
          </button>
        </div>
      </Section>

      {/* Management mode timeout */}
      <Section title={t("managementTimeout")}>
        <div className="space-y-2">
          <p className="text-sm text-slate-600 dark:text-slate-300">{t("managementTimeoutDesc")}</p>
          <div className="flex items-center gap-3">
            <input
              type="number"
              min={5}
              max={120}
              value={managementTimeoutMinutes}
              onChange={e => {
                const raw = parseInt(e.target.value);
                if (!isNaN(raw)) {
                  onManagementTimeoutChange(raw);
                }
              }}
              className="w-20 border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white rounded-xl px-3 py-2.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-sky-500"
            />
            <span className="text-sm text-slate-500 dark:text-slate-400">{t("minutes")}</span>
          </div>
          {(managementTimeoutMinutes < 5 || managementTimeoutMinutes > 120 || !Number.isInteger(managementTimeoutMinutes)) && (
            <p className="text-sm text-red-600">{t("managementTimeoutError")}</p>
          )}
        </div>
      </Section>
      </div>

      {/* ═══ ADVANCED ═══ */}
      <div className="space-y-4">
        <h3 className="text-xs font-bold text-slate-400 dark:text-slate-500 uppercase tracking-wider">{tCommon("advanced") ?? "Advanced"}</h3>

      {/* Closed base toggle */}
      {visibility.closedBase && (
      <Section title={stayoverLabel}>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-slate-600">
              סמן קבוצה זו כדי להפעיל תכנון חופשות אוטומטי. כאשר מופעל, המערכת תתזמן חופשות הביתה עבור אנשי הצוות.
            </p>
          </div>
          <button
            role="switch"
            aria-checked={isClosedBase}
            onClick={() => onClosedBaseChange(!isClosedBase)}
            className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
              isClosedBase ? "bg-sky-500" : "bg-slate-300"
            }`}
          >
            <span
              className={`absolute h-[16px] w-[16px] rounded-full bg-white shadow transition-all ${
                isClosedBase ? "left-[21px]" : "left-[3px]"
              }`}
            />
          </button>
        </div>
      </Section>
      )}

      {/* Home-leave configuration panel — visible only when closed base is enabled */}
      {visibility.homeLeave && (
      <HomeLeaveConfigPanel
        spaceId={spaceId}
        groupId={groupId}
        isClosedBase={isClosedBase}
        memberCount={members.length}
        isAdmin={isAdmin}
      />
      )}

      {/* Smart Import */}
      <Section title={tImport("title")}>
        <div className="space-y-2">
          <p className="text-sm text-slate-500 dark:text-slate-400">{tImport("subtitle")}</p>
          <button
            onClick={() => setImportModalOpen(true)}
            className="flex items-center gap-2 text-sm font-medium text-white bg-sky-500 hover:bg-sky-600 px-4 py-2.5 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            {tImport("title")}
          </button>
        </div>
      </Section>
      </div>

      {/* ═══ DANGER ZONE ═══ */}
      <div className="space-y-4">
        <h3 className="text-xs font-bold text-red-400 dark:text-red-500 uppercase tracking-wider">{tCommon("dangerZone") ?? "Danger Zone"}</h3>

      {/* Ownership transfer */}      <Section title={t("ownershipTransfer")}>
        {hasPendingTransfer ? (
          <div className="space-y-2">
            <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">{t("pendingTransfer")}</p>
            <button onClick={onCancelTransfer} disabled={cancelTransferSaving} className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
              {cancelTransferSaving ? t("transferring") : t("cancelTransfer")}
            </button>
          </div>
        ) : (
          <div className="flex gap-2">
            <select
              value={transferPersonId}
              onChange={e => onTransferPersonChange(e.target.value)}
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
            >
              <option value="">{t("selectMember")}</option>
              {members.map(m => (
                <option key={m.personId} value={m.personId}>{m.displayName ?? m.fullName}</option>
              ))}
            </select>
            <button onClick={onInitiateTransfer} disabled={transferSaving || !transferPersonId} className="bg-amber-500 hover:bg-amber-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {transferSaving ? t("transferring") : t("transfer")}
            </button>
          </div>
        )}
        {transferError && <p className="text-sm text-red-600 mt-2">{transferError}</p>}
      </Section>

      <SmartImportModal
        groupId={groupId}
        open={importModalOpen}
        onClose={() => setImportModalOpen(false)}
      />

      {/* Delete group */}
      <Section title={t("deleteGroup")}>
        {showDeleteConfirm ? (
          <div className="bg-red-50 border border-red-200 rounded-xl p-4 space-y-3">
            <p className="text-sm text-red-700">{t("deleteConfirm")}</p>
            <div className="flex gap-2">
              <button onClick={onDeleteGroup} disabled={deleteSaving} className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
                {deleteSaving ? t("deleting") : t("yesDelete")}
              </button>
              <button onClick={() => onShowDeleteConfirm(false)} className="text-sm text-slate-500 border border-slate-200 px-4 py-2 rounded-xl hover:bg-slate-50 transition-colors">{t("cancel")}</button>
            </div>
            {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}
          </div>
        ) : (
          <button onClick={() => onShowDeleteConfirm(true)} className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2.5 rounded-xl transition-colors">
            {t("deleteGroup")}
          </button>
        )}
      </Section>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-3">
      <h3 className="text-sm font-semibold text-slate-700">{title}</h3>
      {children}
    </div>
  );
}

function JoinCodeSection({ spaceId, groupId }: { spaceId: string; groupId: string }) {
  const t = useTranslations("groups.settings_tab");
  const [joinCode, setJoinCode] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [copied, setCopied] = useState(false);
  const [regenerating, setRegenerating] = useState(false);

  useEffect(() => {
    getJoinCode(spaceId, groupId)
      .then(code => setJoinCode(code))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [spaceId, groupId]);

  async function handleRegenerate() {
    setRegenerating(true);
    try {
      const newCode = await regenerateJoinCode(spaceId, groupId);
      setJoinCode(newCode);
    } catch {}
    finally { setRegenerating(false); }
  }

  function handleCopy() {
    const url = `${window.location.origin}/groups/join?code=${joinCode}`;
    navigator.clipboard.writeText(url).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  return (
    <Section title={t("inviteLink")}>
      {loading ? (
        <p className="text-sm text-slate-400">{t("loading")}</p>
      ) : (
        <div className="space-y-3">
          <p className="text-xs text-slate-500">{t("inviteLinkDesc")}</p>
          <div className="flex items-center gap-2">
            <div className="flex-1 bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 font-mono text-sm text-slate-700 tracking-widest text-center">
              {joinCode ?? "—"}
            </div>
            <button
              onClick={handleCopy}
              disabled={!joinCode}
              className="flex items-center gap-1.5 bg-sky-500 hover:bg-sky-600 text-white text-xs font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
            >
              {copied ? "✓" : (
                <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                </svg>
              )}
              {copied ? t("copied") : t("copyLink")}
            </button>
          </div>
          <button
            onClick={handleRegenerate}
            disabled={regenerating}
            className="text-xs text-slate-500 hover:text-slate-700 disabled:opacity-50"
          >
            {regenerating ? "..." : t("regenerateCode")}
          </button>
        </div>
      )}
    </Section>
  );
}
