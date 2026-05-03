"use client";

import { useEffect, useState } from "react";
import { apiClient } from "@/lib/api/client";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import ScheduleTable2D from "@/components/schedule/ScheduleTable2D";

interface Assignment {
  id: string;
  personId: string;
  personName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  spaceId: string;
  draftVersionId: string;
  /** Set of personIds belonging to this group — filters out cross-group assignments */
  groupMemberIds: Set<string>;
  isAdmin: boolean;
  onPublish: () => Promise<void>;
  onDiscard: () => Promise<void>;
  onRunAgain: () => void;
}

const DAY_NAMES = ["ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת"];

function getWeekDates(anchor: string): string[] {
  const start = new Date(anchor + "T00:00:00");
  start.setDate(start.getDate() - start.getDay());
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    return d.toISOString().split("T")[0];
  });
}

export default function DraftScheduleModal({
  open, onClose, spaceId, draftVersionId, groupMemberIds,
  isAdmin, onPublish, onDiscard, onRunAgain,
}: Props) {
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [loading, setLoading] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [discarding, setDiscarding] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);
  const { fDateShort } = useDateFormat();

  const today = new Date().toISOString().split("T")[0];
  const [weekAnchor, setWeekAnchor] = useState(today);
  const [selectedDay, setSelectedDay] = useState(new Date().getDay());

  useEffect(() => {
    if (!open || !draftVersionId) return;
    setLoading(true);
    setError(null);
    apiClient.get(`/spaces/${spaceId}/schedule-versions/${draftVersionId}`)
      .then(r => {
        const detail = r.data;
        const all: Assignment[] = (detail.assignments ?? []).map((a: any) => ({
          id: a.id,
          personId: a.personId,
          personName: a.personName,
          taskTypeName: a.taskTypeName,
          slotStartsAt: a.slotStartsAt,
          slotEndsAt: a.slotEndsAt,
        }));
        // Filter to this group's members only
        const filtered = groupMemberIds.size > 0
          ? all.filter(a => groupMemberIds.has(a.personId))
          : all;
        setAssignments(filtered);

        // Jump to the first day that has assignments
        if (filtered.length > 0) {
          const firstDate = filtered.map(a => a.slotStartsAt.split("T")[0]).sort()[0];
          if (firstDate) {
            setWeekAnchor(firstDate);
            setSelectedDay(new Date(firstDate + "T00:00:00").getDay());
          }
        }
      })
      .catch(() => setError("שגיאה בטעינת הטיוטה"))
      .finally(() => setLoading(false));
  }, [open, draftVersionId, spaceId]);

  async function handlePublish() {
    setPublishing(true);
    setError(null);
    try { await onPublish(); onClose(); }
    catch (e: any) { setError(e?.response?.data?.error ?? "שגיאה בפרסום"); }
    finally { setPublishing(false); }
  }

  async function handleDiscard() {
    setDiscarding(true);
    setError(null);
    try { await onDiscard(); onClose(); }
    catch (e: any) { setError(e?.response?.data?.error ?? "שגיאה בביטול"); }
    finally { setDiscarding(false); setShowDiscardConfirm(false); }
  }

  if (!open) return null;

  const weekDates = getWeekDates(weekAnchor);
  const selectedDate = weekDates[selectedDay] ?? weekDates[0];
  const weekLabel = weekDates[0] && weekDates[6]
    ? `${fDateShort(weekDates[0] + "T00:00:00")} – ${fDateShort(weekDates[6] + "T00:00:00")}`
    : "";

  function prevWeek() {
    const d = new Date(weekAnchor + "T00:00:00");
    d.setDate(d.getDate() - 7);
    setWeekAnchor(d.toISOString().split("T")[0]);
  }
  function nextWeek() {
    const d = new Date(weekAnchor + "T00:00:00");
    d.setDate(d.getDate() + 7);
    setWeekAnchor(d.toISOString().split("T")[0]);
  }

  // Convert to ScheduleTable2D format
  const tableAssignments = assignments.map(a => ({
    id: a.id,
    personId: a.personId,
    personName: a.personName,
    taskTypeName: a.taskTypeName,
    slotStartsAt: a.slotStartsAt,
    slotEndsAt: a.slotEndsAt,
    source: "solver",
  }));

  return (
    <div
      style={{
        position: "fixed", inset: 0, zIndex: 60,
        background: "rgba(0,0,0,0.5)",
        display: "flex", alignItems: "center", justifyContent: "center",
        padding: "1rem",
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: "white", borderRadius: 20,
          boxShadow: "0 24px 64px rgba(0,0,0,0.18)",
          width: "100%", maxWidth: 900,
          maxHeight: "90vh",
          display: "flex", flexDirection: "column",
          direction: "rtl",
        }}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div style={{
          padding: "1.25rem 1.5rem",
          borderBottom: "1px solid #e2e8f0",
          display: "flex", alignItems: "center", justifyContent: "space-between",
          flexShrink: 0,
        }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{
              background: "#fef3c7", color: "#92400e", border: "1px solid #fde68a",
              borderRadius: 999, padding: "2px 10px", fontSize: 12, fontWeight: 700,
            }}>טיוטה</span>
            <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: 0 }}>
              תצוגה מקדימה של הסידור
            </h2>
          </div>
          <button onClick={onClose} style={{ background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 4 }}>
            <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflowY: "auto", padding: "1rem 1.5rem" }}>
          {loading ? (
            <div style={{ display: "flex", justifyContent: "center", padding: "3rem 0", color: "#94a3b8" }}>
              <svg className="animate-spin" width="24" height="24" fill="none" viewBox="0 0 24 24">
                <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
            </div>
          ) : error ? (
            <p style={{ color: "#dc2626", fontSize: 14, textAlign: "center", padding: "2rem 0" }}>{error}</p>
          ) : assignments.length === 0 ? (
            <div style={{ textAlign: "center", padding: "2rem 0" }}>
              <p style={{ color: "#94a3b8", fontSize: 14, marginBottom: 12 }}>הסידור ריק — לא נמצאו שיבוצים בטיוטה זו.</p>
              <p style={{ color: "#64748b", fontSize: 13, marginBottom: 16 }}>ייתכן שהסולבר לא הצליח לבנות סידור עם האילוצים הנוכחיים.</p>
              {isAdmin && (
                <button onClick={() => { onClose(); onRunAgain(); }}
                  style={{ background: "#3b82f6", color: "white", border: "none", borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
                  🔄 הרץ שוב
                </button>
              )}
            </div>
          ) : (
            <div className="space-y-4">
              {/* Week navigation */}
              <div className="flex items-center gap-2">
                <button onClick={prevWeek} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                  </svg>
                </button>
                <button onClick={() => { setWeekAnchor(today); setSelectedDay(new Date().getDay()); }}
                  className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${weekDates.includes(today) ? "bg-blue-500 text-white border-blue-500" : "border-slate-200 text-slate-600 hover:bg-slate-50"}`}>
                  השבוע
                </button>
                <button onClick={nextWeek} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
                  </svg>
                </button>
                <span className="text-xs text-slate-500 mr-1">{weekLabel}</span>
              </div>

              {/* Day tabs */}
              <div className="flex gap-1 overflow-x-auto pb-1">
                {weekDates.map((d, i) => (
                  <button key={d} onClick={() => setSelectedDay(i)}
                    className={`flex-shrink-0 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                      i === selectedDay ? "bg-blue-500 text-white shadow-sm"
                      : d === today ? "bg-blue-50 text-blue-600 border border-blue-200"
                      : "bg-slate-100 text-slate-600 hover:bg-slate-200"
                    }`}>
                    {DAY_NAMES[i]}
                  </button>
                ))}
              </div>

              {/* 2D schedule table */}
              <ScheduleTable2D
                assignments={tableAssignments}
                filterDate={selectedDate}
              />
            </div>
          )}
        </div>

        {/* Footer */}
        {isAdmin && (
          <div style={{
            padding: "1rem 1.5rem",
            borderTop: "1px solid #e2e8f0",
            display: "flex", alignItems: "center", gap: 10,
            flexShrink: 0,
          }}>
            {showDiscardConfirm ? (
              <>
                <p style={{ fontSize: 13, color: "#dc2626", flex: 1, margin: 0 }}>האם לבטל את הטיוטה? פעולה זו אינה הפיכה.</p>
                <button onClick={handleDiscard} disabled={discarding}
                  style={{ background: "#ef4444", color: "white", border: "none", borderRadius: 10, padding: "8px 16px", fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
                  {discarding ? "מבטל..." : "כן, בטל"}
                </button>
                <button onClick={() => setShowDiscardConfirm(false)}
                  style={{ background: "none", border: "1px solid #e2e8f0", borderRadius: 10, padding: "8px 14px", fontSize: 13, color: "#64748b", cursor: "pointer" }}>
                  חזור
                </button>
              </>
            ) : (
              <>
                <button onClick={handlePublish} disabled={publishing || discarding || loading}
                  style={{ background: "#10b981", color: "white", border: "none", borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer", opacity: publishing ? 0.6 : 1 }}>
                  {publishing ? "מפרסם..." : "✓ פרסם סידור"}
                </button>
                <button onClick={() => { onClose(); onRunAgain(); }} disabled={publishing || discarding}
                  style={{ background: "#3b82f6", color: "white", border: "none", borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
                  🔄 הרץ שוב
                </button>
                <button onClick={() => setShowDiscardConfirm(true)} disabled={publishing || discarding}
                  style={{ background: "none", border: "1px solid #fca5a5", color: "#dc2626", borderRadius: 10, padding: "9px 16px", fontSize: 13, cursor: "pointer", marginRight: "auto" }}>
                  ✕ בטל טיוטה
                </button>
                {error && <p style={{ fontSize: 12, color: "#dc2626", margin: 0 }}>{error}</p>}
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
