"use client";

import { useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  useSandboxStore,
  type SettingsOverrides,
  type SolverInputDto,
  type TaskSlotDto,
  type QualificationRequirementSolverDto,
} from "@/lib/store/sandboxStore";

interface SandboxSettingsTabProps {
  settingsOverrides: SettingsOverrides;
  baseline: SolverInputDto | null;
}

/**
 * SandboxSettingsTab — Settings tab content for the sandbox settings panel.
 *
 * Provides controls for:
 * - Minimum rest hours between shifts (0–24)
 * - Home-leave parameters (only when baseline has home-leave enabled)
 * - Minimum people at base (only when group is a closed base)
 * - Qualification requirements editor for task slots
 *
 * All values are validated client-side before calling updateSettings.
 * Requirements: 5.1, 5.2, 5.3, 5.4, 5.5
 */
export default function SandboxSettingsTab({ settingsOverrides, baseline }: SandboxSettingsTabProps) {
  const t = useTranslations("sandbox.settings");
  const updateSettings = useSandboxStore((s) => s.updateSettings);

  const homeLeaveEnabled = baseline?.homeLeaveConfig?.enabled ?? false;
  // A group is a closed base if it has home-leave config (presence indicates closed base)
  const isClosedBase = homeLeaveEnabled;

  const overrideCount = Object.keys(settingsOverrides).filter(
    (k) => settingsOverrides[k as keyof SettingsOverrides] !== undefined
  ).length;

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
          {t("title")}
        </h3>
        {overrideCount > 0 && (
          <span className="text-xs text-amber-600 dark:text-amber-400">
            {overrideCount} {t("modified")}
          </span>
        )}
      </div>

      {/* Minimum rest hours */}
      <MinRestHoursSection
        value={settingsOverrides.minRestBetweenShiftsHours}
        baselineValue={getBaselineMinRestHours(baseline)}
        onChange={(val) => updateSettings({ minRestBetweenShiftsHours: val })}
        onClear={() => updateSettings({ minRestBetweenShiftsHours: undefined })}
      />

      {/* Home-leave parameters — only when enabled */}
      {homeLeaveEnabled && (
        <HomeLeaveSection
          settingsOverrides={settingsOverrides}
          baseline={baseline}
          onChange={updateSettings}
        />
      )}

      {/* Minimum people at base — only for closed base */}
      {isClosedBase && (
        <MinPeopleAtBaseSection
          value={settingsOverrides.minPeopleAtBase}
          baselineValue={baseline?.homeLeaveConfig?.leave_capacity}
          totalPeople={baseline?.people.length ?? 0}
          onChange={(val) => updateSettings({ minPeopleAtBase: val })}
          onClear={() => updateSettings({ minPeopleAtBase: undefined })}
        />
      )}

      {/* Qualification requirements editor */}
      <QualificationRequirementsSection baseline={baseline} />
    </div>
  );
}

// ── Helper: extract baseline min rest hours from hard constraints ──────────────

function getBaselineMinRestHours(baseline: SolverInputDto | null): number {
  if (!baseline) return 8;
  const constraint = baseline.hardConstraints.find(
    (c) => c.ruleType === "min_rest_between_assignments"
  );
  if (constraint?.payload?.min_hours != null) {
    return Number(constraint.payload.min_hours);
  }
  return 8;
}

// ── Section: Minimum Rest Hours ───────────────────────────────────────────────

