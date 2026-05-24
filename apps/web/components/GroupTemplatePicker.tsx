"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { GROUP_TEMPLATES, type GroupTemplate } from "@/lib/utils/groupTemplates";
import { createGroupTask } from "@/lib/api/tasks";
import { createConstraint } from "@/lib/api/constraints";
import { updateGroupSettings, createGroupQualification, updateGroup } from "@/lib/api/groups";
import { seedReasons } from "@/lib/api/unavailabilityReasons";

interface Props {
  spaceId: string;
  groupId: string;
  onComplete: () => void;
  onSkip: () => void;
}

export default function GroupTemplatePicker({ spaceId, groupId, onComplete, onSkip }: Props) {
  const t = useTranslations("groups.templates");
  const [selected, setSelected] = useState<string | null>(null);
  const [applying, setApplying] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleApply() {
    if (!selected) return;
    const template = GROUP_TEMPLATES.find(t => t.id === selected);
    if (!template || template.id === "custom") {
      // For custom template, still set the templateType
      await updateGroup(spaceId, groupId, { templateType: "Custom" });
      onComplete();
      return;
    }

    setApplying(true);
    setError(null);

    // Map template id to the API template type string
    const templateTypeMap: Record<string, string> = {
      "army-base": "Army",
      "restaurant": "Restaurant",
      "hospital": "Hospital",
      "security": "Security",
      "custom": "Custom",
    };
    const templateType = templateTypeMap[template.id] ?? "Custom";

    try {
      // Set the template type on the group
      await updateGroup(spaceId, groupId, { templateType });

      // Create tasks
      for (const task of template.tasks) {
        await createGroupTask(spaceId, groupId, task);
      }

      // Create constraints
      for (const constraint of template.constraints) {
        await createConstraint(
          spaceId,
          constraint.scopeType,
          groupId,
          constraint.severity,
          constraint.ruleType,
          constraint.rulePayloadJson,
          null,
          null
        );
      }

      // Update solver horizon
      await updateGroupSettings(spaceId, groupId, template.solverHorizonDays, null);

      // Create qualifications from template
      for (const qualification of template.qualifications) {
        try {
          await createGroupQualification(spaceId, groupId, qualification.name, qualification.description ?? null);
        } catch (err: unknown) {
          const error = err as { response?: { status?: number } };
          // Skip 409 (already exists) — continue with the rest
          if (error?.response?.status !== 409) throw err;
        }
      }

      // Seed unavailability reasons from template
      if (template.unavailabilityReasons.length > 0) {
        await seedReasons(spaceId, template.unavailabilityReasons);
      }

      onComplete();
    } catch {
      setError(t("errorApplying"));
    } finally {
      setApplying(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="text-center">
        <h2 className="text-lg font-bold text-slate-900 dark:text-white">{t("title")}</h2>
        <p className="text-sm text-slate-500 mt-1">{t("subtitle")}</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {GROUP_TEMPLATES.map(template => (
          <button
            key={template.id}
            onClick={() => setSelected(template.id)}
            className={`text-start p-4 rounded-xl border-2 transition-all ${
              selected === template.id
                ? "border-sky-500 bg-sky-50 dark:bg-sky-900/20 shadow-sm"
                : "border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600"
            }`}
          >
            <div className="flex items-start gap-3">
              <span
                className="w-10 h-10 rounded-xl flex items-center justify-center text-lg flex-shrink-0"
                style={{ background: `${template.color}15` }}
              >
                {template.icon}
              </span>
              <div className="min-w-0">
                <p className="text-sm font-semibold text-slate-900 dark:text-white">{template.name}</p>
                <p className="text-xs text-slate-500 mt-0.5 leading-relaxed">{template.description}</p>
                {template.tasks.length > 0 && (
                  <div className="flex flex-wrap gap-1 mt-2">
                    {template.tasks.map(task => (
                      <span key={task.name} className="text-[10px] bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 px-2 py-0.5 rounded-full">
                        {task.name}
                      </span>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </button>
        ))}
      </div>

      {error && <p className="text-sm text-red-600 text-center">{error}</p>}

      <div className="flex items-center justify-between pt-2">
        <button
          onClick={onSkip}
          className="text-sm text-slate-500 hover:text-slate-700 dark:text-slate-400"
        >
          {t("skip")}
        </button>
        <button
          onClick={handleApply}
          disabled={!selected || applying}
          className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-6 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
        >
          {applying ? t("applying") : t("apply")}
        </button>
      </div>
    </div>
  );
}
