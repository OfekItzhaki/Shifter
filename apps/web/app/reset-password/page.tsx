"use client";

import { useState, Suspense } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { resetPassword } from "@/lib/api/auth";
import ShifterLogo from "@/components/shell/ShifterLogo";

function ResetPasswordForm() {
  const t = useTranslations("auth");
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (newPassword !== confirmPassword) {
      setError(t("passwordMismatch"));
      return;
    }
    if (newPassword.length < 8) {
      setError(t("passwordTooShort"));
      return;
    }
    if (!token) {
      setError(t("invalidToken"));
      return;
    }

    setLoading(true);
    try {
      await resetPassword(token, newPassword);
      router.push("/login?reset=1");
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string; message?: string } }; message?: string };
      const msg = axiosErr?.response?.data?.error ?? axiosErr?.response?.data?.message ?? axiosErr?.message;
      if (msg?.includes("Invalid or expired")) {
        setError(t("tokenExpired"));
      } else {
        setError(msg ?? t("resetError"));
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <div>
        <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, color: "#374151", marginBottom: "0.375rem" }}>
          {t("newPassword")}
        </label>
        <input
          type="password"
          required
          value={newPassword}
          onChange={e => setNewPassword(e.target.value)}
          placeholder={t("passwordMinLength")}
          style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: 10, padding: "0.625rem 0.875rem", fontSize: "0.875rem", color: "#0f172a", outline: "none", boxSizing: "border-box" }}
        />
      </div>
      <div>
        <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, color: "#374151", marginBottom: "0.375rem" }}>
          {t("confirmPassword")}
        </label>
        <input
          type="password"
          required
          value={confirmPassword}
          onChange={e => setConfirmPassword(e.target.value)}
          placeholder={t("confirmPasswordPlaceholder")}
          style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: 10, padding: "0.625rem 0.875rem", fontSize: "0.875rem", color: "#0f172a", outline: "none", boxSizing: "border-box" }}
        />
      </div>
      {error && (
        <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 10, padding: "0.625rem 0.875rem" }}>
          <p style={{ fontSize: "0.875rem", color: "#dc2626", margin: 0 }}>{error}</p>
        </div>
      )}
      <button
        type="submit"
        disabled={loading}
        style={{ width: "100%", background: loading ? "#7dd3fc" : "#0ea5e9", color: "white", border: "none", borderRadius: 10, padding: "0.75rem", fontSize: "0.875rem", fontWeight: 600, cursor: loading ? "not-allowed" : "pointer" }}
      >
        {loading ? t("resetting") : t("resetPassword")}
      </button>
    </form>
  );
}

export default function ResetPasswordPage() {
  const t = useTranslations("auth");
  const locale = useLocale();
  const backArrow = locale === "he" ? "→" : "←";
  return (
    <main style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", background: "#f8fafc", padding: "1rem" }}>
      <div style={{ width: "100%", maxWidth: "380px" }}>
        <div style={{ display: "flex", justifyContent: "center", marginBottom: "2rem" }}>
          <ShifterLogo size={132} variant="full" />
        </div>

        <div style={{ background: "white", borderRadius: 16, boxShadow: "0 4px 24px rgba(0,0,0,0.08)", border: "1px solid #e2e8f0", padding: "2rem" }}>
          <div style={{ marginBottom: "1.5rem" }}>
            <h1 style={{ fontSize: "1.25rem", fontWeight: 600, color: "#0f172a", margin: 0 }}>{t("resetPassword")}</h1>
            <p style={{ fontSize: "0.875rem", color: "#64748b", marginTop: "0.25rem" }}>{t("resetPasswordHint")}</p>
          </div>
          <Suspense fallback={<p style={{ color: "#64748b", fontSize: "0.875rem" }}>{t("loading")}</p>}>
            <ResetPasswordForm />
          </Suspense>
          <p style={{ textAlign: "center", fontSize: "0.875rem", color: "#64748b", marginTop: "1.25rem" }}>
            <Link href="/login" style={{ color: "#0ea5e9", fontWeight: 500, textDecoration: "none" }}>
              {backArrow} {t("backToLogin")}
            </Link>
          </p>
        </div>
      </div>
    </main>
  );
}