function MinRestHoursSection({
  value,
  baselineValue,
  onChange,
  onClear,
}: {
  value: number | undefined;
  baselineValue: number;
  onChange: (val: number) => void;
  onClear: () => void;
}) {
  const t = useTranslations("sandbox.settings");
  const displayValue = value ?? baselineValue;
  const isOverridden = value !== undefined;

  const handleChange = useCallback(
    (newVal: number) => {
      // Clamp to valid range
      const clamped = Math.max(0, Math.min(24, Math.round(newVal)));
      onChange(clamped);
    },
    [onChange]
  );

  return (
    <div className="bg-slate-50 dark:bg-slate-800/50 rounded-xl p-4 space-y-3">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
          {t("minRestHours")}
        </label>
        {isOverridden && (
          <button
            onClick={onClear}
            className="text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
            title={t("resetToBaseline")}
          >
            ✕
          </button>
        )}
      </div>
      <p className="text-xs text-slate-400 dark:text-slate-500">
        {t("minRestHoursDesc")}
      </p>
      <div className="flex items-center gap-3">
        <input
          type="range"
          min={0}
          max={24}
          step={1}
          value={displayValue}
          onChange={(e) => handleChange(Number(e.target.value))}
          className="flex-1"
        />
        <input
          type="number"
          min={0}
          max={24}
          value={displayValue}
          onChange={(e) => handleChange(Number(e.target.value))}
          className={`w-16 border rounded-lg px-2 py-1.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-sky-500 ${
            isOverridden
              ? "border-amber-300 dark:border-amber-600 bg-amber-50 dark:bg-amber-900/20"
              : "border-slate-200 dark:border-slate-600"
          }`}
        />
        <span className="text-xs text-slate-500 dark:text-slate-400">{t("hours")}</span>
      </div>
      {displayValue === 0 && (
        <p className="text-xs text-amber-600 dark:text-amber-400">{t("minRestZeroWarning")}</p>
      )}
      {isOverridden && (
        <p className="text-xs text-slate-400 dark:text-slate-500">
          {t("baseline")}: {baselineValue}h
        </p>
      )}
    </div>
  );
}

// ── Section: Home-Leave Parameters ────────────────────────────────────────────

function HomeLeaveSection({
  settingsOverrides,
  baseline,
  onChange,
}: {
  settingsOverrides: SettingsOverrides;
  baseline: SolverInputDto | null;
  onChange: (settings: Partial<SettingsOverrides>) => void;
}) {
  const t = useTranslations("sandbox.settings");
  const config = baseline?.homeLeaveConfig;

  return (
    <div className="bg-slate-50 dark:bg-slate-800/50 rounded-xl p-4 space-y-4">
      <h4 className="text-sm font-medium text-slate-700 dark:text-slate-300">
        {t("homeLeaveTitle")}
      </h4>
      <p className="text-xs text-slate-400 dark:text-slate-500">
        {t("homeLeaveDesc")}
      </p>

      {/* Eligibility threshold hours */}
      <NumberField
        label={t("eligibilityThreshold")}
        hint={t("eligibilityThresholdHint")}
        value={settingsOverrides.eligibilityThresholdHours}
        baselineValue={config?.eligibility_threshold_hours}
        min={0}
        max={720}
        step={1}
        unit={t("hours")}
        onChange={(val) => onChange({ eligibilityThresholdHours: val })}
        onClear={() => onChange({ eligibilityThresholdHours: undefined })}
      />

      {/* Leave duration hours */}
      <NumberField
        label={t("leaveDuration")}
        hint={t("leaveDurationHint")}
        value={settingsOverrides.leaveDurationHours}
        baselineValue={config?.leave_duration_hours}
        min={1}
        max={720}
        step={1}
        unit={t("hours")}
        onChange={(val) => onChange({ leaveDurationHours: val })}
        onClear={() => onChange({ leaveDurationHours: undefined })}
      />

      {/* Leave capacity */}
      <NumberField
        label={t("leaveCapacity")}
        hint={t("leaveCapacityHint")}
        value={settingsOverrides.leaveCapacity}
        baselineValue={config?.leave_capacity}
        min={1}
        max={100}
        step={1}
        onChange={(val) => onChange({ leaveCapacity: val })}
        onClear={() => onChange({ leaveCapacity: undefined })}
      />

      {/* Balance value */}
      <NumberField
        label={t("balanceValue")}
        hint={t("balanceValueHint")}
        value={settingsOverrides.balanceValue}
        baselineValue={config?.balance_value ?? 50}
        min={0}
        max={100}
        step={1}
        onChange={(val) => onChange({ balanceValue: val })}
        onClear={() => onChange({ balanceValue: undefined })}
      />
    </div>
  );
}

// ── Section: Minimum People at Base ───────────────────────────────────────────

