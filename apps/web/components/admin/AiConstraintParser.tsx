"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";

interface ParsedConstraint {
  parsed: boolean;
  ruleType: string | null;
  scopeType: string | null;
  scopeHint: string | null;
  rulePayloadJson: string | null;
  confidenceNote: string | null;
  rawInput: string;
}

interface AiConstraintParserProps {
  onConfirm: (constraint: ParsedConstraint) => void;
}

export default function AiConstraintParser({ onConfirm }: AiConstraintParserProps) {
  const t = useTranslations("aiParser");
  const { currentSpaceId } = useSpaceStore();
  const [input, setInput] = useState("");
  const [result, setResult] = useState<ParsedConstraint | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleParse() {
    if (!currentSpaceId || !input.trim()) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const { data } = await apiClient.post(
        `/spaces/${currentSpaceId}/ai/parse-constraint`,
        { input }
      );
      setResult(data);
    } catch {
      setError(t("parseError"));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="space-y-4 rounded-lg border border-sky-200 bg-sky-50 p-4">
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold text-sky-700 uppercase tracking-wide">
          {t("title")}
        </span>
        <span className="text-xs text-sky-500">
          {t("subtitle")}
        </span>
      </div>

      <div className="flex gap-2">
        <input
          type="text"
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => e.key === "Enter" && handleParse()}
          placeholder={t("placeholder")}
          className="flex-1 border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400"
        />
        <button
          onClick={handleParse}
          disabled={loading || !input.trim()}
          className="bg-sky-600 text-white text-sm px-4 py-2 rounded-lg hover:bg-sky-700 disabled:opacity-50"
        >
          {loading ? "..." : t("parse")}
        </button>
      </div>

      {error && <p className="text-sm text-red-600">{error}</p>}

      {result && (
        <div className="space-y-3 rounded-lg border border-sky-300 bg-white p-3">
          {result.parsed ? (
            <>
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div>
                  <span className="text-gray-500 text-xs">{t("ruleType")}</span>
                  <p className="font-mono font-medium">{result.ruleType}</p>
                </div>
                <div>
                  <span className="text-gray-500 text-xs">{t("scope")}</span>
                  <p className="font-medium">{result.scopeType} — {result.scopeHint}</p>
                </div>
              </div>

              {result.rulePayloadJson && (
                <div>
                  <span className="text-gray-500 text-xs">Payload</span>
                  <pre className="text-xs bg-gray-50 rounded p-2 mt-1 overflow-x-auto">
                    {result.rulePayloadJson}
                  </pre>
                </div>
              )}

              {result.confidenceNote && (
                <p className="text-xs text-amber-600 italic">{result.confidenceNote}</p>
              )}

              <div className="flex gap-2 pt-1">
                <button
                  onClick={() => onConfirm(result)}
                  className="bg-green-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-green-700"
                >
                  {t("confirm")}
                </button>
                <button
                  onClick={() => setResult(null)}
                  className="text-xs text-gray-500 hover:underline"
                >
                  {t("cancel")}
                </button>
              </div>
            </>
          ) : (
            <div className="space-y-1">
              <p className="text-sm text-amber-700">{t("cannotParse")}</p>
              {result.confidenceNote && (
                <p className="text-xs text-gray-500">{result.confidenceNote}</p>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
