"use client";

import { useServiceWorker } from "@/lib/hooks/useServiceWorker";

/**
 * Shows a small banner at the top of the screen when the user is offline.
 * Also shows an update prompt when a new version is available.
 */
export default function OfflineBanner() {
  const { isOffline, updateAvailable, update } = useServiceWorker();

  if (!isOffline && !updateAvailable) return null;

  return (
    <div className="fixed top-0 left-0 right-0 z-[100]">
      {isOffline && (
        <div className="bg-amber-500 text-white text-center py-1.5 px-4 text-xs font-medium flex items-center justify-center gap-2">
          <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M18.364 5.636a9 9 0 010 12.728M5.636 18.364a9 9 0 010-12.728" />
          </svg>
          <span>אין חיבור — מציג נתונים שמורים</span>
        </div>
      )}
      {updateAvailable && !isOffline && (
        <div className="bg-blue-500 text-white text-center py-1.5 px-4 text-xs font-medium flex items-center justify-center gap-2">
          <span>גרסה חדשה זמינה</span>
          <button
            onClick={update}
            className="underline font-bold"
          >
            עדכן עכשיו
          </button>
        </div>
      )}
    </div>
  );
}