function MinPeopleAtBaseSection({
  value,
  baselineValue,
  totalPeople,
  onChange,
  onClear,
}: {
  value: number | undefined;
  baselineValue: number | undefined;
  totalPeople: number;
  onChange: (val: number) => void;
  onClear: () => void;
}) {
  const t = useTranslations("sandbox.settings");
  const displayValue = value ?? baselineValue ?? 1;
  const isOverridden = value !== undefined;
  const maxValue = Math.max(1, totalPeople - 1);

  const handleChange = useCallback(
    (newVal: number) => {
      const clamped = Math.max(1, Math.min(maxValue, Math.round(newVal)));
      onChange(clamped);
    },
    [onChange, maxValue]
  );

  return (
    <div className="bg-slate-50 dark:bg-slate-800/50 rounded-xl p-4 space-y-3">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
          {t("minPeopleAtBase")}
        </label>
        {isOverridden && (
          <button
            onClick={onClear}
            className="text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
            title={t("resetToBaseline")}
          >
            ✕
          </button>
        )}
      </div>
      <p className="text-xs text-slate-400 dark:text-slate-500">
        {t("minPeopleAtBaseDesc")}
      </p>
      <div className="flex items-center gap-3">
        <input
          type="number"
          min={1}
          max={maxValue}
          value={displayValue}
          onChange={(e) => handleChange(Number(e.target.value))}
          className={`w-20 border rounded-lg px-3 py-1.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-sky-500 ${
            isOverridden
              ? "border-amber-300 dark:border-amber-600 bg-amber-50 dark:bg-amber-900/20"
              : "border-slate-200 dark:border-slate-600"
          }`}
        />
        <span className="text-xs text-slate-500 dark:text-slate-400">
          / {totalPeople} {t("people")}
        </span>
      </div>
      {isOverridden && baselineValue !== undefined && (
        <p className="text-xs text-slate-400 dark:text-slate-500">
          {t("baseline")}: {baselineValue}
        </p>
      )}
    </div>
  );
}

// ── Section: Qualification Requirements Editor ────────────────────────────────

function QualificationRequirementsSection({ baseline }: { baseline: SolverInputDto | null }) {
  const t = useTranslations("sandbox.settings");
  const editTask = useSandboxStore((s) => s.editTask);
  const taskOverrides = useSandboxStore((s) => s.taskOverrides);

  // Get effective task slots (baseline + overrides)
  const effectiveSlots = getEffectiveTaskSlots(baseline, taskOverrides);

  // Only show tasks that have qualification requirements or can have them
  const slotsWithQualifications = effectiveSlots.filter(
    (slot) => slot.qualificationRequirements && slot.qualificationRequirements.length > 0
  );

  if (slotsWithQualifications.length === 0) {
    return (
      <div className="bg-slate-50 dark:bg-slate-800/50 rounded-xl p-4 space-y-3">
        <h4 className="text-sm font-medium text-slate-700 dark:text-slate-300">
          {t("qualificationsTitle")}
        </h4>
        <p className="text-xs text-slate-400 dark:text-slate-500">
          {t("noQualifications")}
        </p>
      </div>
    );
  }

  return (
    <div className="bg-slate-50 dark:bg-slate-800/50 rounded-xl p-4 space-y-4">
      <h4 className="text-sm font-medium text-slate-700 dark:text-slate-300">
        {t("qualificationsTitle")}
      </h4>
      <p className="text-xs text-slate-400 dark:text-slate-500">
        {t("qualificationsDesc")}
      </p>

      <div className="space-y-3">
        {slotsWithQualifications.map((slot) => (
          <QualificationSlotEditor
            key={slot.slotId}
            slot={slot}
            onUpdate={(reqs) => {
              editTask(slot.slotId, { qualificationRequirements: reqs });
            }}
          />
        ))}
      </div>
    </div>
  );
}

