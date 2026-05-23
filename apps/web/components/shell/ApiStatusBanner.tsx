"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";

/**
 * Shows a subtle banner when the API is returning errors.
 * Auto-dismisses after 10 seconds or when the API recovers.
 */
export default function ApiStatusBanner() {
  const [visible, setVisible] = useState(false);
  const [isOffline, setIsOffline] = useState(false);

  useEffect(() => {
    let timeout: NodeJS.Timeout;

    function handleApiError() {
      setVisible(true);
      // Auto-hide after 10 seconds
      clearTimeout(timeout);
      timeout = setTimeout(() => setVisible(false), 10000);
    }

    function handleOnline() {
      setIsOffline(false);
      setVisible(false);
    }

    function handleOffline() {
      setIsOffline(true);
      setVisible(true);
    }

    window.addEventListener("api-error", handleApiError);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    // Check initial state
    if (!navigator.onLine) {
      setIsOffline(true);
      setVisible(true);
    }

    return () => {
      window.removeEventListener("api-error", handleApiError);
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
      clearTimeout(timeout);
    };
  }, []);

  if (!visible) return null;

  return (
    <div
      className="fixed top-0 left-0 right-0 z-50 flex items-center justify-center py-2 px-4 text-xs font-medium transition-all"
      style={{
        background: isOffline ? "#1e293b" : "rgba(245, 158, 11, 0.95)",
        color: isOffline ? "#fbbf24" : "#1e293b",
      }}
    >
      <span>
        {isOffline
          ? "📡 No internet connection — showing cached data"
          : "⚠ Server temporarily unavailable — some data may be outdated"}
      </span>
      <button
        onClick={() => setVisible(false)}
        className="ml-4 opacity-60 hover:opacity-100"
        style={{ background: "none", border: "none", cursor: "pointer", color: "inherit", fontSize: 14 }}
      >
        ✕
      </button>
    </div>
  );
}
