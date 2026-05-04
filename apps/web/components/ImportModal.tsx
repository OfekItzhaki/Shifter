"use client";

import { useState, useRef } from "react";
import { useLocale } from "next-intl";
import ExcelJS from "exceljs";
import { createPerson } from "@/lib/api/people";
import { addGroupMemberById } from "@/lib/api/groups";
import { createGroupTask } from "@/lib/api/tasks";

// ── Types ─────────────────────────────────────────────────────────────────────

type ImportMode = "members" | "tasks";

interface MemberRow {
  fullName: string;
  displayName?: string;
  phone?: string;
  email?: string;
}

interface TaskRow {
  name: string;
  shiftDurationMinutes: number;
  requiredHeadcount: number;
  burdenLevel: string;
  dailyStartTime?: string;
  dailyEndTime?: string;
}

type RowStatus = "pending" | "ok" | "error" | "skip";

interface ImportRow<T> {
  data: T;
  status: RowStatus;
  message?: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  spaceId: string;
  groupId: string;
  /** Called when at least one row was imported successfully */
  onImported: () => void;
}

// ── Column spec shown to the user ─────────────────────────────────────────────

const MEMBER_COLUMNS = [
  { key: "Full Name",     required: true,  example: "John Smith" },
  { key: "Display Name", required: false, example: "Johnny" },
  { key: "Phone",        required: false, example: "+972501234567" },
  { key: "Email",        required: false, example: "john@example.com" },
];

const TASK_COLUMNS = [
  { key: "Name",                    required: true,  example: "Kitchen" },
  { key: "Shift Duration (hours)",  required: true,  example: "4" },
  { key: "Headcount",               required: true,  example: "1" },
  { key: "Burden Level",            required: false, example: "neutral / disliked / hated / favorable" },
  { key: "Daily Start",             required: false, example: "08:00" },
  { key: "Daily End",               required: false, example: "22:00" },
];

// ── Parsers ───────────────────────────────────────────────────────────────────

function parseMemberSheet(rows: Record<string, string>[]): MemberRow[] {
  return rows
    .map(r => ({
      fullName:    (r["Full Name"] ?? r["full name"] ?? r["שם מלא"] ?? "").trim(),
      displayName: (r["Display Name"] ?? r["display name"] ?? r["שם תצוגה"] ?? "").trim() || undefined,
      phone:       (r["Phone"] ?? r["phone"] ?? r["טלפון"] ?? "").trim() || undefined,
      email:       (r["Email"] ?? r["email"] ?? r["אימייל"] ?? "").trim() || undefined,
    }))
    .filter(r => r.fullName.length > 0);
}

function parseTaskSheet(rows: Record<string, string>[]): TaskRow[] {
  const BURDEN_MAP: Record<string, string> = {
    favorable: "favorable", נוח: "favorable",
    neutral: "neutral", ניטרלי: "neutral",
    disliked: "disliked", "לא אהוב": "disliked",
    hated: "hated", שנוא: "hated",
  };

  return rows
    .map(r => {
      const name = (r["Name"] ?? r["name"] ?? r["שם"] ?? "").trim();
      const hours = parseFloat(r["Shift Duration (hours)"] ?? r["משך משמרת (שעות)"] ?? "4");
      const headcount = parseInt(r["Headcount"] ?? r["מספר נדרש"] ?? "1", 10);
      const burdenRaw = (r["Burden Level"] ?? r["רמת עומס"] ?? "neutral").trim().toLowerCase();
      const burden = BURDEN_MAP[burdenRaw] ?? "neutral";
      const dailyStart = (r["Daily Start"] ?? r["שעת התחלה"] ?? "").trim() || undefined;
      const dailyEnd   = (r["Daily End"]   ?? r["שעת סיום"]   ?? "").trim() || undefined;

      return {
        name,
        shiftDurationMinutes: Math.max(1, Math.round((isNaN(hours) ? 4 : hours) * 60)),
        requiredHeadcount: isNaN(headcount) ? 1 : Math.max(1, headcount),
        burdenLevel: burden,
        dailyStartTime: dailyStart,
        dailyEndTime: dailyEnd,
      };
    })
    .filter(r => r.name.length > 0);
}

