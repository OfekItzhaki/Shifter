"use client";

import { useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { addPresenceWindow } from "@/lib/api/availability";

interface Props {
  open: boolean;
  onClose: () => void;
  spaceId: string;
  personId: string;
  personName: string;
  /** Called after the presence window is saved — parent can trigger solver re-run */
  onSaved: (triggerRerun: boolean) => void;
}

export default function CantMakeItModal({
  open, onClose, spaceId, personId, personName, onSaved,
}: Props) {
  const locale = useLocale();
  const isRtl = locale === "he";

  // Default: blocked from now until end of today
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  const todayEnd = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T23:59`;
  const nowStr = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;

  const [startsAt, setStartsAt] = useState(nowStr);
  const [endsAt, setEndsAt] = useState(todayEnd);
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [triggerRerun, setTriggerRerun] = useState(true);

  if (!open) return null;

  async function handleSave() {
    if (!startsAt || !endsAt) {
      setError(locale === "he" ? "נא למלא תאריך התחלה וסיום" : "Please fill in start and end time");
      return;
    }
    if (new Date(endsAt) <= new Date(startsAt)) {
      setError(locale === "he" ? "זמן הסיום חייב להיות אחרי זמן ההתחלה" : "End time must be after start time");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await addPresenceWindow(
        spaceId, personId,
        "at_home",
        new Date(startsAt).toISOString(),
        new Date(endsAt).toISOString(),
        note.trim() || null
      );
      onSaved(triggerRerun);
      onClose();
    } catch {
      setError(locale === "he" ? "שגיאה בשמירה. נסה שוב." : "Error saving. Please try again.");
    } finally {
      setSaving(false);
    }
  }

  const labels = {
    title: locale === "he" ? `${personName} לא יגיע/תגיע` : `${personName} can't make it`,
    subtitle: locale === "he"
      ? "הגדר את הטווח שבו האדם לא יהיה זמין. הסולבר לא ישבץ אותו בזמן זה."
      : "Set the time range when this person is unavailable. The solver won't assign them during this window.",
    from: locale === "he" ? "מ" : "From",
    until: locale === "he" ? "עד" : "Until",
    note: locale === "he" ? "הערה (אופציונלי)" : "Note (optional)",
    notePlaceholder: locale === "he" ? "לדוגמה: חולה, לא יגיע עד מחר" : "e.g. sick, arriving tomorrow",
    rerun: locale === "he" ? "הפעל מחדש את הסולבר לאחר השמירה" : "Re-run solver after saving",
    save: locale === "he" ? "שמור" : "Save",
    saving: locale === "he" ? "שומר..." : "Saving...",
    cancel: locale === "he" ? "ביטול" : "Cancel",
  };

  return (
    <div
      style={{ position: "fixed", inset: 0, zIndex: 70, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: "1rem" }}
      onClick={onClose}
    >
      <div
        style={{ background: "white", borderRadius: 20, boxShadow: "0 24px 64px rgba(0,0,0,0.18)", width: "100%", maxWidth: 440, padding: "1.5rem" }}
        dir={isRtl ? "rtl" : "ltr"}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", marginBottom: "1rem" }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
              <span style={{ fontSize: 20 }}>⚠️</span>
              <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: 0 }}>{labels.title}</h2>
            </div>
            <p style={{ fontSize: 13, color: "#64748b", margin: 0 }}>{labels.subtitle}</p>
          </div>
          <button onClick={onClose} style={{ background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 4, flexShrink: 0 }}>
            <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Form */}
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
            <div>
              <label style={{ display: "block", fontSize: 12, color: "#64748b", marginBottom: 4 }}>{labels.from}</label>
              <input
                type="datetime-local"
                value={startsAt}
                onChange={e => setStartsAt(e.target.value)}
                style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: 10, padding: "8px 12px", fontSize: 13, outline: "none", boxSizing: "border-box" }}
              />
            </div>
            <div>
              <label style={{ display: "block", fontSize: 12, color: "#64748b", marginBottom: 4 }}>{labels.until}</label>
              <input
                type="datetime-local"
                value={endsAt}
                onChange={e => setEndsAt(e.target.value)}
                style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: 10, padding: "8px 12px", fontSize: 13, outline: "none", boxSizing: "border-box" }}
              />
            </div>
          </div>

          <div>
            <label style={{ display: "block", fontSize: 12, color: "#64748b", marginBottom: 4 }}>{labels.note}</label>
            <input
              type="text"
              value={note}
              onChange={e => setNote(e.target.value)}
              placeholder={labels.notePlaceholder}
              style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: 10, padding: "8px 12px", fontSize: 13, outline: "none", boxSizing: "border-box" }}
            />
          </div>

          <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13, color: "#374151", cursor: "pointer" }}>
            <input
              type="checkbox"
              checked={triggerRerun}
              onChange={e => setTriggerRerun(e.target.checked)}
              style={{ width: 16, height: 16, borderRadius: 4 }}
            />
            {labels.rerun}
          </label>

          {error && (
            <p style={{ fontSize: 13, color: "#dc2626", margin: 0 }}>{error}</p>
          )}

          <div style={{ display: "flex", gap: 8, paddingTop: 4 }}>
            <button
              onClick={handleSave}
              disabled={saving}
              style={{
                flex: 1, background: "#ef4444", color: "white", border: "none",
                borderRadius: 10, padding: "10px 16px", fontSize: 13, fontWeight: 600,
                cursor: saving ? "not-allowed" : "pointer", opacity: saving ? 0.6 : 1,
              }}
            >
              {saving ? labels.saving : labels.save}
            </button>
            <button
              onClick={onClose}
              style={{
                background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
                padding: "10px 16px", fontSize: 13, color: "#64748b", cursor: "pointer",
              }}
            >
              {labels.cancel}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
