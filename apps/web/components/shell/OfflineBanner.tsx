"use client";

import { useServiceWorker } from "@/lib/hooks/useServiceWorker";

/**
 * Shows contextual banners:
 * - Offline: amber bar at top
 * - Update available: floating toast at bottom-right
 */
export default function OfflineBanner() {
  const { isOffline, updateAvailable, update } = useServiceWorker();

  if (!isOffline && !updateAvailable) return null;

  return (
    <>
      {isOffline && (
        <div className="fixed top-0 left-0 right-0 z-[100] bg-amber-500 text-white text-center py-1.5 px-4 text-xs font-medium flex items-center justify-center gap-2">
          <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M18.364 5.636a9 9 0 010 12.728M5.636 18.364a9 9 0 010-12.728" />
          </svg>
          <span>אין חיבור — מציג נתונים שמורים</span>
        </div>
      )}
      {updateAvailable && !isOffline && (
        <div className="fixed bottom-6 right-6 z-[100] animate-in slide-in-from-bottom-4 fade-in duration-300">
          <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-700 px-5 py-4 flex items-center gap-4 max-w-sm">
            <div className="w-10 h-10 rounded-xl bg-sky-50 dark:bg-sky-900/30 flex items-center justify-center flex-shrink-0">
              <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="#0ea5e9" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
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
