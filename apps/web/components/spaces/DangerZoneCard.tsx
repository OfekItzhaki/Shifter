"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import { softDeleteSpace, transferOwnership, SpaceMemberDto } from "@/lib/api/spaces";
import { useWriteGuard } from "@/lib/api/writeGuard";

interface DangerZoneCardProps {
  spaceId: string;
  isOwner: boolean;
  members: SpaceMemberDto[];
  currentOwnerId: string;
}

/**
 * Danger Zone card for destructive space actions: delete space and transfer ownership.
 * Visually distinct with red border. Only visible to the Space Owner.
 * Requires confirmation dialogs before executing destructive actions.
 *
 * Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5
 */
export default function DangerZoneCard({
  spaceId,
  isOwner,
  members,
  currentOwnerId,
}: DangerZoneCardProps) {
  const t = useTranslations("spaces");
  const { isDisabled: writeGuardDisabled, tooltipText: writeGuardTooltip } = useWriteGuard();

  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const [transferTarget, setTransferTarget] = useState("");
  const [showTransferConfirm, setShowTransferConfirm] = useState(false);
  const [transferring, setTransferring] = useState(false);

  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);

  // Auto-dismiss toast after 4 seconds
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 4000);
    return () => clearTimeout(timer);
  }, [toast]);

  // Permission gate: hide entirely for non-owners
  if (!isOwner) return null;

  // Transfer targets: all members except the current owner
  const transferTargets = members.filter((m) => m.userId !== currentOwnerId);

  const handleDelete = async () => {
    setDeleting(true);
    setToast(null);
    try {
      await softDeleteSpace(spaceId);
      setToast({ type: "success", message: t("dangerZone.deleteSuccess") });
      setShowDeleteConfirm(false);
    } catch {
      setToast({ type: "error", message: t("dangerZone.deleteError") });
    } finally {
      setDeleting(false);
    }
  };

  const handleTransfer = async () => {
    if (!transferTarget) return;
    setTransferring(true);
    setToast(null);
    try {
      await transferOwnership(spaceId, transferTarget);
      setToast({ type: "success", message: t("dangerZone.transferSuccess") });
      setShowTransferConfirm(false);
      setTransferTarget("");
    } catch {
      setToast({ type: "error", message: t("dangerZone.transferError") });
    } finally {
      setTransferring(false);
    }
  };

  return (
    <div className="space-y-4">
      {/* Transfer Ownership — Amber/Warning card */}
      <div className="border-2 border-amber-300 dark:border-amber-700 bg-amber-50 dark:bg-amber-950/20 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-amber-700 dark:text-amber-400 mb-1">
          {t("dangerZone.transferTitle")}
        </h2>
        <p className="text-xs text-amber-600/70 dark:text-amber-400/70 mb-4">
          {t("dangerZone.transferDescription")}
        </p>

        <div className="flex items-center gap-3">
          <select
            value={transferTarget}
            onChange={(e) => {
              setTransferTarget(e.target.value);
              setShowTransferConfirm(false);
            }}
            aria-label={t("dangerZone.selectMemberLabel")}
            className="flex-1 px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-amber-500"
          >
            <option value="">{t("dangerZone.selectMember")}</option>
            {transferTargets.map((member) => (
              <option key={member.userId} value={member.userId}>
                {member.displayName ?? member.email ?? member.userId}
              </option>
            ))}
          </select>

          {!showTransferConfirm ? (
            <span title={writeGuardDisabled ? writeGuardTooltip : undefined}>
              <button
                onClick={() => setShowTransferConfirm(true)}
                disabled={!transferTarget || writeGuardDisabled}
                className="px-4 py-2 rounded-lg border border-amber-300 dark:border-amber-700 text-amber-600 dark:text-amber-400 text-sm font-medium hover:bg-amber-100 dark:hover:bg-amber-900/30 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {t("dangerZone.transferButton")}
              </button>
            </span>
          ) : (
            <div className="flex items-center gap-2">
              <button
                onClick={handleTransfer}
                disabled={transferring}
                className="px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-700 disabled:bg-amber-300 dark:disabled:bg-amber-800 text-white text-xs font-medium transition-colors"
              >
                {transferring ? t("dangerZone.transferring") : t("dangerZone.confirmTransfer")}
              </button>
              <button
                onClick={() => setShowTransferConfirm(false)}
                disabled={transferring}
                className="px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-600 text-xs text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
              >
                {t("dangerZone.cancel")}
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Delete Space — Red/Danger card */}
      <div className="border-2 border-red-300 dark:border-red-700 bg-red-50 dark:bg-red-950/20 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-red-700 dark:text-red-400 mb-1">
          {t("dangerZone.title")}
        </h2>
        <p className="text-xs text-red-600/70 dark:text-red-400/70 mb-4">
          {t("dangerZone.description")}
        </p>

        <h3 className="text-xs font-medium text-slate-700 dark:text-slate-300 mb-2">
          {t("dangerZone.deleteTitle")}
        </h3>
        <p className="text-xs text-slate-500 dark:text-slate-400 mb-2">
          {t("dangerZone.deleteDescription")}
        </p>

        {!showDeleteConfirm ? (
          <span title={writeGuardDisabled ? writeGuardTooltip : undefined}>
            <button
              onClick={() => setShowDeleteConfirm(true)}
              disabled={writeGuardDisabled}
              className="px-4 py-2 rounded-lg border border-red-300 dark:border-red-700 text-red-600 dark:text-red-400 text-sm font-medium hover:bg-red-100 dark:hover:bg-red-900/30 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {t("dangerZone.deleteButton")}
            </button>
          </span>
        ) : (
          <div className="flex items-center gap-2">
            <p className="text-xs text-red-600 dark:text-red-400 flex-1">
              {t("dangerZone.deleteConfirm")}
            </p>
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="px-3 py-1.5 rounded-lg bg-red-600 hover:bg-red-700 disabled:bg-red-300 dark:disabled:bg-red-800 text-white text-xs font-medium transition-colors"
            >
              {deleting ? t("dangerZone.deleting") : t("dangerZone.confirmDelete")}
            </button>
            <button
              onClick={() => setShowDeleteConfirm(false)}
              disabled={deleting}
              className="px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-600 text-xs text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
            >
              {t("dangerZone.cancel")}
            </button>
          </div>
        )}
      </div>

      {/* Toast notification */}
      {toast && (
        <div
          role={toast.type === "error" ? "alert" : "status"}
          aria-live={toast.type === "error" ? "assertive" : "polite"}
          className={`text-xs px-3 py-2 rounded-lg ${
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