function QualificationSlotEditor({
  slot,
  onUpdate,
}: {
  slot: TaskSlotDto;
  onUpdate: (reqs: QualificationRequirementSolverDto[]) => void;
}) {
  const t = useTranslations("sandbox.settings");
  const [expanded, setExpanded] = useState(false);
  const requirements = slot.qualificationRequirements ?? [];

  const handleCountChange = (index: number, count: number) => {
    const clamped = Math.max(1, Math.min(slot.requiredHeadcount, count));
    const updated = [...requirements];
    updated[index] = { ...updated[index], count: clamped };
    onUpdate(updated);
  };

  const handleMandatoryToggle = (index: number) => {
    const updated = [...requirements];
    updated[index] = { ...updated[index], mandatory: !updated[index].mandatory };
    onUpdate(updated);
  };

  return (
    <div className="border border-slate-200 dark:border-slate-600 rounded-lg overflow-hidden">
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between px-3 py-2 text-xs font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700/50 transition-colors"
      >
        <span className="truncate">{slot.taskTypeName}</span>
        <span className="flex items-center gap-1.5 text-slate-400">
          <span>{requirements.length} {t("qualifications")}</span>
          <svg
            className={`w-3 h-3 transition-transform ${expanded ? "rotate-180" : ""}`}
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
          </svg>
        </span>
      </button>

      {expanded && (
        <div className="px-3 pb-3 space-y-2 border-t border-slate-100 dark:border-slate-700">
          {requirements.map((req, idx) => (
            <div key={`${req.qualification_name}-${idx}`} className="flex items-center gap-2 pt-2">
              <span className="text-xs text-slate-600 dark:text-slate-400 flex-1 truncate">
                {req.qualification_name}
              </span>
              <input
                type="number"
                min={1}
                max={slot.requiredHeadcount}
                value={req.count}
                onChange={(e) => handleCountChange(idx, Number(e.target.value))}
                className="w-12 border border-slate-200 dark:border-slate-600 rounded px-1.5 py-1 text-xs text-center focus:outline-none focus:ring-1 focus:ring-sky-500"
              />
              <button
                onClick={() => handleMandatoryToggle(idx)}
                className={`text-xs px-2 py-1 rounded transition-colors ${
                  req.mandatory
                    ? "bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400"
                    : "bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400"
                }`}
                title={req.mandatory ? t("mandatory") : t("optional")}
              >
                {req.mandatory ? t("mandatory") : t("optional")}
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Reusable NumberField Component ────────────────────────────────────────────

function NumberField({
  label,
  hint,
  value,
  baselineValue,
  min,
  max,
  step,
  unit,
  onChange,
  onClear,
}: {
  label: string;
  hint?: string;
  value: number | undefined;
  baselineValue: number | undefined;
  min: number;
  max: number;
  step: number;
  unit?: string;
  onChange: (val: number) => void;
  onClear: () => void;
}) {
  const t = useTranslations("sandbox.settings");
  const displayValue = value ?? baselineValue ?? min;
  const isOverridden = value !== undefined;

  const handleChange = useCallback(
    (newVal: number) => {
      const clamped = Math.max(min, Math.min(max, newVal));
      onChange(clamped);
    },
    [onChange, min, max]
  );

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <label className="text-xs font-medium text-slate-600 dark:text-slate-400">
          {label}
        </label>
        {isOverridden && (
          <button
            onClick={onClear}
            className="text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
            title={t("resetToBaseline")}
          >
            ✕
          </button>
        )}
      </div>
      {hint && (
        <p className="text-xs text-slate-400 dark:text-slate-500">{hint}</p>
      )}
      <div className="flex items-center gap-2">
        <input
          type="number"
          min={min}
          max={max}
          step={step}
          value={displayValue}
          placeholder={baselineValue?.toString()}
          onChange={(e) => handleChange(Number(e.target.value))}
          className={`w-20 border rounded-lg px-2.5 py-1.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-sky-500 ${
            isOverridden
              ? "border-amber-300 dark:border-amber-600 bg-amber-50 dark:bg-amber-900/20"
              : "border-slate-200 dark:border-slate-600"
          }`}
        />
        {unit && (
          <span className="text-xs text-slate-500 dark:text-slate-400">{unit}</span>
        )}
      </div>
      {isOverridden && baselineValue !== undefined && (
        <p className="text-xs text-slate-400 dark:text-slate-500">
          {t("baseline")}: {baselineValue}{unit ? ` ${unit}` : ""}
        </p>
      )}
    </div>
  );
}

// ── Helper: Get effective task slots (baseline merged with overrides) ─────────

function getEffectiveTaskSlots(
  baseline: SolverInputDto | null,
  taskOverrides: Map<string, { action: string; original?: TaskSlotDto; modified?: Partial<TaskSlotDto> }>
): TaskSlotDto[] {
  if (!baseline) return [];

  const slots: TaskSlotDto[] = [];

  // Start with baseline slots, applying edits and filtering removals
  for (const slot of baseline.taskSlots) {
    const override = taskOverrides.get(slot.slotId);
    if (override?.action === "remove") continue;
    if (override?.action === "edit" && override.modified) {
      slots.push({ ...slot, ...override.modified } as TaskSlotDto);
    } else {
      slots.push(slot);
    }
  }

  // Add new slots from overrides
  for (const [, override] of taskOverrides) {
    if (override.action === "add" && override.modified) {
      slots.push(override.modified as TaskSlotDto);
    }
  }

  return slots;
}