function readWorkbook(file: File): Promise<Record<string, string>[]> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = async e => {
      try {
        const buffer = e.target!.result as ArrayBuffer;
        const wb = new ExcelJS.Workbook();
        await wb.xlsx.load(buffer);
        const ws = wb.worksheets[0];
        if (!ws) { resolve([]); return; }

        // First row = headers
        const headers: string[] = [];
        ws.getRow(1).eachCell({ includeEmpty: true }, (cell, colNumber) => {
          headers[colNumber - 1] = String(cell.value ?? "").trim();
        });

        const rows: Record<string, string>[] = [];
        ws.eachRow((row, rowNumber) => {
          if (rowNumber === 1) return; // skip header
          const obj: Record<string, string> = {};
          row.eachCell({ includeEmpty: true }, (cell, colNumber) => {
            const header = headers[colNumber - 1];
            if (header) obj[header] = String(cell.value ?? "").trim();
          });
          rows.push(obj);
        });
        resolve(rows);
      } catch (err) {
        reject(err);
      }
    };
    reader.onerror = reject;
    reader.readAsArrayBuffer(file);
  });
}

// ── Main component ────────────────────────────────────────────────────────────

export default function ImportModal({ open, onClose, spaceId, groupId, onImported }: Props) {
  const locale = useLocale();
  const isRtl = locale === "he";
  const fileRef = useRef<HTMLInputElement>(null);

  const [mode, setMode] = useState<ImportMode>("members");
  const [step, setStep] = useState<"instructions" | "preview" | "importing" | "done">("instructions");
  const [memberRows, setMemberRows] = useState<ImportRow<MemberRow>[]>([]);
  const [taskRows, setTaskRows] = useState<ImportRow<TaskRow>[]>([]);
  const [importedCount, setImportedCount] = useState(0);
  const [errorCount, setErrorCount] = useState(0);
  const [fileError, setFileError] = useState<string | null>(null);

  if (!open) return null;

  const L = {
    title: isRtl ? "ייבוא מ-Excel" : "Import from Excel",
    members: isRtl ? "חברים" : "Members",
    tasks: isRtl ? "משימות" : "Tasks",
    instructions: isRtl ? "הוראות" : "Instructions",
    preview: isRtl ? "תצוגה מקדימה" : "Preview",
    chooseFile: isRtl ? "בחר קובץ Excel / CSV" : "Choose Excel / CSV file",
    orDrag: isRtl ? "או גרור לכאן" : "or drag & drop here",
    import: isRtl ? "ייבא" : "Import",
    importing: isRtl ? "מייבא..." : "Importing...",
    done: isRtl ? "הסתיים" : "Done",
    close: isRtl ? "סגור" : "Close",
    back: isRtl ? "חזרה" : "Back",
    required: isRtl ? "חובה" : "required",
    optional: isRtl ? "אופציונלי" : "optional",
    column: isRtl ? "עמודה" : "Column",
    example: isRtl ? "דוגמה" : "Example",
    rows: isRtl ? "שורות" : "rows",
    ok: isRtl ? "בסדר" : "OK",
    error: isRtl ? "שגיאה" : "Error",
    skip: isRtl ? "דלג" : "Skip",
    importedMsg: (n: number, e: number) =>
      isRtl ? `יובאו ${n} רשומות${e > 0 ? `, ${e} שגיאות` : ""}` : `Imported ${n} records${e > 0 ? `, ${e} errors` : ""}`,
    downloadTemplate: isRtl ? "הורד תבנית" : "Download template",
  };

  async function handleFile(file: File) {
    setFileError(null);
    try {
      const rows = await readWorkbook(file);
      if (rows.length === 0) {
        setFileError(isRtl ? "הקובץ ריק" : "File is empty");
        return;
      }
      if (mode === "members") {
        const parsed = parseMemberSheet(rows);
        setMemberRows(parsed.map(d => ({ data: d, status: "pending" })));
      } else {
        const parsed = parseTaskSheet(rows);
        setTaskRows(parsed.map(d => ({ data: d, status: "pending" })));
      }
      setStep("preview");
    } catch {
      setFileError(isRtl ? "שגיאה בקריאת הקובץ. ודא שהוא Excel או CSV תקין." : "Error reading file. Make sure it's a valid Excel or CSV file.");
    }
  }

  async function handleImport() {
    setStep("importing");
    let ok = 0;
    let err = 0;

    if (mode === "members") {
      const updated = [...memberRows];
      for (let i = 0; i < updated.length; i++) {
        const row = updated[i];
        if (row.status !== "pending") continue;
        try {
          const person = await createPerson(spaceId, row.data.fullName, row.data.displayName);
          await addGroupMemberById(spaceId, groupId, person.id);
          updated[i] = { ...row, status: "ok" };
          ok++;
        } catch (e: unknown) {
          const status = (e as { response?: { status?: number } })?.response?.status;
          if (status === 409) {
            updated[i] = { ...row, status: "skip", message: isRtl ? "כבר קיים" : "Already exists" };
          } else {
            updated[i] = { ...row, status: "error", message: isRtl ? "שגיאה" : "Error" };
            err++;
          }
        }
        setMemberRows([...updated]);
      }
    } else {
      const updated = [...taskRows];
      const now = new Date();
      const startsAt = now.toISOString();
      const endsAt = new Date(now.getTime() + 90 * 86400000).toISOString();

      for (let i = 0; i < updated.length; i++) {
        const row = updated[i];
        if (row.status !== "pending") continue;
        try {
          await createGroupTask(spaceId, groupId, {
            name: row.data.name,
            startsAt,
            endsAt,
            shiftDurationMinutes: row.data.shiftDurationMinutes,
            requiredHeadcount: row.data.requiredHeadcount,
            burdenLevel: row.data.burdenLevel,
            allowsDoubleShift: false,
            allowsOverlap: false,
            dailyStartTime: row.data.dailyStartTime || null,
            dailyEndTime: row.data.dailyEndTime || null,
            requiredQualificationNames: [],
          });
          updated[i] = { ...row, status: "ok" };
          ok++;
        } catch {
          updated[i] = { ...row, status: "error", message: isRtl ? "שגיאה" : "Error" };
          err++;
        }
        setTaskRows([...updated]);
      }
    }

    setImportedCount(ok);
    setErrorCount(err);
    setStep("done");
    if (ok > 0) onImported();
  }

  async function downloadTemplate() {
    const cols = mode === "members" ? MEMBER_COLUMNS : TASK_COLUMNS;
    const wb = new ExcelJS.Workbook();
    const ws = wb.addWorksheet(mode === "members" ? "Members" : "Tasks");

    // Header row — bold
    ws.addRow(cols.map(c => c.key)).eachCell(cell => {
      cell.font = { bold: true };
    });
    // Example row
    ws.addRow(cols.map(c => c.example));

    // Auto-width columns
    cols.forEach((_, i) => {
      ws.getColumn(i + 1).width = 22;
    });

    const buffer = await wb.xlsx.writeBuffer();
    const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${mode}-template.xlsx`;
    a.click();
    URL.revokeObjectURL(url);
  }

  function reset() {
    setStep("instructions");
    setMemberRows([]);
    setTaskRows([]);
    setImportedCount(0);
    setErrorCount(0);
    setFileError(null);
  }

  const rows = mode === "members" ? memberRows : taskRows;
  const cols = mode === "members" ? MEMBER_COLUMNS : TASK_COLUMNS;

  return (
    <div
      style={{ position: "fixed", inset: 0, zIndex: 70, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: "1rem" }}
      onClick={onClose}
    >
      <div
        style={{ background: "white", borderRadius: 20, boxShadow: "0 24px 64px rgba(0,0,0,0.18)", width: "100%", maxWidth: 640, maxHeight: "90vh", display: "flex", flexDirection: "column" }}
        dir={isRtl ? "rtl" : "ltr"}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div style={{ padding: "1.25rem 1.5rem", borderBottom: "1px solid #e2e8f0", display: "flex", alignItems: "center", justifyContent: "space-between", flexShrink: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ fontSize: 20 }}>📥</span>
            <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: 0 }}>{L.title}</h2>
          </div>
          <button onClick={onClose} style={{ background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 4 }}>
            <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Mode tabs — only on instructions step */}
        {step === "instructions" && (
          <div style={{ display: "flex", gap: 0, padding: "0 1.5rem", borderBottom: "1px solid #e2e8f0", flexShrink: 0 }}>
            {(["members", "tasks"] as ImportMode[]).map(m => (
              <button
                key={m}
                onClick={() => setMode(m)}
                style={{
                  padding: "10px 16px", fontSize: 13, fontWeight: 600, border: "none", background: "none",
                  cursor: "pointer", borderBottom: mode === m ? "2px solid #3b82f6" : "2px solid transparent",
                  color: mode === m ? "#3b82f6" : "#64748b", marginBottom: -1,
                }}
              >
                {m === "members" ? L.members : L.tasks}
              </button>
            ))}
          </div>
        )}

        {/* Body */}
        <div style={{ flex: 1, overflowY: "auto", padding: "1.25rem 1.5rem" }}>

          {/* ── Instructions step ── */}
          {step === "instructions" && (
            <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <p style={{ fontSize: 13, color: "#64748b", margin: 0 }}>
                {isRtl
                  ? `הכן קובץ Excel או CSV עם העמודות הבאות. השורה הראשונה חייבת להיות כותרות.`
                  : `Prepare an Excel or CSV file with the following columns. The first row must be the headers.`}
              </p>

              {/* Column table */}
              <div style={{ border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                  <thead>
                    <tr style={{ background: "#f8fafc" }}>
                      <th style={{ padding: "8px 12px", textAlign: isRtl ? "right" : "left", fontWeight: 600, color: "#475569", borderBottom: "1px solid #e2e8f0" }}>{L.column}</th>
                      <th style={{ padding: "8px 12px", textAlign: isRtl ? "right" : "left", fontWeight: 600, color: "#475569", borderBottom: "1px solid #e2e8f0" }}>{L.example}</th>
                      <th style={{ padding: "8px 12px", textAlign: "center", fontWeight: 600, color: "#475569", borderBottom: "1px solid #e2e8f0" }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {cols.map((col, i) => (
                      <tr key={col.key} style={{ borderBottom: i < cols.length - 1 ? "1px solid #f1f5f9" : "none" }}>
                        <td style={{ padding: "8px 12px", fontWeight: 600, color: "#1e293b" }}>{col.key}</td>
                        <td style={{ padding: "8px 12px", color: "#64748b", fontFamily: "monospace", fontSize: 12 }}>{col.example}</td>
                        <td style={{ padding: "8px 12px", textAlign: "center" }}>
                          <span style={{
                            fontSize: 11, fontWeight: 600, padding: "2px 8px", borderRadius: 999,
                            background: col.required ? "#fef2f2" : "#f0fdf4",
                            color: col.required ? "#dc2626" : "#16a34a",
                            border: `1px solid ${col.required ? "#fecaca" : "#bbf7d0"}`,
                          }}>
                            {col.required ? L.required : L.optional}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Download template */}
              <button
                onClick={downloadTemplate}
                style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 13, color: "#3b82f6", background: "none", border: "1px solid #bfdbfe", borderRadius: 8, padding: "7px 14px", cursor: "pointer", width: "fit-content" }}
              >
                <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                </svg>
                {L.downloadTemplate}
              </button>

              {/* File drop zone */}
              <div
                onDragOver={e => e.preventDefault()}
                onDrop={e => { e.preventDefault(); const f = e.dataTransfer.files[0]; if (f) handleFile(f); }}
                onClick={() => fileRef.current?.click()}
                style={{
                  border: "2px dashed #cbd5e1", borderRadius: 14, padding: "2rem 1rem",
                  textAlign: "center", cursor: "pointer", background: "#f8fafc",
                  transition: "border-color 0.15s",
                }}
              >
                <div style={{ fontSize: 32, marginBottom: 8 }}>📂</div>
                <p style={{ fontSize: 14, fontWeight: 600, color: "#374151", margin: "0 0 4px" }}>{L.chooseFile}</p>
                <p style={{ fontSize: 12, color: "#94a3b8", margin: 0 }}>{L.orDrag}</p>
                <input
                  ref={fileRef}
                  type="file"
                  accept=".xlsx,.xls,.csv"
                  style={{ display: "none" }}
                  onChange={e => { const f = e.target.files?.[0]; if (f) handleFile(f); }}
                />
              </div>

              {fileError && <p style={{ fontSize: 13, color: "#dc2626", margin: 0 }}>{fileError}</p>}
            </div>
          )}

          {/* ── Preview step ── */}
          {step === "preview" && (
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <p style={{ fontSize: 13, color: "#64748b", margin: 0 }}>
                {isRtl ? `נמצאו ${rows.length} ${L.rows}. בדוק ולחץ ייבא.` : `Found ${rows.length} ${L.rows}. Review and click Import.`}
              </p>
              <div style={{ border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden", maxHeight: 320, overflowY: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 12 }}>
                  <thead>
                    <tr style={{ background: "#f8fafc", position: "sticky", top: 0 }}>
                      {mode === "members" ? (
                        <>
                          <th style={thStyle}>Full Name</th>
                          <th style={thStyle}>Display Name</th>
                          <th style={thStyle}>Phone</th>
                          <th style={thStyle}>Email</th>
                        </>
                      ) : (
                        <>
                          <th style={thStyle}>Name</th>
                          <th style={thStyle}>Duration</th>
                          <th style={thStyle}>Headcount</th>
                          <th style={thStyle}>Burden</th>
                        </>
                      )}
                    </tr>
                  </thead>
                  <tbody>
                    {mode === "members"
                      ? (memberRows as ImportRow<MemberRow>[]).map((row, i) => (
                          <tr key={i} style={{ borderBottom: "1px solid #f1f5f9" }}>
                            <td style={tdStyle}>{row.data.fullName}</td>
                            <td style={tdStyle}>{row.data.displayName ?? "—"}</td>
                            <td style={tdStyle}>{row.data.phone ?? "—"}</td>
                            <td style={tdStyle}>{row.data.email ?? "—"}</td>
                          </tr>
                        ))
                      : (taskRows as ImportRow<TaskRow>[]).map((row, i) => (
                          <tr key={i} style={{ borderBottom: "1px solid #f1f5f9" }}>
                            <td style={tdStyle}>{row.data.name}</td>
                            <td style={tdStyle}>{row.data.shiftDurationMinutes / 60}h</td>
                            <td style={tdStyle}>{row.data.requiredHeadcount}</td>
                            <td style={tdStyle}>{row.data.burdenLevel}</td>
                          </tr>
                        ))
                    }
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* ── Importing step ── */}
          {step === "importing" && (
            <div style={{ display: "flex", flexDirection: "column", gap: 8, maxHeight: 320, overflowY: "auto" }}>
              {rows.map((row, i) => {
                const name = mode === "members"
                  ? (row as ImportRow<MemberRow>).data.fullName
                  : (row as ImportRow<TaskRow>).data.name;
                return (
                  <div key={i} style={{ display: "flex", alignItems: "center", gap: 10, padding: "6px 0", borderBottom: "1px solid #f1f5f9" }}>
                    <span style={{ fontSize: 16, flexShrink: 0 }}>
                      {row.status === "ok" ? "✅" : row.status === "error" ? "❌" : row.status === "skip" ? "⏭" : "⏳"}
                    </span>
                    <span style={{ fontSize: 13, color: "#374151", flex: 1 }}>{name}</span>
                    {row.message && <span style={{ fontSize: 11, color: "#94a3b8" }}>{row.message}</span>}
                  </div>
                );
              })}
            </div>
          )}

          {/* ── Done step ── */}
          {step === "done" && (
            <div style={{ textAlign: "center", padding: "1.5rem 0" }}>
              <div style={{ fontSize: 48, marginBottom: 12 }}>{errorCount === 0 ? "🎉" : "⚠️"}</div>
              <p style={{ fontSize: 15, fontWeight: 700, color: "#0f172a", margin: "0 0 6px" }}>
                {L.importedMsg(importedCount, errorCount)}
              </p>
              {errorCount > 0 && (
                <p style={{ fontSize: 13, color: "#64748b", margin: 0 }}>
                  {isRtl ? "שורות עם שגיאות לא יובאו." : "Rows with errors were not imported."}
                </p>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div style={{ padding: "1rem 1.5rem", borderTop: "1px solid #e2e8f0", display: "flex", gap: 8, flexShrink: 0 }}>
          {step === "instructions" && (
            <button onClick={onClose} style={btnSecondary}>{L.close}</button>
          )}
          {step === "preview" && (
            <>
              <button onClick={handleImport} style={btnPrimary}>{L.import}</button>
              <button onClick={reset} style={btnSecondary}>{L.back}</button>
            </>
          )}
          {step === "importing" && (
            <button disabled style={{ ...btnPrimary, opacity: 0.6, cursor: "not-allowed" }}>{L.importing}</button>
          )}
          {step === "done" && (
            <>
              <button onClick={onClose} style={btnPrimary}>{L.done}</button>
              <button onClick={reset} style={btnSecondary}>{isRtl ? "ייבא עוד" : "Import more"}</button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Styles ────────────────────────────────────────────────────────────────────

const thStyle: React.CSSProperties = {
  padding: "8px 12px", textAlign: "left", fontWeight: 600,
  color: "#475569", borderBottom: "1px solid #e2e8f0", fontSize: 11,
  textTransform: "uppercase", letterSpacing: "0.05em",
};

const tdStyle: React.CSSProperties = {
  padding: "7px 12px", color: "#374151",
};

const btnPrimary: React.CSSProperties = {
  background: "#3b82f6", color: "white", border: "none",
  borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer",
};

const btnSecondary: React.CSSProperties = {
  background: "none", border: "1px solid #e2e8f0",
  borderRadius: 10, padding: "9px 16px", fontSize: 13, color: "#64748b", cursor: "pointer",
};
