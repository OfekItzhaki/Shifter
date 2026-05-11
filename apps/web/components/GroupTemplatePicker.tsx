"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { GROUP_TEMPLATES, type GroupTemplate } from "@/lib/utils/groupTemplates";
import { createGroupTask } from "@/lib/api/tasks";
import { createConstraint } from "@/lib/api/constraints";
import { updateGroupSettings } from "@/lib/api/groups";

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
      onComplete();
      return;
    }

    setApplying(true);
    setError(null);

    try {
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
                ? "border-blue-500 bg-blue-50 dark:bg-blue-900/20 shadow-sm"
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
          className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-6 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
        >
          {applying ? t("applying") : t("apply")}
        </button>
      </div>
    </div>
  );
}
