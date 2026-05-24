"use client";

import { useState, useRef, useEffect } from "react";
import { useTranslations } from "next-intl";
import Modal from "@/components/Modal";
import { GROUP_TEMPLATES } from "@/lib/utils/groupTemplates";

interface Props {
  open: boolean;
  onClose: () => void;
  onCreateGroup: (name: string, templateId: string) => Promise<void>;
  isPending: boolean;
}

export default function CreateGroupWizard({ open, onClose, onCreateGroup, isPending }: Props) {
  const t = useTranslations("groups.createWizard");
  const [name, setName] = useState("");
  const [selectedTemplate, setSelectedTemplate] = useState<string>("custom");
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Auto-focus name input when modal opens
  useEffect(() => {
    if (open) {
      setTimeout(() => inputRef.current?.focus(), 100);
    } else {
      // Reset state when closed
      setName("");
      setSelectedTemplate("custom");
      setError(null);
    }
  }, [open]);

  async function handleSubmit() {
    if (!name.trim()) return;
    setError(null);
    try {
      await onCreateGroup(name.trim(), selectedTemplate);
    } catch {
      setError(t("creating"));
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={t("title")} maxWidth={560}>
      <div className="space-y-5">
        {/* Group name input */}
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
            onKeyDown={e => { if (e.key === "Enter" && name.trim() && selectedTemplate) handleSubmit(); }}
          />
        </div>

        {/* Template selection */}
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

        {error && <p className="text-sm text-red-600">{error}</p>}

        {/* Continue button */}
        <button
          type="button"
          onClick={handleSubmit}
          disabled={!name.trim() || isPending}
          className="w-full bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
        >
          {isPending ? t("creating") : t("continue")}
        </button>
      </div>
    </Modal>
  );
}
