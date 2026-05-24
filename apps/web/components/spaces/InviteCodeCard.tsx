"use client";

import { useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { regenerateSpaceInviteCode } from "@/lib/api/spaces";

interface Props {
  spaceId: string;
  inviteCode: string | null;
  isOwner: boolean;
  /** Called after a successful regeneration with the new code */
  onCodeRegenerated?: (newCode: string) => void;
}

/**
 * Displays the space invite code with copy and regenerate actions.
 * Only renders when the user is the space owner and an invite code exists.
 */
export default function InviteCodeCard({
  spaceId,
  inviteCode,
  isOwner,
  onCodeRegenerated,
}: Props) {
  const t = useTranslations("spaces");
  const [currentCode, setCurrentCode] = useState(inviteCode);
  const [copied, setCopied] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const handleCopy = useCallback(() => {
    if (!currentCode) return;
    navigator.clipboard.writeText(currentCode);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [currentCode]);

  const handleRegenerate = useCallback(async () => {
    if (!spaceId) return;
    setRegenerating(true);
    try {
      const { inviteCode: newCode } = await regenerateSpaceInviteCode(spaceId);
      setCurrentCode(newCode);
      onCodeRegenerated?.(newCode);
    } catch {
      /* handled by interceptor */
    } finally {
      setRegenerating(false);
      setShowConfirm(false);
    }
  }, [spaceId, onCodeRegenerated]);

  // Only visible to Space Owner with an existing invite code
  if (!isOwner || !currentCode) return null;

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
        {t("inviteCode")}
      </h2>

      {/* Current invite code display */}
      <div className="flex items-center gap-3">
        <code className="flex-1 px-4 py-2.5 rounded-lg bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 text-center font-mono text-lg tracking-widest text-slate-900 dark:text-white">
          {currentCode}
        </code>
        <button
          onClick={handleCopy}
          aria-label={copied ? t("copied") : t("copyCode")}
          className="px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 text-sm text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
        >
          {copied ? t("copied") : t("copyCode")}
        </button>
      </div>

      {/* Regenerate section */}
      {!showConfirm ? (
        <button
          onClick={() => setShowConfirm(true)}
          className="mt-3 text-xs text-slate-500 dark:text-slate-400 hover:text-red-500 dark:hover:text-red-400 transition-colors"
        >
          {t("regenerateCode")}
        </button>
      ) : (
        <div className="mt-3 flex items-center gap-2">
          <p className="text-xs text-amber-600 dark:text-amber-400 flex-1">
            {t("regenerateConfirm")}
          </p>
          <button
            onClick={handleRegenerate}
            disabled={regenerating}
            className="px-3 py-1.5 rounded-lg bg-red-500 hover:bg-red-600 disabled:bg-red-300 dark:disabled:bg-red-800 text-white text-xs font-medium transition-colors"
          >
            {regenerating ? "..." : t("regenerateCode")}
          </button>
          <button
            onClick={() => setShowConfirm(false)}
            disabled={regenerating}
            className="px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-600 text-xs text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
          >
            Cancel
          </button>
        </div>
      )}
    </div>
  );
}
