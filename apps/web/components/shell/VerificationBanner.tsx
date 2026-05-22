"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { getMe, resendVerification } from "@/lib/api/auth";
import { useAuthStore } from "@/lib/store/authStore";

/**
 * Non-blocking dismissible banner shown to unverified users.
 * Encourages email verification without gating any functionality.
 * Dismissed state is session-scoped (resets on page reload).
 */
export default function VerificationBanner() {
  const t = useTranslations("verifyEmail.banner");
  const { isAuthenticated } = useAuthStore();
  const [dismissed, setDismissed] = useState(false);
  const [emailVerified, setEmailVerified] = useState<boolean | null>(null);
  const [resendStatus, setResendStatus] = useState<"idle" | "sending" | "sent">("idle");

  useEffect(() => {
    if (!isAuthenticated) return;
    getMe()
      .then((me) => setEmailVerified(me.emailVerified))
      .catch(() => {
        // If we can't fetch, don't show the banner
        setEmailVerified(true);
      });
  }, [isAuthenticated]);

  // Don't render if: not authenticated, still loading, already verified, or dismissed
  if (!isAuthenticated || emailVerified === null || emailVerified || dismissed) {
    return null;
  }

  async function handleResend() {
    setResendStatus("sending");
    try {
      await resendVerification();
      setResendStatus("sent");
    } catch {
      setResendStatus("idle");
    }
  }

  return (
    <div
      role="alert"
      style={{
        background: "#f0f9ff",
        border: "1px solid #bfdbfe",
        borderRadius: 10,
        padding: "10px 16px",
        margin: "0 0 16px",
        display: "flex",
        alignItems: "center",
        gap: 12,
        fontSize: "0.8125rem",
        color: "#1e40af",
      }}
    >
      <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} style={{ flexShrink: 0 }}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
      </svg>
      <span style={{ flex: 1 }}>{t("message")}</span>
      <button
        onClick={handleResend}
        disabled={resendStatus !== "idle"}
        style={{
          background: resendStatus === "sent" ? "#16a34a" : "#0ea5e9",
          color: "white",
          border: "none",
          borderRadius: 6,
          padding: "4px 12px",
          fontSize: "0.75rem",
          fontWeight: 600,
          cursor: resendStatus !== "idle" ? "not-allowed" : "pointer",
          whiteSpace: "nowrap",
        }}
      >
        {resendStatus === "sent" ? "✓" : resendStatus === "sending" ? "..." : t("resend")}
      </button>
      <button
        onClick={() => setDismissed(true)}
        aria-label={t("dismiss")}
        style={{
          background: "none",
          border: "none",
          cursor: "pointer",
          color: "#64748b",
          padding: 4,
          display: "flex",
          alignItems: "center",
        }}
      >
        <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>
  );
}
