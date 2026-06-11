"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  listShiftTemplates,
  createShiftTemplate,
  updateShiftTemplate,
  deleteShiftTemplate,
  ShiftTemplateDto,
  CreateShiftTemplatePayload,
  UpdateShiftTemplatePayload,
} from "@/lib/api/selfService";
import { validateTemplateTimeRange } from "@/lib/utils/selfServiceValidation";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import { formatTime24h } from "@/lib/utils/selfServiceFormat";
import type { GroupTaskDto } from "@/lib/api/tasks";
import Modal from "@/components/Modal";
import LoadingCard from "./LoadingCard";
import ErrorRetry from "./ErrorRetry";
import MutationButton from "./MutationButton";

interface ShiftTemplatesTabProps {
  spaceId: string;
  groupId: string;
  tasks: GroupTaskDto[];
}

/** Days of week indexed 0=Sunday through 6=Saturday */
const DAYS_OF_WEEK = [0, 1, 2, 3, 4, 5, 6] as const;

interface TemplateFormState {
  dayOfWeek: number | "";
  startTime: string;
  endTime: string;
  requiredHeadcount: number;
  groupTaskId: string;
}

const EMPTY_FORM: TemplateFormState = {
  dayOfWeek: "",
  startTime: "",
  endTime: "",
  requiredHeadcount: 1,
  groupTaskId: "",
};

