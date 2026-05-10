"use client";

import { useState, useCallback, useRef } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";
import Modal from "@/components/Modal";

interface ImportTaskDto {
  name: string;
  shiftDurationHours: number;
  requiredHeadcount: number;
}

interface ImportAssignmentDto {
  personName: string;
  taskName: string;
  dayOfWeek: string;
  startHour: number;
  endHour: number;
}

interface ImportPreview {
  people: string[];
  tasks: ImportTaskDto[];
  assignments: ImportAssignmentDto[];
  aiConfidence: string | null;
  parseMethod: "structured" | "ai" | null;
  warnings: string[] | null;
}

type ModalState = "idle" | "parsing" | "preview" | "confirming" | "done";

interface Props {
  groupId: string;
  open: boolean;
  onClose: () => void;
}

const ACCEPTED_TYPES = ".xlsx,.xls,.csv,.png,.jpg,.jpeg,.pdf";
const MAX_SIZE = 10 * 1024 * 1024; // 10MB

const DAY_LABELS: Record<string, string> = {
  sunday: "א׳",
  monday: "ב׳",
  tuesday: "ג׳",
  wednesday: "ד׳",
  thursday: "ה׳",
  friday: "ו׳",
  saturday: "ש׳",
};

export default function SmartImportModal({ groupId, open, onClose }: Props) {
  const t = useTranslations("import");
  const { currentSpaceId } = useSpaceStore();

  const [state, setState] = useState<ModalState>("idle");
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [draftVersionId, setDraftVersionId] = useState<string | null>(null);
  const [includedPeople, setIncludedPeople] = useState<Set<string>>(new Set());
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);

  const reset = useCallback(() => {
    setState("idle");
    setPreview(null);
    setError(null);
    setDraftVersionId(null);
    setIncludedPeople(new Set());
  }, []);

  const handleClose = useCallback(() => {
    reset();
    onClose();
  }, [reset, onClose]);

  const handleFile = useCallback(async (file: File) => {
    if (file.size > MAX_SIZE) {
      setError(t("maxSize"));
      return;
    }

    setError(null);
    setState("parsing");

    try {
      const formData = new FormData();
      formData.append("file", file);

      const { data } = await apiClient.post<ImportPreview>(
        `/spaces/${currentSpaceId}/groups/${groupId}/import/parse`,
        formData,
        { headers: { "Content-Type": "multipart/form-data" } }
      );

      setPreview(data);
      setIncludedPeople(new Set(data.people));
      setState("preview");
    } catch (err: unknown) {
      setState("idle");
      const axiosErr = err as { response?: { status?: number; data?: { error?: string } } };
      setError(axiosErr?.response?.data?.error || t("parseError"));
    }
  }, [currentSpaceId, groupId, t]);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFile(file);
  }, [handleFile]);

  const handleConfirm = useCallback(async () => {
    if (!preview) return;

    setState("confirming");
    setError(null);

    try {
      const filteredPeople = preview.people.filter(p => includedPeople.has(p));
      const { data } = await apiClient.post(
        `/spaces/${currentSpaceId}/groups/${groupId}/import/confirm`,
        {
          people: filteredPeople,
          tasks: preview.tasks,
          assignments: preview.assignments.filter(a => includedPeople.has(a.personName)),
        }
      );

      setDraftVersionId(data.draftVersionId);
      setState("done");
    } catch {
      setState("preview");
      setError(t("parseError"));
    }
  }, [preview, includedPeople, currentSpaceId, groupId, t]);

  const togglePerson = (name: string) => {
    setIncludedPeople(prev => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  };

  const handleDownloadTemplate = useCallback(async () => {
    try {
      const response = await apiClient.get(
        `/spaces/${currentSpaceId}/groups/${groupId}/import/template`,
        { responseType: 'blob' }
      );
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      link.href = url;
      link.download = 'import-template.csv';
      link.click();
      window.URL.revokeObjectURL(url);
    } catch {
      // Silently fail — template download is not critical
    }
  }, [currentSpaceId, groupId]);

  if (!open) return null;

  return (
    <Modal open={open} onClose={handleClose} title={t("title")}>
      <div className="space-y-4 max-h-[70vh] overflow-y-auto">
        {/* Idle — file upload */}
        {state === "idle" && (
          <div className="space-y-3">
            <p className="text-sm text-slate-500">{t("subtitle")}</p>

            <div
              onDragOver={e => { e.preventDefault(); setDragOver(true); }}
              onDragLeave={() => setDragOver(false)}
              onDrop={handleDrop}
              onClick={() => fileInputRef.current?.click()}
              className={`border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-colors ${
                dragOver ? "border-blue-400 bg-blue-50" : "border-slate-200 hover:border-slate-300 hover:bg-slate-50"
              }`}
            >
              <svg className="mx-auto h-10 w-10 text-slate-300 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
              </svg>
              <p className="text-sm text-slate-600 font-medium">{t("dropzone")}</p>
              <p className="text-xs text-slate-400 mt-1">{t("supportedFormats")}</p>
              <p className="text-xs text-slate-400">{t("maxSize")}</p>
            </div>

            <input
              ref={fileInputRef}
              type="file"
              accept={ACCEPTED_TYPES}
              className="hidden"
              onChange={e => {
                const file = e.target.files?.[0];
                if (file) handleFile(file);
              }}
            />

            {/* Field explanation */}
            <div className="bg-slate-50 border border-slate-200 rounded-xl px-4 py-3 space-y-1">
              <p className="text-xs text-slate-600 font-medium">{t("fieldExplanation")}</p>
              <p className="text-xs text-slate-400">{t("optionalColumns")}</p>
              <button
                onClick={handleDownloadTemplate}
                type="button"
                className="inline-flex items-center gap-1 text-xs text-blue-600 hover:text-blue-700 font-medium mt-1"
              >
                <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                </svg>
                {t("downloadTemplate")}
              </button>
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 space-y-1">
                <p>{error}</p>
                <button
                  onClick={handleDownloadTemplate}
                  type="button"
                  className="inline-flex items-center gap-1 text-xs text-blue-600 hover:text-blue-700 font-medium"
                >
                  {t("downloadTemplate")}
                </button>
              </div>
            )}
          </div>
        )}

        {/* Parsing — spinner */}
        {state === "parsing" && (
          <div className="flex flex-col items-center py-12 gap-3">
            <svg className="animate-spin h-8 w-8 text-blue-500" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            <p className="text-sm text-slate-600">{t("parsing")}</p>
          </div>
        )}

        {/* Preview — show parsed data */}
        {state === "preview" && preview && (
          <div className="space-y-4">
            <div className="bg-blue-50 border border-blue-200 rounded-xl px-4 py-3">
              <p className="text-sm text-blue-800 font-medium">
                {t("found", {
                  people: String(preview.people.length),
                  tasks: String(preview.tasks.length),
                  assignments: String(preview.assignments.length),
                })}
              </p>
              {preview.aiConfidence && (
                <p className="text-xs text-blue-600 mt-1">
                  AI confidence: {preview.aiConfidence}
                </p>
              )}
              {preview.parseMethod && (
                <span className={`inline-block text-xs px-2 py-0.5 rounded-full font-medium mt-1 ${
                  preview.parseMethod === "structured"
                    ? "bg-emerald-100 text-emerald-700"
                    : "bg-purple-100 text-purple-700"
                }`}>
                  {preview.parseMethod === "structured" ? t("parseMethodStructured") : t("parseMethodAi")}
                </span>
              )}
            </div>

            {preview.warnings && preview.warnings.length > 0 && (
              <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">
                <p className="text-xs font-medium text-amber-700 mb-1">{t("warningsTitle")}</p>
                <ul className="text-xs text-amber-600 space-y-0.5 max-h-24 overflow-y-auto">
                  {preview.warnings.map((w, i) => <li key={i}>{w}</li>)}
                </ul>
              </div>
            )}

            {/* People */}
            {preview.people.length > 0 && (
              <div>
                <h4 className="text-sm font-semibold text-slate-700 mb-2">
                  אנשים ({preview.people.length})
                </h4>
                <div className="space-y-1 max-h-32 overflow-y-auto">
                  {preview.people.map(name => (
                    <label key={name} className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={includedPeople.has(name)}
                        onChange={() => togglePerson(name)}
                        className="rounded border-slate-300 text-blue-500 focus:ring-blue-500"
                      />
                      {name}
                    </label>
                  ))}
                </div>
              </div>
            )}

            {/* Tasks */}
            {preview.tasks.length > 0 && (
              <div>
                <h4 className="text-sm font-semibold text-slate-700 mb-2">
                  משימות ({preview.tasks.length})
                </h4>
                <div className="border border-slate-200 rounded-lg overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50">
                      <tr>
                        <th className="text-right px-3 py-2 text-slate-600 font-medium">שם</th>
                        <th className="text-right px-3 py-2 text-slate-600 font-medium">שעות</th>
                        <th className="text-right px-3 py-2 text-slate-600 font-medium">נדרשים</th>
                      </tr>
                    </thead>
                    <tbody>
                      {preview.tasks.map((task, i) => (
                        <tr key={i} className="border-t border-slate-100">
                          <td className="px-3 py-2">{task.name}</td>
                          <td className="px-3 py-2">{task.shiftDurationHours}h</td>
                          <td className="px-3 py-2">{task.requiredHeadcount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Assignments grouped by day */}
            {preview.assignments.length > 0 && (
              <div>
                <h4 className="text-sm font-semibold text-slate-700 mb-2">
                  שיבוצים ({preview.assignments.length})
                </h4>
                <div className="border border-slate-200 rounded-lg overflow-hidden max-h-48 overflow-y-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 sticky top-0">
                      <tr>
                        <th className="text-right px-3 py-2 text-slate-600 font-medium">יום</th>
                        <th className="text-right px-3 py-2 text-slate-600 font-medium">אדם</th>
                        <th className="text-right px-3 py-2 text-slate-600 font-medium">משימה</th>
                        <th className="text-right px-3 py-2 text-slate-600 font-medium">שעות</th>
                      </tr>
                    </thead>
                    <tbody>
                      {preview.assignments.map((a, i) => (
                        <tr key={i} className="border-t border-slate-100">
                          <td className="px-3 py-1.5">{DAY_LABELS[a.dayOfWeek] || a.dayOfWeek}</td>
                          <td className="px-3 py-1.5">{a.personName}</td>
                          <td className="px-3 py-1.5">{a.taskName}</td>
                          <td className="px-3 py-1.5">{a.startHour}:00-{a.endHour}:00</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {error && (
              <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</p>
            )}

            <div className="flex gap-2 pt-2">
              <button
                onClick={handleConfirm}
                className="flex-1 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors"
              >
                {t("confirm")}
              </button>
              <button
                onClick={handleClose}
                className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors"
              >
                ביטול
              </button>
            </div>
          </div>
        )}

        {/* Confirming — spinner */}
        {state === "confirming" && (
          <div className="flex flex-col items-center py-12 gap-3">
            <svg className="animate-spin h-8 w-8 text-blue-500" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            <p className="text-sm text-slate-600">{t("confirming")}</p>
          </div>
        )}

        {/* Done — success */}
        {state === "done" && (
          <div className="flex flex-col items-center py-8 gap-4">
            <div className="w-12 h-12 bg-emerald-100 rounded-full flex items-center justify-center">
              <svg className="h-6 w-6 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <p className="text-sm text-slate-700 font-medium">{t("done")}</p>
            {draftVersionId && (
              <button
                onClick={handleClose}
                className="text-sm text-blue-600 hover:text-blue-700 font-medium"
              >
                {t("viewDraft")}
              </button>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
}
