"use client";

import { useState, useEffect } from "react";

/**
 * Subtle fixed banner at the bottom of the screen when API is unavailable.
 * Doesn't push content, doesn't jump, stays visible while offline.
 */
export default function ApiStatusBanner() {
  const [visible, setVisible] = useState(false);
  const [isOffline, setIsOffline] = useState(false);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    let errorCount = 0;

    function handleApiError() {
      errorCount++;
      if (errorCount >= 2 && !dismissed) {
        setVisible(true);
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
    };
  }, [dismissed]);

  if (!visible) return null;

  return (
    <div
      style={{
        position: "fixed",
        bottom: 16,
        left: "50%",
        transform: "translateX(-50%)",
        zIndex: 9999,
        maxWidth: 400,
        width: "calc(100% - 32px)",
        padding: "10px 16px",
        borderRadius: 12,
        background: isOffline ? "#1e293b" : "rgba(30, 41, 59, 0.95)",
        border: `1px solid ${isOffline ? "#fbbf24" : "#475569"}`,
        backdropFilter: "blur(8px)",
        display: "flex",
        alignItems: "center",
        gap: 10,
        boxShadow: "0 4px 20px rgba(0,0,0,0.3)",
        animation: "slideUp 0.3s ease-out",
      }}
    >
      <span style={{ fontSize: 14 }}>{isOffline ? "📡" : "⚠️"}</span>
      <span style={{ flex: 1, fontSize: 12, fontWeight: 500, color: isOffline ? "#fbbf24" : "#94a3b8" }}>
        {isOffline
          ? "No internet connection"
          : "Server temporarily unavailable"}
      </span>
      {!isOffline && (
        <button
          onClick={() => { setVisible(false); setDismissed(true); }}
          style={{
            background: "none",
            border: "none",
            cursor: "pointer",
            color: "#64748b",
            padding: 4,
            lineHeight: 1,
            borderRadius: 4,
            flexShrink: 0,
          }}
          aria-label="Dismiss"
        >
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      )}
    </div>
  );
}