export default function ShiftTemplatesTab({ spaceId, groupId, tasks }: ShiftTemplatesTabProps) {
  const t = useTranslations("selfService.templates");

  // ── Data state ───────────────────────────────────────────────────────────
  const [templates, setTemplates] = useState<ShiftTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // ── Create form state ────────────────────────────────────────────────────
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createForm, setCreateForm] = useState<TemplateFormState>(EMPTY_FORM);
  const [createValidationError, setCreateValidationError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // ── Edit state ───────────────────────────────────────────────────────────
  const [editingTemplate, setEditingTemplate] = useState<ShiftTemplateDto | null>(null);
  const [editForm, setEditForm] = useState<TemplateFormState>(EMPTY_FORM);
  const [editValidationError, setEditValidationError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [editError, setEditError] = useState<string | null>(null);

  // ── Delete state ─────────────────────────────────────────────────────────
  const [deleteTarget, setDeleteTarget] = useState<ShiftTemplateDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  // ── Fetch templates ──────────────────────────────────────────────────────
  const fetchTemplates = useCallback(async (showLoading = true) => {
    try {
      if (showLoading) {
        setLoading(true);
      }
      setError(null);
      const data = await listShiftTemplates(spaceId, groupId);
      setTemplates(data.filter((tpl) => !tpl.isDeleted));
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      if (showLoading) {
        setLoading(false);
      }
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(() => fetchTemplates());
  }, [fetchTemplates]);

  // ── Day name helper ──────────────────────────────────────────────────────
  function getDayName(dayOfWeek: number): string {
    const dayKeys = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];
    return t(`days.${dayKeys[dayOfWeek]}`);
  }

  // ── Create handlers ──────────────────────────────────────────────────────
  function openCreateForm() {
    setCreateForm(EMPTY_FORM);
    setCreateValidationError(null);
    setCreateError(null);
    setShowCreateForm(true);
  }

  function closeCreateForm() {
    setShowCreateForm(false);
    setCreateForm(EMPTY_FORM);
    setCreateValidationError(null);
    setCreateError(null);
  }

  async function handleCreate() {
    setCreateValidationError(null);
    setCreateError(null);

    // Validate required fields
    if (createForm.dayOfWeek === "" || !createForm.startTime || !createForm.endTime || !createForm.groupTaskId) {
      return;
    }

    // Validate time range
    const timeValidation = validateTemplateTimeRange(createForm.startTime, createForm.endTime);
    if (!timeValidation.valid) {
      setCreateValidationError(t("validation.startAfterEnd"));
      return;
    }

    // Validate headcount
    if (createForm.requiredHeadcount < 1 || createForm.requiredHeadcount > 999) {
      setCreateValidationError(t("validation.headcountRange"));
      return;
    }

    setCreating(true);
    try {
      const payload: CreateShiftTemplatePayload = {
        dayOfWeek: createForm.dayOfWeek as number,
        startTime: createForm.startTime,
        endTime: createForm.endTime,
        requiredHeadcount: createForm.requiredHeadcount,
        groupTaskId: createForm.groupTaskId,
      };
      await createShiftTemplate(spaceId, groupId, payload);
      closeCreateForm();
      await fetchTemplates();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setCreateError(message);
      await fetchTemplates(false);
    } finally {
      setCreating(false);
    }
  }

  // ── Edit handlers ────────────────────────────────────────────────────────
  function openEditModal(template: ShiftTemplateDto) {
    setEditingTemplate(template);
    setEditForm({
      dayOfWeek: template.dayOfWeek,
      startTime: formatTime24h(template.startTime),
      endTime: formatTime24h(template.endTime),
      requiredHeadcount: template.requiredHeadcount,
      groupTaskId: template.groupTaskId,
    });
    setEditValidationError(null);
    setEditError(null);
  }

  function closeEditModal() {
    setEditingTemplate(null);
    setEditForm(EMPTY_FORM);
    setEditValidationError(null);
    setEditError(null);
  }

  async function handleEditSave() {
    if (!editingTemplate) return;
    setEditValidationError(null);
    setEditError(null);

    // Validate required fields
    if (editForm.dayOfWeek === "" || !editForm.startTime || !editForm.endTime) {
      return;
    }

    // Validate time range
    const timeValidation = validateTemplateTimeRange(editForm.startTime, editForm.endTime);
    if (!timeValidation.valid) {
      setEditValidationError(t("validation.startAfterEnd"));
      return;
    }

    // Validate headcount
    if (editForm.requiredHeadcount < 1 || editForm.requiredHeadcount > 999) {
      setEditValidationError(t("validation.headcountRange"));
      return;
    }

    setSaving(true);
    try {
      const payload: UpdateShiftTemplatePayload = {
        dayOfWeek: editForm.dayOfWeek as number,
        startTime: editForm.startTime,
        endTime: editForm.endTime,
        requiredHeadcount: editForm.requiredHeadcount,
        groupTaskId: editForm.groupTaskId || undefined,
      };
      await updateShiftTemplate(spaceId, groupId, editingTemplate.id, payload);
      closeEditModal();
      await fetchTemplates();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setEditError(message);
      await fetchTemplates(false);
    } finally {
      setSaving(false);
    }
  }

  // ── Delete handlers ──────────────────────────────────────────────────────
  function openDeleteConfirm(template: ShiftTemplateDto) {
    setDeleteTarget(template);
    setDeleteError(null);
  }

  function closeDeleteConfirm() {
    setDeleteTarget(null);
    setDeleteError(null);
  }

  async function handleDeleteConfirm() {
    if (!deleteTarget) return;
    setDeleting(true);
    setDeleteError(null);

    try {
      await deleteShiftTemplate(spaceId, groupId, deleteTarget.id);
      closeDeleteConfirm();
      await fetchTemplates();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setDeleteError(message);
      await fetchTemplates(false);
    } finally {
      setDeleting(false);
    }
  }

  // ── Loading state ────────────────────────────────────────────────────────
  if (loading) {
    return <LoadingCard rows={4} variant="list" />;
  }

  // ── Error state ──────────────────────────────────────────────────────────
  if (error) {
    return <ErrorRetry message={error} onRetry={() => fetchTemplates()} />;
  }

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="space-y-4">
      {/* Header with create button */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-700">{t("title")}</h3>
        <button
          onClick={openCreateForm}
          className="text-sm text-white bg-sky-600 hover:bg-sky-700 px-4 py-2 rounded-lg transition-colors"
        >
          {t("createButton")}
        </button>
      </div>

      {/* Empty state */}
      {templates.length === 0 && !showCreateForm && (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">{t("noTemplates")}</p>
        </div>
      )}

      {/* Create form */}
      {showCreateForm && (
        <div className="bg-white border border-sky-200 rounded-xl p-4 space-y-3">
          <TemplateForm
            form={createForm}
            onChange={setCreateForm}
            tasks={tasks}
            getDayName={getDayName}
          />

          {createValidationError && (
            <p className="text-xs text-red-600">{createValidationError}</p>
          )}
          {createError && (
            <p className="text-xs text-red-600">{createError}</p>
          )}

          <div className="flex gap-2 justify-end">
            <button
              onClick={closeCreateForm}
              disabled={creating}
              className="px-3 py-1.5 text-sm text-slate-600 hover:text-slate-800 border border-slate-200 rounded-lg transition-colors"
            >
              {t("form.cancel")}
            </button>
            <MutationButton
              onClick={handleCreate}
              loading={creating}
              disabled={createForm.dayOfWeek === "" || !createForm.startTime || !createForm.endTime || !createForm.groupTaskId}
              label={t("form.submit")}
              loadingLabel={t("creating")}
              variant="primary"
            />
          </div>
        </div>
      )}

      {/* Template list */}
      {templates.length > 0 && (
        <div className="space-y-2">
          {templates.map((template) => (
            <div
              key={template.id}
              className="flex items-center justify-between gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3"
            >
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-slate-900">
                  {getDayName(template.dayOfWeek)}
                </p>
                <p className="text-xs text-slate-500 mt-0.5">
                  {formatTime24h(template.startTime)} – {formatTime24h(template.endTime)} · {template.groupTaskName} · {template.requiredHeadcount} {t("form.requiredHeadcount")}
                </p>
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                <button
                  onClick={() => openEditModal(template)}
                  className="text-xs text-sky-600 hover:text-sky-700 border border-sky-200 bg-sky-50 hover:bg-sky-100 px-2.5 py-1 rounded-lg transition-colors"
                >
                  {t("editButton")}
                </button>
                <button
                  onClick={() => openDeleteConfirm(template)}
                  className="text-xs text-red-600 hover:text-red-700 border border-red-200 bg-red-50 hover:bg-red-100 px-2.5 py-1 rounded-lg transition-colors"
                >
                  {t("deleteButton")}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Edit modal */}
      <Modal
        open={!!editingTemplate}
        onClose={closeEditModal}
        title={t("editButton")}
      >
        <div className="space-y-4">
          <TemplateForm
            form={editForm}
            onChange={setEditForm}
            tasks={tasks}
            getDayName={getDayName}
          />

          {editValidationError && (
            <p className="text-xs text-red-600">{editValidationError}</p>
          )}
          {editError && (
            <p className="text-xs text-red-600">{editError}</p>
          )}

          <div className="flex gap-3 justify-end">
            <button
              onClick={closeEditModal}
              disabled={saving}
              className="px-4 py-2 text-sm text-slate-600 hover:text-slate-800 border border-slate-200 rounded-lg transition-colors"
            >
              {t("form.cancel")}
            </button>
            <MutationButton
              onClick={handleEditSave}
              loading={saving}
              disabled={editForm.dayOfWeek === "" || !editForm.startTime || !editForm.endTime}
              label={t("form.submit")}
              loadingLabel={t("saving")}
              variant="primary"
            />
          </div>
        </div>
      </Modal>

      {/* Delete confirmation modal */}
      <Modal
        open={!!deleteTarget}
        onClose={closeDeleteConfirm}
        title={t("deleteConfirmTitle")}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">{t("deleteConfirmMessage")}</p>

          {deleteError && (
            <p className="text-xs text-red-600">{deleteError}</p>
          )}

          <div className="flex gap-3 justify-end">
            <button
              onClick={closeDeleteConfirm}
              disabled={deleting}
              className="px-4 py-2 text-sm text-slate-600 hover:text-slate-800 border border-slate-200 rounded-lg transition-colors"
            >
              {t("deleteConfirmNo")}
            </button>
            <MutationButton
              onClick={handleDeleteConfirm}
              loading={deleting}
              label={t("deleteConfirmYes")}
              loadingLabel={t("deleting")}
              variant="danger"
            />
          </div>
        </div>
      </Modal>
    </div>
  );
}

// ── TemplateForm sub-component ─────────────────────────────────────────────

interface TemplateFormProps {
  form: TemplateFormState;
  onChange: (form: TemplateFormState) => void;
  tasks: GroupTaskDto[];
  getDayName: (day: number) => string;
}

function TemplateForm({ form, onChange, tasks, getDayName }: TemplateFormProps) {
  const t = useTranslations("selfService.templates");
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
      {/* Day of week */}
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">
          {t("form.dayOfWeek")}
        </label>
        <select
          value={form.dayOfWeek}
          onChange={(e) => onChange({ ...form, dayOfWeek: e.target.value === "" ? "" : Number(e.target.value) })}
          className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
          dir="rtl"
        >
          <option value="">{t("form.selectDay")}</option>
          {DAYS_OF_WEEK.map((day) => (
            <option key={day} value={day}>
              {getDayName(day)}
            </option>
          ))}
        </select>
      </div>

      {/* Task */}
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">
          {t("form.task")}
        </label>
        <select
          value={form.groupTaskId}
          onChange={(e) => onChange({ ...form, groupTaskId: e.target.value })}
          className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
          dir="rtl"
        >
          <option value="">{t("form.selectTask")}</option>
          {tasks.map((task) => (
            <option key={task.id} value={task.id}>
              {task.name}
            </option>
          ))}
        </select>
      </div>

      {/* Start time */}
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">
          {t("form.startTime")}
        </label>
        <input
          type="time"
          value={form.startTime}
          onChange={(e) => onChange({ ...form, startTime: e.target.value })}
          className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
        />
      </div>

      {/* End time */}
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">
          {t("form.endTime")}
        </label>
        <input
          type="time"
          value={form.endTime}
          onChange={(e) => onChange({ ...form, endTime: e.target.value })}
          className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
        />
      </div>

      {/* Required headcount */}
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">
          {t("form.requiredHeadcount")}
        </label>
        <input
          type="number"
          min={1}
          max={999}
          value={form.requiredHeadcount}
          onChange={(e) => onChange({ ...form, requiredHeadcount: Math.max(1, Math.min(999, Number(e.target.value) || 1)) })}
          className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
        />
      </div>
    </div>
  );
}

// (Spinner is now provided by MutationButton component)
