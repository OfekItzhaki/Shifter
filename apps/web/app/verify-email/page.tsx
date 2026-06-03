"use client";

import { Suspense, useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { verifyEmail, resendVerification } from "@/lib/api/auth";
import ShifterLogo from "@/components/shell/ShifterLogo";

function VerifyEmailContent() {
  const t = useTranslations("verifyEmail");
  const locale = useLocale();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const backArrow = locale === "he" ? "→" : "←";

  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");
  const [resendStatus, setResendStatus] = useState<"idle" | "sending" | "sent" | "error">("idle");

  useEffect(() => {
    if (!token) {
      setStatus("error");
      return;
    }
    verifyEmail(token)
      .then(() => setStatus("success"))
      .catch(() => setStatus("error"));
  }, [token]);

  async function handleResend() {
    setResendStatus("sending");
    try {
      await resendVerification();
      setResendStatus("sent");
    } catch {
      setResendStatus("error");
    }
  }

  if (status === "loading") {
    return (
      <div style={{ textAlign: "center", padding: "2rem 0" }}>
        <div style={{ width: 40, height: 40, border: "3px solid #e2e8f0", borderTopColor: "#0ea5e9", borderRadius: "50%", animation: "spin 0.8s linear infinite", margin: "0 auto 1rem" }} />
        <p style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("loading")}</p>
      </div>
    );
  }

  if (status === "success") {
    return (
      <div style={{ textAlign: "center" }}>
        <div style={{ width: 56, height: 56, borderRadius: "50%", background: "#dcfce7", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 1rem" }}>
          <svg width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="#16a34a" strokeWidth={2.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>
        <h2 style={{ fontSize: "1.125rem", fontWeight: 600, color: "#0f172a", margin: "0 0 0.5rem" }}>{t("success")}</h2>
        <p style={{ fontSize: "0.875rem", color: "#64748b", margin: "0 0 1.5rem" }}>{t("successMessage")}</p>
        <Link
          href="/login"
          style={{ display: "inline-block", background: "#0ea5e9", color: "white", borderRadius: 10, padding: "0.625rem 1.5rem", fontSize: "0.875rem", fontWeight: 600, textDecoration: "none" }}
        >
          {t("goToLogin")}
        </Link>
      </div>
    );
  }

  // Error state
  return (
    <div style={{ textAlign: "center" }}>
      <div style={{ width: 56, height: 56, borderRadius: "50%", background: "#fef2f2", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 1rem" }}>
        <svg width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="#dc2626" strokeWidth={2.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </div>
      <h2 style={{ fontSize: "1.125rem", fontWeight: 600, color: "#0f172a", margin: "0 0 0.5rem" }}>{t("error")}</h2>
      <p style={{ fontSize: "0.875rem", color: "#64748b", margin: "0 0 1.5rem" }}>{t("errorMessage")}</p>

      <button
        onClick={handleResend}
        disabled={resendStatus === "sending" || resendStatus === "sent"}
        style={{
          display: "inline-block",
          background: resendStatus === "sent" ? "#16a34a" : resendStatus === "sending" ? "#7dd3fc" : "#0ea5e9",
          color: "white",
          border: "none",
          borderRadius: 10,
          padding: "0.625rem 1.5rem",
          fontSize: "0.875rem",
          fontWeight: 600,
          cursor: resendStatus === "sending" || resendStatus === "sent" ? "not-allowed" : "pointer",
          marginBottom: "1rem",
        }}
      >
        {resendStatus === "sent" ? "✓" : t("resend")}
      </button>

      <p style={{ fontSize: "0.875rem", color: "#64748b" }}>
        <Link href="/login" style={{ color: "#0ea5e9", fontWeight: 500, textDecoration: "none" }}>
          {backArrow} {t("goToLogin")}
        </Link>
      </p>
    </div>
  );
}

export default function VerifyEmailPage() {
  const t = useTranslations("verifyEmail");
  return (
    <main style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", background: "#f8fafc", padding: "1rem" }}>
      <div style={{ width: "100%", maxWidth: "380px" }}>
        <div style={{ display: "flex", justifyContent: "center", marginBottom: "2rem" }}>
          <ShifterLogo size={132} variant="full" />
        </div>

        <div style={{ background: "white", borderRadius: 16, boxShadow: "0 4px 24px rgba(0,0,0,0.08)", border: "1px solid #e2e8f0", padding: "2rem" }}>
          <Suspense fallback={<p style={{ color: "#64748b", fontSize: "0.875rem", textAlign: "center" }}>{t("loading")}</p>}>
            <VerifyEmailContent />
          </Suspense>
        </div>
      </div>
    </main>
  );
}
