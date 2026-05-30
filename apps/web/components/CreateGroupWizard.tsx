"use client";

import { useState, useRef, useEffect } from "react";
import { useTranslations } from "next-intl";
import Modal from "@/components/Modal";
import { GROUP_TEMPLATES } from "@/lib/utils/groupTemplates";
import SchedulingModeSelector, { type SchedulingMode } from "@/components/groups/selfService/SchedulingModeSelector";
import ModeWarningDialog from "@/components/groups/selfService/ModeWarningDialog";

/** Self-service-specific template options for shift patterns */
const SELF_SERVICE_TEMPLATES = [
  {
    id: "weekly-shifts",
    icon: "📅",
    color: "#8b5cf6",
  },
  {
    id: "flexible",
    icon: "🔄",
    color: "#06b6d4",
  },
  {
    id: "custom",
    icon: "✏️",
    color: "#64748b",
  },
];

interface Props {
  open: boolean;
  onClose: () => void;
  onCreateGroup: (name: string, templateId: string, schedulingMode?: SchedulingMode) => Promise<void>;
  isPending: boolean;
}

type WizardStep = "name" | "scheduling-mode" | "template";

export default function CreateGroupWizard({ open, onClose, onCreateGroup, isPending }: Props) {
  const t = useTranslations("groups.createWizard");
  const tMode = useTranslations("selfService.modeSelector");
  const [step, setStep] = useState<WizardStep>("name");
  const [name, setName] = useState("");
  const [schedulingMode, setSchedulingMode] = useState<SchedulingMode | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<string>("custom");
  const [selectedSelfServiceTemplate, setSelectedSelfServiceTemplate] = useState<string>("weekly-shifts");
  const [error, setError] = useState<string | null>(null);
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Auto-focus name input when modal opens
  useEffect(() => {
    if (open) {
      setTimeout(() => inputRef.current?.focus(), 100);
    } else {
      // Reset state when closed
      setStep("name");
      setName("");
      setSchedulingMode(null);
      setSelectedTemplate("custom");
      setSelectedSelfServiceTemplate("weekly-shifts");
      setError(null);
      setShowConfirmDialog(false);
    }
  }, [open]);

  function handleNameContinue() {
    if (!name.trim()) return;
    setStep("scheduling-mode");
  }

  function handleModeContinue() {
    if (!schedulingMode) return;
    setStep("template");
  }

  function handleBack() {
    if (step === "template") {
      setStep("scheduling-mode");
    } else if (step === "scheduling-mode") {
      setStep("name");
    }
  }

  async function handleSubmit() {
    if (!name.trim() || !schedulingMode) return;
    setShowConfirmDialog(true);
  }

  async function handleConfirmSubmit() {
    if (!name.trim()) return;
    setError(null);
    try {
      const templateId = schedulingMode === "SelfService" ? selectedSelfServiceTemplate : selectedTemplate;
      await onCreateGroup(name.trim(), templateId, schedulingMode ?? undefined);
      setShowConfirmDialog(false);
    } catch (err: unknown) {
      setShowConfirmDialog(false);
      const errorMsg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        || t("createError");
      setError(errorMsg);
    }
  }

  function getTitle(): string {
    switch (step) {
      case "name":
        return t("title");
      case "scheduling-mode":
        return tMode("title");
      case "template":
        return t("templateLabel");
      default:
        return t("title");
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={getTitle()} maxWidth={560}>
      <div className="space-y-5">
        {/* Step 1: Group name input */}
        {step === "name" && (
          <>
            <div>
              <label className="block text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wide mb-1.5">
                {t("nameLabel")} *
              </label>
              <input
                ref={inputRef}
                type="text"
                value={name}
                onChange={e => setName(e.target.value)}
                placeholder={t("namePlaceholder")}
                className="w-full border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500 placeholder:text-slate-400"
                onKeyDown={e => { if (e.key === "Enter" && name.trim()) handleNameContinue(); }}
              />
            </div>

            <button
              type="button"
              onClick={handleNameContinue}
              disabled={!name.trim()}
              className="w-full bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
            >
              {t("continue")}
            </button>
          </>
        )}

        {/* Step 2: Scheduling mode selection */}
        {step === "scheduling-mode" && (
          <>
            <SchedulingModeSelector
              selectedMode={schedulingMode}
              onSelect={setSchedulingMode}
            />

            <div className="flex gap-3">
              <button
                type="button"
                onClick={handleBack}
                className="flex-1 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 text-sm font-medium px-4 py-2.5 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors"
              >
                חזרה
              </button>
              <button
                type="button"
                onClick={handleModeContinue}
                disabled={!schedulingMode}
                className="flex-1 bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
              >
                {t("continue")}
              </button>
            </div>
          </>
        )}

        {/* Step 3: Template selection (mode-conditional) */}
        {step === "template" && (
          <>
            {schedulingMode === "AutoGenerated" && (
              <div>
                <label className="block text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wide mb-1.5">
                  {t("templateLabel")}
                </label>
                <p className="text-xs text-slate-400 dark:text-slate-500 mb-3">{t("templateDescription")}</p>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                  {GROUP_TEMPLATES.map(template => (
                    <button
                      key={template.id}
                      type="button"
                      onClick={() => setSelectedTemplate(template.id)}
                      className={`text-start p-3.5 rounded-xl border-2 transition-all ${
                        selectedTemplate === template.id
                          ? "border-sky-500 bg-sky-50 dark:bg-sky-900/20 shadow-sm"
                          : "border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600"
                      }`}
                    >
                      <div className="flex items-start gap-3">
                        <span
                          className="w-9 h-9 rounded-lg flex items-center justify-center text-base flex-shrink-0"
                          style={{ background: `${template.color}15` }}
                        >
                          {template.icon}
                        </span>
                        <div className="min-w-0 flex-1">
                          <p className="text-sm font-semibold text-slate-900 dark:text-white">{template.nameHe}</p>
                          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5 leading-relaxed line-clamp-2">
                            {t(`templateCards.${template.id}`)}
                          </p>
                        </div>
                      </div>
                    </button>
                  ))}
                </div>
              </div>
            )}

            {schedulingMode === "SelfService" && (
              <div>
                <label className="block text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wide mb-1.5">
                  {t("templateLabel")}
                </label>
                <p className="text-xs text-slate-400 dark:text-slate-500 mb-3">
                  {t("selfServiceTemplateDescription")}
                </p>

                <div className="grid grid-cols-1 gap-2.5">
                  {SELF_SERVICE_TEMPLATES.map(template => (
                    <button
                      key={template.id}
                      type="button"
                      onClick={() => setSelectedSelfServiceTemplate(template.id)}
                      className={`text-start p-3.5 rounded-xl border-2 transition-all ${
                        selectedSelfServiceTemplate === template.id
                          ? "border-purple-500 bg-purple-50 dark:bg-purple-900/20 shadow-sm"
                          : "border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600"
                      }`}
                    >
                      <div className="flex items-start gap-3">
                        <span
                          className="w-9 h-9 rounded-lg flex items-center justify-center text-base flex-shrink-0"
                          style={{ background: `${template.color}15` }}
                        >
                          {template.icon}
                        </span>
                        <div className="min-w-0 flex-1">
                          <p className="text-sm font-semibold text-slate-900 dark:text-white">
                            {t(`selfServiceTemplates.${template.id}.name`)}
                          </p>
                          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5 leading-relaxed">
                            {t(`selfServiceTemplates.${template.id}.description`)}
                          </p>
                        </div>
                      </div>
                    </button>
                  ))}
                </div>
              </div>
            )}

            {error && <p className="text-sm text-red-600">{error}</p>}

            <div className="flex gap-3">
              <button
                type="button"
                onClick={handleBack}
                className="flex-1 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 text-sm font-medium px-4 py-2.5 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors"
              >
                חזרה
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={isPending}
                className="flex-1 bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
              >
                {isPending ? t("creating") : t("continue")}
              </button>
            </div>
          </>
        )}
      </div>

      {/* Mode confirmation dialog */}
      {schedulingMode && (
        <ModeWarningDialog
          open={showConfirmDialog}
          selectedMode={schedulingMode}
          onConfirm={handleConfirmSubmit}
          onCancel={() => setShowConfirmDialog(false)}
          isPending={isPending}
        />
      )}
    </Modal>
  );
}
