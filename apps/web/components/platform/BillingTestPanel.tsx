"use client";

import { useState } from "react";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";

export default function BillingTestPanel() {
  const { currentSpaceId } = useSpaceStore();
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleTestCharge() {
    if (!currentSpaceId) {
      setError("No space selected");
      return;
    }
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const { data } = await apiClient.post(`/spaces/${currentSpaceId}/billing/test-charge`);
      if (data.checkoutUrl) {
        setResult(data.checkoutUrl);
        window.open(data.checkoutUrl, "_blank", "noopener,noreferrer");
      } else {
        setError("No checkout URL received");
      }
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string } }; message?: string };
      setError(axiosErr?.response?.data?.error ?? axiosErr?.message ?? "Error creating test charge");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <h2 className="text-sm font-bold text-slate-900 dark:text-white mb-1 flex items-center gap-2">
        <span>🧪</span> Test Charge
      </h2>
      <p className="text-xs text-slate-500 dark:text-slate-400 mb-4">
        Creates a checkout session with the test product. Use card 4242 4242 4242 4242.
        Marked as test-charge — won't affect subscriptions.
      </p>

      <div className="flex items-center gap-3">
        <button
          onClick={handleTestCharge}
          disabled={loading}
          className="bg-violet-600 hover:bg-violet-700 text-white rounded-lg px-4 py-2 text-xs font-semibold disabled:opacity-60 cursor-pointer border-none transition-colors"
        >
          {loading ? "Creating..." : "Run Test Charge"}
        </button>

        {!currentSpaceId && (
          <span className="text-xs text-amber-500">⚠️ Select a space first</span>
        )}
      </div>

      {error && (
        <p className="text-xs text-red-600 dark:text-red-400 mt-3">{error}</p>
      )}

      {result && (
        <div className="mt-3 px-3 py-2 bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800 rounded-lg">
          <p className="text-xs text-emerald-700 dark:text-emerald-400">
            ✅ Checkout URL created — opened in new tab
          </p>
          <a
            href={result}
            target="_blank"
            rel="noopener noreferrer"
            className="text-[10px] text-sky-600 dark:text-sky-400 break-all"
          >
            {result}
          </a>
        </div>
      )}
    </div>
  );
}
