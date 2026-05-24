"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalTime } from "@/lib/utils/formatTime";
import Modal from "@/components/Modal";

export interface OverridePerson {
  personId: string;
  displayName: string;
}

interface OverrideModalProps {
  open: boolean;
  slotKey: string;        // "${startsAt}|${endsAt}"
  taskName: string;
  currentAssignees: string[];   // display names currently in the cell
  eligiblePeople: OverridePerson[];
  saving: boolean;
  error: string | null;
  onConfirm: (slotKey: string, newPersonIds: string[]) => Promise<void>;
  onClear: (slotKey: string) => Promise<void>;
  onClose: () => void;
}

export default function OverrideModal({
  open, slotKey, taskName, currentAssignees, eligiblePeople,
  saving, error, onConfirm, onClear, onClose,
}: OverrideModalProps) {
  const t = useTranslations("schedule.override");
  const tCommon = useTranslations("common");
  const timezoneId = useAuthStore(s => s.timezoneId);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [showConfirmClear, setShowConfirmClear] = useState(false);

  // Parse time from slotKey for display
  const [startsAt, endsAt] = slotKey.split("|");
  const fmt = (iso: string) => formatLocalTime(iso, timezoneId, "24h");
  const timeLabel = startsAt && endsAt ? `${fmt(startsAt)} – ${fmt(endsAt)}` : "";

  function togglePerson(personId: string) {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(personId)) next.delete(personId);
      else next.add(personId);
      return next;
    });
  }

  async function handleConfirm() {
    if (selectedIds.size === 0) return;
    await onConfirm(slotKey, Array.from(selectedIds));
  }

  async function handleClear() {
    await onClear(slotKey);
    setShowConfirmClear(false);
  }

  return (
    <Modal title={t("title")} open={open} onClose={onClose} maxWidth={480}>
      <div className="space-y-4">
        {/* Slot info */}
        <div className="bg-slate-50 border border-slate-200 rounded-xl px-4 py-3 space-y-1">
          <p className="text-sm font-semibold text-slate-800">{taskName}</p>
          <p className="text-xs text-slate-500">{timeLabel}</p>
        </div>

        {/* Current assignees */}
        {currentAssignees.length > 0 && (
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">{t("currentAssignees")}</p>
            <div className="flex flex-wrap gap-1.5">
              {currentAssignees.map(name => (
                <span key={name} className="px-2.5 py-1 bg-sky-50 text-sky-700 border border-sky-200 rounded-full text-xs font-medium">
                  {name}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Person selector */}
        <div>
          <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">{t("selectPeople")}</p>
          {eligiblePeople.length === 0 ? (
            <p className="text-sm text-slate-400">{t("noEligible")}</p>
          ) : (
            <div className="space-y-1 max-h-48 overflow-y-auto">
              {eligiblePeople.map(p => (
                <label
                  key={p.personId}
                  className={`flex items-center gap-3 px-3 py-2 rounded-xl border cursor-pointer transition-colors ${
                    selectedIds.has(p.personId)
                      ? "border-sky-300 bg-sky-50"
                      : "border-slate-200 bg-white hover:bg-slate-50"
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={selectedIds.has(p.personId)}
                    onChange={() => togglePerson(p.personId)}
                    className="accent-sky-500"
                  />
                  <span className="text-sm text-slate-800">{p.displayName}</span>
                </label>
              ))}
            </div>
          )}
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}

        {/* Actions */}
        <div className="flex items-center gap-2 pt-1">
          <button
            onClick={handleConfirm}
            disabled={saving || selectedIds.size === 0}
            className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
          >
            {saving ? t("saving") : t("applyOverride")}
          </button>

          {!showConfirmClear ? (
            <button
              onClick={() => setShowConfirmClear(true)}
              disabled={saving}
              className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
            >
              {t("clearShift")}
            </button>
          ) : (
            <div className="flex items-center gap-2">
              <span className="text-xs text-red-600">{tCommon("confirm")}?</span>
              <button
                onClick={handleClear}
                disabled={saving}
                className="bg-red-500 hover:bg-red-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
              >
                {saving ? "..." : t("yesClear")}
              </button>
              <button
                onClick={() => setShowConfirmClear(false)}
                className="text-xs text-slate-500 px-2"
              >
                {tCommon("cancel")}
              </button>
            </div>
          )}

          <button
            onClick={onClose}
            className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors mr-auto"
          >
            {tCommon("cancel")}
          </button>
        </div>
      </div>
    </Modal>
  );
}
