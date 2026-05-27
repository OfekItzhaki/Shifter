"use client";

import { useEffect, useState, useRef } from "react";
import { useServiceWorker } from "@/lib/hooks/useServiceWorker";
import { useConnectivityStore, ConnectivityStatus } from "@/lib/store/connectivityStore";
import { queryClient } from "@/lib/query/queryClient";

/**
 * Shows contextual banners:
 * - Offline: amber inline bar — "אתה לא מחובר לאינטרנט"
 * - Server unavailable: amber inline bar — "השרת לא זמין כרגע"
 * - Update available: floating toast at bottom-right (unchanged, from useServiceWorker)
 *
 * The banner is INLINE (not fixed/overlay) — it pushes content down with a smooth
 * slide animation. When connectivity returns, it slides out and triggers a silent refresh.
 */
export default function OfflineBanner() {
  const { updateAvailable, update } = useServiceWorker();
  const status = useConnectivityStore((s) => s.status);

  const [showBanner, setShowBanner] = useState(false);
  const [bannerText, setBannerText] = useState("");
  const prevStatusRef = useRef<ConnectivityStatus>(status);

  useEffect(() => {
    if (status !== "online") {
      // Show banner with appropriate text
      setBannerText(
        status === "offline"
          ? "אתה לא מחובר לאינטרנט"
          : "השרת לא זמין כרגע"
      );
      setShowBanner(true);
    } else if (prevStatusRef.current !== "online") {
      // Transitioning back to online — hide banner and refresh data
      setShowBanner(false);
      // Silently refetch all active queries so the UI updates with fresh data
      queryClient.refetchQueries({ type: "active" });
    }

    prevStatusRef.current = status;
  }, [status]);

  return (
    <>
      {/* Inline connectivity banner — slides in/out, pushes content down */}
      <div
        className="overflow-hidden transition-all duration-300 ease-in-out"
        style={{ maxHeight: showBanner ? "40px" : "0px" }}
      >
        <div
          role="status"
          className="bg-amber-500 text-white text-center py-2 px-4 text-xs font-medium flex items-center justify-center gap-2"
        >
          <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <span>{bannerText}</span>
        </div>
      </div>

      {/* Update available toast — floating, unchanged */}
      {updateAvailable && !showBanner && (
        <div className="fixed bottom-6 right-6 z-[100] animate-in slide-in-from-bottom-4 fade-in duration-300">
          <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-700 px-5 py-4 flex items-center gap-4 max-w-sm">
            <div className="w-10 h-10 rounded-xl bg-sky-50 dark:bg-sky-900/30 flex items-center justify-center flex-shrink-0">
              <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="#0ea5e9" strokeWidth={2}>
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
                />
              </svg>
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-semibold text-slate-900 dark:text-white">גרסה חדשה זמינה</p>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">לחץ לעדכון לגרסה האחרונה</p>
            </div>
            <button
              onClick={update}
              className="flex-shrink-0 bg-sky-500 hover:bg-sky-600 text-white text-xs font-semibold px-4 py-2 rounded-xl transition-colors"
            >
              עדכן
            </button>
          </div>
        </div>
      )}
    </>
  );
}
