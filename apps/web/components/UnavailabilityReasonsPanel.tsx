"use client";

import { useState, useEffect, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  getReasons,
  createReason,
  updateReason,
  deleteReason,
  type UnavailabilityReasonDto,
} from "@/lib/api/unavailabilityReasons";

interface UnavailabilityReasonsPanelProps {
  spaceId: string;
}

const MAX_REASONS = 50;
const MAX_DISPLAY_NAME_LENGTH = 100;

/**
 * Unavailability Reasons settings panel.
 * Allows managing the list of structured unavailability reasons for a space.
 */
export default function UnavailabilityReasonsPanel({ spaceId }: UnavailabilityReasonsPanelProps) {
  const t = useTranslations("unavailabilityReasons");
  const [reasons, setReasons] = useState<UnavailabilityReasonDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [newReasonName, setNewReasonName] = useState("");
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const fetchReasons = useCallback(async () => {
    if (!spaceId) return;
    setLoading(true);
    try {
      const data = await getReasons(spaceId);
      setReasons(data);
    } catch {
      setReasons([]);
    } finally {
      setLoading(false);
    }
  }, [spaceId]);

  useEffect(() => {
    fetchReasons();
  }, [fetchReasons]);

  async function handleAdd() {
    const trimmed = newReasonName.trim();
    if (!trimmed) return;
    if (trimmed.length > MAX_DISPLAY_NAME_LENGTH) {
      setError(t("nameTooLong", { max: MAX_DISPLAY_NAME_LENGTH }));
      return;
    }
    if (reasons.length >= MAX_REASONS) {
      setError(t("maxReached", { max: MAX_REASONS }));
      return;
    }

    setAdding(true);
    setError(null);
    try {
      const nextSortOrder = reasons.length > 0 ? Math.max(...reasons.map((r) => r.sortOrder)) + 1 : 0;
      await createReason(spaceId, { displayName: trimmed, sortOrder: nextSortOrder });
      setNewReasonName("");
      await fetchReasons();
    } catch {
      setError(t("errorAdd"));
    } finally {
      setAdding(false);
    }
  }

  function startEditing(reason: UnavailabilityReasonDto) {
    setEditingId(reason.id);
    setEditingName(reason.displayName);
    setError(null);
  }

  async function handleSaveEdit() {
    if (!editingId) return;
    const trimmed = editingName.trim();
    if (!trimmed) return;
    if (trimmed.length > MAX_DISPLAY_NAME_LENGTH) {
      setError(t("nameTooLong", { max: MAX_DISPLAY_NAME_LENGTH }));
      return;
    }

    setError(null);
    try {
      const existing = reasons.find((r) => r.id === editingId);
      if (!existing) return;
      await updateReason(spaceId, editingId, { displayName: trimmed, sortOrder: existing.sortOrder });
      setEditingId(null);
      setEditingName("");
      await fetchReasons();
    } catch {
      setError(t("errorUpdate"));
    }
  }

  function cancelEditing() {
    setEditingId(null);
    setEditingName("");
  }

  async function handleDeactivate(reasonId: string) {
    setError(null);
    try {
      await deleteReason(spaceId, reasonId);
      await fetchReasons();
    } catch {
      setError(t("errorDelete"));
    }
  }

  if (loading) {
    return (
      <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-3">
        <h3 className="text-sm font-semibold text-slate-700">{t("title")}</h3>
        <p className="text-sm text-slate-400">{t("loading")}</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-700">{t("title")}</h3>
        <span className="text-xs text-slate-400">
          {reasons.length}/{MAX_REASONS}
        </span>
      </div>

      {/* Reasons list */}
      <div className="space-y-2">
        {reasons.map((reason) => (
          <div
            key={reason.id}
            className="flex items-center gap-2 bg-slate-50 rounded-xl px-3 py-2"
          >
            {editingId === reason.id ? (
              <>
                <input
                  type="text"
                  value={editingName}
                  onChange={(e) => setEditingName(e.target.value)}
                  maxLength={MAX_DISPLAY_NAME_LENGTH}
                  className="flex-1 border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
                  onKeyDown={(e) => {
                    if (e.key === "Enter") handleSaveEdit();
                    if (e.key === "Escape") cancelEditing();
                  }}
                  autoFocus
                />
                <button
                  onClick={handleSaveEdit}
                  className="text-xs text-sky-600 hover:text-sky-700 font-medium"
                >
                  {t("save")}
                </button>
                <button
                  onClick={cancelEditing}
                  className="text-xs text-slate-400 hover:text-slate-600"
                >
                  {t("cancel")}
                </button>
              </>
            ) : (
              <>
                <span className="flex-1 text-sm text-slate-700">{reason.displayName}</span>
                <button
                  onClick={() => startEditing(reason)}
                  className="text-xs text-slate-400 hover:text-sky-600 transition-colors"
                  title={t("edit")}
                >
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" />
                  </svg>
                </button>
                <button
                  onClick={() => handleDeactivate(reason.id)}
                  className="text-xs text-slate-400 hover:text-red-600 transition-colors"
                  title={t("delete")}
                >
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
                  </svg>
                </button>
              </>
            )}
          </div>
        ))}

        {reasons.length === 0 && (
          <p className="text-sm text-slate-400 text-center py-2">{t("empty")}</p>
        )}
      </div>

      {/* Add new reason */}
      <div className="flex items-center gap-2">
        <input
          type="text"
          value={newReasonName}
          onChange={(e) => setNewReasonName(e.target.value)}
          placeholder={t("placeholder")}
          maxLength={MAX_DISPLAY_NAME_LENGTH}
          className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
          onKeyDown={(e) => {
            if (e.key === "Enter") handleAdd();
          }}
        />
        <button
          onClick={handleAdd}
          disabled={adding || !newReasonName.trim() || reasons.length >= MAX_REASONS}
          className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors whitespace-nowrap"
        >
          {adding ? t("adding") : t("addReason")}
        </button>
      </div>

      {/* Error message */}
      {error && <p className="text-sm text-red-600">{error}</p>}
    </div>
  );
}
