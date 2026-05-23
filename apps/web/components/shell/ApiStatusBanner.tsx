"use client";

import { useState, useEffect } from "react";

/**
 * Shows a non-intrusive banner when the API is returning errors.
 * Only shows once per session — doesn't flash on every page navigation.
 * Auto-dismisses after 8 seconds or when the API recovers.
 */
export default function ApiStatusBanner() {
  const [visible, setVisible] = useState(false);
  const [isOffline, setIsOffline] = useState(false);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    let timeout: NodeJS.Timeout;
    let errorCount = 0;

    function handleApiError() {
      errorCount++;
      // Only show after 2+ errors to avoid flashing on single transient failures
      if (errorCount >= 2 && !dismissed) {
        setVisible(true);
        clearTimeout(timeout);
        timeout = setTimeout(() => setVisible(false), 8000);
      }
    }

    function handleApiOnline() {
      errorCount = 0;
      setVisible(false);
    }

    function handleOnline() {
      setIsOffline(false);
      setVisible(false);
      errorCount = 0;
    }

    function handleOffline() {
      setIsOffline(true);
      if (!dismissed) setVisible(true);
    }

    window.addEventListener("api-error", handleApiError);
    window.addEventListener("api-online", handleApiOnline);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    if (!navigator.onLine) {
      setIsOffline(true);
      setVisible(true);
    }

    return () => {
      window.removeEventListener("api-error", handleApiError);
      window.removeEventListener("api-online", handleApiOnline);
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
      clearTimeout(timeout);
    };
  }, [dismissed]);

  if (!visible) return null;

  return (
    <div
      className="w-full rounded-lg mb-4 flex items-center justify-between px-4 py-2.5"
      style={{
        background: isOffline ? "#1e293b" : "#fef3c7",
        border: isOffline ? "1px solid #fbbf24" : "1px solid #f59e0b",
      }}
    >
      <span
        className="text-xs font-medium"
        style={{ color: isOffline ? "#fbbf24" : "#92400e" }}
      >
        {isOffline
          ? "📡 No internet connection"
          : "⚠ Server temporarily unavailable — try refreshing in a moment"}
      </span>
      <button
        onClick={() => { setVisible(false); setDismissed(true); }}
        className="flex-shrink-0 ml-3 rounded-md p-1 hover:bg-black/10 transition-colors"
        style={{ background: "none", border: "none", cursor: "pointer", color: isOffline ? "#fbbf24" : "#92400e", lineHeight: 1 }}
        aria-label="Dismiss"
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>
  );
}
