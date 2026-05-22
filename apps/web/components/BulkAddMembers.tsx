"use client";

import { useState, useCallback } from "react";
import { useTranslations } from "next-intl";

interface Props {
  onBulkAdd: (names: string[], onProgress: (current: number, total: number) => void) => Promise<{ success: number; errors: number }>;
}

export default function BulkAddMembers({ onBulkAdd }: Props) {
  const t = useTranslations("groups.members_tab");
  const [text, setText] = useState("");
  const [adding, setAdding] = useState(false);
  const [progress, setProgress] = useState<{ current: number; total: number } | null>(null);
  const [result, setResult] = useState<{ success: number; errors: number } | null>(null);

  const names = text
    .split("\n")
    .map(line => line.trim())
    .filter(line => line.length > 0);

  const handleProgress = useCallback((current: number, total: number) => {
    setProgress({ current, total });
  }, []);

  async function handleBulkAdd() {
    if (names.length === 0) return;
    setAdding(true);
    setResult(null);
    setProgress({ current: 0, total: names.length });

    try {
      const res = await onBulkAdd(names, handleProgress);
      setResult(res);
      if (res.errors === 0) {
        setText("");
      }
    } finally {
      setAdding(false);
      setProgress(null);
    }
  }

  return (
    <div className="space-y-4">
      <div>
        <textarea
          value={text}
          onChange={e => { setText(e.target.value); setResult(null); }}
          placeholder={t("bulkPlaceholder")}
          rows={6}
          disabled={adding}
          className="w-full border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder:text-slate-400 resize-none"
        />
      </div>

      {names.length > 0 && !adding && !result && (
        <p className="text-xs text-slate-500 dark:text-slate-400">
          {t("bulkCount", { count: names.length })}
        </p>
      )}

      {progress && adding && (
        <div className="space-y-2">
          <p className="text-xs text-blue-600 dark:text-blue-400 font-medium">
            {t("bulkProgress", { current: progress.current, total: progress.total })}
          </p>
          <div className="w-full h-1.5 bg-slate-100 dark:bg-slate-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-blue-500 rounded-full transition-all duration-300"
              style={{ width: `${(progress.current / progress.total) * 100}%` }}
            />
          </div>
        </div>
      )}

      {result && (
        <div className="flex items-center gap-2 flex-wrap">
          {result.success > 0 && (
            <span className="text-xs text-green-600 dark:text-green-400 font-medium">
              ✓ {t("bulkSuccess", { count: result.success })}
            </span>
          )}
          {result.errors > 0 && (
            <span className="text-xs text-red-600 dark:text-red-400 font-medium">
              ✕ {t("bulkErrors", { count: result.errors })}
            </span>
          )}
        </div>
      )}

      <button
        type="button"
        onClick={handleBulkAdd}
        disabled={names.length === 0 || adding}
        className="w-full bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
      >
        {adding
          ? t("bulkProgress", { current: progress?.current ?? 0, total: progress?.total ?? 0 })
          : t("bulkAddAll")
        }
      </button>
    </div>
  );
}
