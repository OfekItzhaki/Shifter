"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import type { GroupMemberDto } from "@/lib/api/groups";
import { getJoinCode, regenerateJoinCode } from "@/lib/api/groups";
import SmartImportModal from "@/components/SmartImportModal";
import HomeLeaveConfigPanel from "@/components/home-leave/HomeLeaveConfigPanel";

interface DraftVersion { id: string; status: string; }

interface Props {
  isAdmin: boolean;
  spaceId: string;
  groupId: string;
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
  isAdmin, spaceId, groupId, newGroupName, renameSaving, renameError,
  solverHorizon, savingSettings, settingsError, settingsSaved,
  solverStartDateTime, autoPublish, isClosedBase,
  solverPolling, solverStatus, solverError, draftVersion,
  members,
  transferPersonId, transferSaving, transferError, hasPendingTransfer, cancelTransferSaving,
  showDeleteConfirm, deleteSaving, deleteError,
  onGroupNameChange, onRenameGroup, onSolverHorizonChange, onSolverStartDateTimeChange, onAutoPublishChange, onClosedBaseChange, onSaveSettings,
  onTriggerSolver, onOpenDraftModal,
  onTransferPersonChange, onInitiateTransfer, onCancelTransfer,
  onShowDeleteConfirm, onDeleteGroup,
}: Props) {
  const t = useTranslations("groups.settings_tab");
  const tCommon = useTranslations("common");
  const tImport = useTranslations("import");
  const tAdmin = useTranslations("admin");

  const [importModalOpen, setImportModalOpen] = useState(false);

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
    <div className="space-y-6">
      {/* Rename */}
      <Section title={t("groupName")}>
        <div className="flex gap-2">
          <input
            type="text"
            value={newGroupName}
            onChange={e => onGroupNameChange(e.target.value)}
            className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button onClick={onRenameGroup} disabled={renameSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {renameSaving ? t("saving") : t("save")}
          </button>
        </div>
        {renameError && <p className="text-sm text-red-600 mt-2">{renameError}</p>}
      </Section>

      {/* Join Code */}
      <JoinCodeSection spaceId={spaceId} groupId={groupId} />

      {/* Solver horizon */}
      <Section title={t("planningHorizon")}>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-sm text-slate-600">{t("daysAhead")}: <strong>{solverHorizon}</strong></span>
          </div>
          <input
            type="range"
            min={1}
            max={7}
            value={solverHorizon}
            onChange={e => onSolverHorizonChange(Number(e.target.value))}
            className="w-full"
          />
          <div className="flex items-center gap-2">
            <label className="text-sm text-slate-600 whitespace-nowrap">{t("solverStartFrom")}</label>
            <input
              type="datetime-local"
              value={solverStartDateTime ?? ""}
              onChange={e => onSolverStartDateTimeChange(e.target.value || null)}
              className="flex-1 border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {solverStartDateTime && (
              <button
                onClick={() => onSolverStartDateTimeChange(null)}
                className="text-xs text-slate-400 hover:text-slate-600 transition-colors"
                title="Clear — use current time"
              >✕</button>
            )}
          </div>
          {solverStartDateTime && new Date(solverStartDateTime) < new Date() && (
            <p className="text-xs text-amber-600">⚠ התאריך בעבר — הסולבר יתחיל מנקודה זו.</p>
          )}
          <p className="text-xs text-slate-400">{t("solverStartFromHint")}</p>
          <button onClick={onSaveSettings} disabled={savingSettings} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {savingSettings ? t("saving") : t("saveSettings")}
          </button>
          {settingsError && <p className="text-sm text-red-600">{settingsError}</p>}
          {settingsSaved && <p className="text-sm text-emerald-600">{t("save")} ✓</p>}
        </div>
      </Section>

      {/* Auto-publish toggle */}
      <Section title={t("autoPublish")}>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-slate-600">{t("autoPublishDesc")}</p>
          </div>
          <button
            role="switch"
            aria-checked={autoPublish}
            onClick={() => onAutoPublishChange(!autoPublish)}
            className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
              autoPublish ? "bg-blue-500" : "bg-slate-300"
            }`}
          >
            <span
              className={`absolute h-[16px] w-[16px] rounded-full bg-white shadow transition-all ${
                autoPublish ? "left-[21px]" : "left-[3px]"
              }`}
            />
          </button>
        </div>
      </Section>

      {/* Closed base toggle */}
      <Section title="בסיס סגור">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-slate-600">
              סמן קבוצה זו כבסיס סגור כדי להפעיל תכנון חופשות אוטומטי. כאשר מופעל, המערכת תתזמן חופשות הביתה עבור אנשי הצוות.
            </p>
          </div>
          <button
            role="switch"
            aria-checked={isClosedBase}
            onClick={() => onClosedBaseChange(!isClosedBase)}
            className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
              isClosedBase ? "bg-blue-500" : "bg-slate-300"
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

      {/* Home-leave configuration panel — visible only when closed base is enabled */}
      <HomeLeaveConfigPanel
        spaceId={spaceId}
        groupId={groupId}
        isClosedBase={isClosedBase}
        memberCount={members.length}
      />

      {/* Trigger solver */}
      <Section title={t("runSchedule")}>
        <div className="space-y-3">
          {members.length < 2 && (
            <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3">
              <p className="text-sm font-medium text-red-700">{tAdmin("solverCannotRun")}</p>
              <p className="text-xs text-red-600 mt-1">{tAdmin("solverNotEnoughMembers")}</p>
            </div>
          )}
          {solverError && !solverPolling && (
            <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3">
              <p className="text-sm font-medium text-red-700">{tAdmin("solverLastFailed")}</p>
              <p className="text-xs text-red-600 mt-1">{solverError}</p>
              <p className="text-xs text-slate-500 mt-2">{tAdmin("solverSolutions")}</p>
            </div>
          )}
          {draftVersion && (
            <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">
              <span className="text-sm text-amber-800">{t("draftPending")}</span>
              <button onClick={onOpenDraftModal} className="text-xs text-amber-700 border border-amber-300 hover:bg-amber-100 px-3 py-1.5 rounded-lg transition-colors font-medium">{t("viewDraft")}</button>
            </div>
          )}
          {/* Start time picker — defaults to now, admin can override */}
          <div className="flex items-center gap-2">
            <label className="text-sm text-slate-600 whitespace-nowrap">{t("startFrom")}</label>
            <input
              type="datetime-local"
              value={solverStartTime}
              onChange={e => setSolverStartTime(e.target.value)}
              className="flex-1 border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <button
            onClick={() => onTriggerSolver(solverStartTime ? new Date(solverStartTime).toISOString() : undefined)}
            disabled={solverPolling || members.length < 2}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
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
          {solverStatus && !solverError && !solverPolling && (
            <p className={`text-sm ${solverStatus === "Completed" ? "text-emerald-600" : solverStatus === "Failed" ? "text-red-600" : "text-slate-600"}`}>
              {solverStatus === "Completed" ? tCommon("completed") + " ✓" : solverStatus === "TimedOut" ? tCommon("timedOut") : ""}
            </p>
          )}
        </div>
      </Section>

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
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
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

      {/* Smart Import */}
      <Section title={tImport("title")}>
        <div className="space-y-2">
          <p className="text-sm text-slate-500">{tImport("subtitle")}</p>
          <button
            onClick={() => setImportModalOpen(true)}
            className="flex items-center gap-2 text-sm font-medium text-white bg-blue-500 hover:bg-blue-600 px-4 py-2.5 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            {tImport("title")}
          </button>
        </div>
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
              className="flex items-center gap-1.5 bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
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
