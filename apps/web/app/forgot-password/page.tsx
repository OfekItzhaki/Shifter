"use client";

import { useState, useEffect } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { forgotPassword } from "@/lib/api/auth";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";

export default function ForgotPasswordPage() {
  const t = useTranslations("auth");
  const locale = useLocale();
  const router = useRouter();
  const { isLoggedIn } = useEffectiveAuth();
  const backArrow = locale === "he" ? "→" : "←";
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);
  const [resendCooldown, setResendCooldown] = useState(0);

  // Redirect authenticated users away from forgot-password page
  useEffect(() => {
    if (isLoggedIn) {
      router.replace("/home");
    }
  }, [isLoggedIn, router]);
  const [resendCount, setResendCount] = useState(0);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    await forgotPassword(email);
    setSubmitted(true);
    setLoading(false);
    startCooldown();
  }

  async function handleResend() {
    if (resendCooldown > 0 || resendCount >= 3) return;
    setLoading(true);
    await forgotPassword(email);
    setLoading(false);
    setResendCount(c => c + 1);
    startCooldown();
  }

  function startCooldown() {
    setResendCooldown(60);
    const interval = setInterval(() => {
      setResendCooldown(prev => {
        if (prev <= 1) { clearInterval(interval); return 0; }
        return prev - 1;
      });
    }, 1000);
  }

  return (
    <main style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", background: "#f8fafc", padding: "1rem" }}>
      <div style={{ width: "100%", maxWidth: "380px" }}>
        {/* Logo */}
        <div style={{ display: "flex", justifyContent: "center", marginBottom: "2rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <ShifterLogo size={40} />
            <span style={{ fontSize: "1.5rem", fontWeight: 700, color: "#0f172a" }}>Shifter</span>
          </div>
        </div>

        <div style={{ background: "white", borderRadius: 16, boxShadow: "0 4px 24px rgba(0,0,0,0.08)", border: "1px solid #e2e8f0", padding: "2rem" }}>
          <div style={{ marginBottom: "1.5rem" }}>
            <h1 style={{ fontSize: "1.25rem", fontWeight: 600, color: "#0f172a", margin: 0 }}>{t("forgotPassword")}?</h1>
            <p style={{ fontSize: "0.875rem", color: "#64748b", marginTop: "0.25rem" }}>{t("forgotPasswordHint")}</p>
          </div>

          {submitted ? (
            <div style={{ textAlign: "center" }}>
              <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 10, padding: "1rem" }}>
                <p style={{ fontSize: "0.875rem", color: "#15803d", margin: 0 }}>
                  {t("forgotPasswordSent")}
                </p>
              </div>
              {resendCount < 3 && (
                <button
                  onClick={handleResend}
                  disabled={resendCooldown > 0 || loading}
                  style={{
                    marginTop: "1rem",
                    background: "none",
                    border: "none",
                    color: resendCooldown > 0 ? "#94a3b8" : "#0ea5e9",
                    fontSize: "0.875rem",
                    fontWeight: 500,
                    cursor: resendCooldown > 0 ? "not-allowed" : "pointer",
                  }}
                >
                  {resendCooldown > 0 ? `שלח שוב (${resendCooldown}s)` : "שלח שוב"}
                </button>
              )}
              {resendCount >= 3 && (
                <p style={{ fontSize: "0.75rem", color: "#94a3b8", marginTop: "0.75rem" }}>
                  הגעת למגבלת הניסיונות. נסה שוב מאוחר יותר.
                </p>
              )}
            </div>
          ) : (
            <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, color: "#374151", marginBottom: "0.375rem" }}>
                  {t("email")}
                </label>
                <input
                  type="email"
                  required
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: 10, padding: "0.625rem 0.875rem", fontSize: "0.875rem", color: "#0f172a", outline: "none", boxSizing: "border-box" }}
                />
              </div>
              <button
                type="submit"
                disabled={loading}
                style={{ width: "100%", background: loading ? "#7dd3fc" : "#0ea5e9", color: "white", border: "none", borderRadius: 10, padding: "0.75rem", fontSize: "0.875rem", fontWeight: 600, cursor: loading ? "not-allowed" : "pointer" }}
              >
                {loading ? t("sending") : t("sendResetLink")}
              </button>
            </form>
          )}

          <p style={{ textAlign: "center", fontSize: "0.875rem", color: "#64748b", marginTop: "1.25rem" }}>
            <Link href="/login" style={{ color: "#0ea5e9", fontWeight: 500, textDecoration: "none" }}>
              {backArrow} {t("backToLogin")}
            </Link>
          </p>

          <div style={{ marginTop: "1.25rem", paddingTop: "1rem", borderTop: "1px solid #f1f5f9" }}>
            <LanguageSwitcher variant="auth" />
          </div>
        </div>
      </div>
    </main>
  );
}
