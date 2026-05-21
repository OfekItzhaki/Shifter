"use client";

import { useState, useEffect } from "react";
import Link from "next/link";

const STORAGE_KEY = "cookie-consent-accepted";

export default function CookieConsent() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const accepted = localStorage.getItem(STORAGE_KEY);
    if (!accepted) {
      setVisible(true);
    }
  }, []);

  function handleAccept() {
    localStorage.setItem(STORAGE_KEY, "1");
    setVisible(false);
  }

  if (!visible) return null;

  return (
    <div
      dir="rtl"
      style={{
        position: "fixed",
        bottom: 0,
        left: 0,
        right: 0,
        zIndex: 9999,
        background: "#1e293b",
        borderTop: "1px solid #334155",
        padding: "12px 20px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "12px",
        flexWrap: "wrap",
      }}
    >
      <p style={{ color: "#cbd5e1", fontSize: "0.8125rem", margin: 0, lineHeight: 1.5 }}>
        אנחנו משתמשים בעוגיות לצורך אימות והפעלת השירות בלבד.{" "}
        <Link href="/privacy" style={{ color: "#93c5fd", textDecoration: "underline" }}>
          מדיניות פרטיות
        </Link>
      </p>
      <button
        onClick={handleAccept}
        style={{
          background: "#3b82f6",
          color: "white",
          border: "none",
          borderRadius: "6px",
          padding: "6px 16px",
          fontSize: "0.8125rem",
          fontWeight: 600,
          cursor: "pointer",
          whiteSpace: "nowrap",
          flexShrink: 0,
        }}
      >
        הבנתי
      </button>
    </div>
  );
}
