"use client";

import { useState, useEffect, Suspense } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import { isWebAuthnSupported, authenticateWithBiometric, listCredentials, registerCredential } from "@/lib/webauthn";
import { detectBrowserLocale } from "@/lib/utils/detectLocale";

function LoginForm() {
  const t = useTranslations("auth");
  const { login } = useAuthStore();
  const router = useRouter();
  const searchParams = useSearchParams();
  const justRegistered = searchParams.get("registered") === "1";
  const redirectTo = searchParams.get("redirect") ?? "/schedule/my-missions";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Biometric login state
  const [webAuthnAvailable, setWebAuthnAvailable] = useState(false);
  const [biometricLoading, setBiometricLoading] = useState(false);
  const [biometricError, setBiometricError] = useState<string | null>(null);

  // Biometric registration prompt state
  const [showBiometricPrompt, setShowBiometricPrompt] = useState(false);
  const [biometricRegistering, setBiometricRegistering] = useState(false);

  useEffect(() => {
    setWebAuthnAvailable(isWebAuthnSupported());
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(email, password);

      // After successful login, check if we should offer biometric registration
      if (isWebAuthnSupported() && !localStorage.getItem("biometric_prompt_dismissed")) {
        try {
          const creds = await listCredentials();
          if (creds.length === 0) {
            setLoading(false);
            setShowBiometricPrompt(true);
            return; // Don't redirect yet — show the prompt
          }
        } catch {
          // If listing fails, just continue to redirect
        }
      }

      router.push(redirectTo);
    } catch {
      setError(t("invalidCredentials"));
    } finally {
      setLoading(false);
    }
  }

  async function handleBiometricRegister() {
    setBiometricRegistering(true);
    try {
      await registerCredential("המכשיר שלי");
    } catch {
      // Registration failed or cancelled — just continue
    } finally {
      setBiometricRegistering(false);
      setShowBiometricPrompt(false);
      router.push(redirectTo);
    }
  }

  function handleBiometricPromptDismiss() {
    localStorage.setItem("biometric_prompt_dismissed", "1");
    setShowBiometricPrompt(false);
    router.push(redirectTo);
  }

  async function handleBiometricLogin() {
    setBiometricError(null);
    setBiometricLoading(true);
    try {
      const tokens = await authenticateWithBiometric();
      localStorage.setItem("access_token", tokens.accessToken);
      localStorage.setItem("refresh_token", tokens.refreshToken);
      localStorage.removeItem("jobuler-space");
      document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=900; SameSite=Strict`;
      const locale = tokens.preferredLocale || detectBrowserLocale();
      document.cookie = `locale=${locale}; path=/; max-age=31536000; SameSite=Strict`;
      router.push(redirectTo);
    } catch (err: any) {
      if (err?.message === "USER_CANCELLED") {
        // User cancelled — no error to show
        return;
      }
      setBiometricError(t("noCredentialFound"));
    } finally {
      setBiometricLoading(false);
    }
  }

  return (
    <main className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900 p-4">
      <div style={{ width: "100%", maxWidth: "380px" }}>
        {/* Logo */}
        <div style={{ display: "flex", justifyContent: "center", marginBottom: "2rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <ShifterLogo size={40} />
            <span className="text-2xl font-bold text-slate-900 dark:text-white">Shifter</span>
          </div>
        </div>

        {/* Card */}
        <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-lg border border-slate-200 dark:border-slate-700 p-8">
          <div style={{ marginBottom: "1.5rem" }}>
            <h1 className="text-xl font-semibold text-slate-900 dark:text-white m-0">{t("login")}</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">Sign in to your workspace</p>
          </div>

          {/* Biometric Login Button */}
          {webAuthnAvailable && (
            <div style={{ marginBottom: "1.25rem" }}>
              <button
                type="button"
                onClick={handleBiometricLogin}
                disabled={biometricLoading}
                style={{
                  width: "100%",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  gap: "0.625rem",
                  background: biometricLoading
                    ? "linear-gradient(135deg, #a78bfa, #818cf8)"
                    : "linear-gradient(135deg, #7c3aed, #4f46e5)",
                  color: "white",
                  border: "none",
                  borderRadius: "10px",
                  padding: "0.875rem",
                  fontSize: "0.9375rem",
                  fontWeight: 600,
                  cursor: biometricLoading ? "not-allowed" : "pointer",
                  transition: "all 0.15s",
                  boxShadow: "0 2px 8px rgba(79, 70, 229, 0.3)",
                }}
                aria-label={t("biometricLogin")}
              >
                {/* Fingerprint icon */}
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M2 12C2 6.5 6.5 2 12 2a10 10 0 0 1 8 4" />
                  <path d="M5 19.5C5.5 18 6 15 6 12c0-3.5 2.5-6 6-6 3.5 0 6 2.5 6 6 0 1.5-.5 3-1 4" />
                  <path d="M9 12c0-1.5 1.5-3 3-3s3 1.5 3 3-1 4-2 6" />
                  <path d="M12 12v4" />
                  <path d="M2 16c1 2 2.5 3.5 4.5 4.5" />
                  <path d="M15 17c1 1.5 2 3 2.5 4.5" />
                  <path d="M19.5 8c.5 1 .5 2 .5 4 0 2-.5 4-1 6" />
                </svg>
                {biometricLoading ? t("authenticating") : t("biometricLogin")}
              </button>

              {biometricError && (
                <div style={{
                  marginTop: "0.625rem",
                  background: "#fef2f2",
                  border: "1px solid #fecaca",
                  borderRadius: "10px",
                  padding: "0.5rem 0.75rem",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  gap: "0.5rem",
                }}>
                  <p style={{ fontSize: "0.8125rem", color: "#dc2626", margin: 0 }}>{biometricError}</p>
                  <button
                    type="button"
                    onClick={() => setBiometricError(null)}
                    style={{ background: "none", border: "none", cursor: "pointer", color: "#dc2626", padding: 0, lineHeight: 1 }}
                    aria-label="סגור"
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  </button>
                </div>
              )}

              {/* Divider */}
              <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginTop: "1.25rem" }}>
                <div style={{ flex: 1, height: "1px", background: "#e2e8f0" }} />
                <span style={{ fontSize: "0.75rem", color: "#94a3b8", fontWeight: 500 }}>{t("or")}</span>
                <div style={{ flex: 1, height: "1px", background: "#e2e8f0" }} />
              </div>
            </div>
          )}

          {justRegistered && (
            <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 10, padding: "0.625rem 0.875rem", display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "1rem" }}>
              <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="#16a34a" strokeWidth={2} style={{ flexShrink: 0 }}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <p style={{ fontSize: "0.875rem", color: "#15803d", margin: 0 }}>{t("accountCreated")}</p>
            </div>
          )}

          {searchParams.get("reset") === "1" && (
            <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 10, padding: "0.625rem 0.875rem", display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "1rem" }}>
              <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="#16a34a" strokeWidth={2} style={{ flexShrink: 0 }}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <p style={{ fontSize: "0.875rem", color: "#15803d", margin: 0 }}>{t("passwordResetSuccess")}</p>
            </div>
          )}

          <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, color: "#374151", marginBottom: "0.375rem" }}>
                {t("emailOrPhone")}
              </label>
              <input
                type="text"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="email@example.com / 0501234567"
                style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "0.625rem 0.875rem", fontSize: "0.875rem", color: "#0f172a", outline: "none", boxSizing: "border-box" }}
              />
            </div>

            <div>
              <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, color: "#374151", marginBottom: "0.375rem" }}>
                {t("password")}
              </label>
              <div style={{ position: "relative" }}>
                <input
                  type={showPassword ? "text" : "password"}
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "0.625rem 2.5rem 0.625rem 0.875rem", fontSize: "0.875rem", color: "#0f172a", outline: "none", boxSizing: "border-box" }}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  style={{ position: "absolute", right: "0.75rem", top: "50%", transform: "translateY(-50%)", background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 0, display: "flex", alignItems: "center" }}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                >
                  {showPassword ? (
                    <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                    </svg>
                  ) : (
                    <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                  )}
                </button>
              </div>
            </div>

            <div style={{ textAlign: "left" }}>
              <Link href="/forgot-password" style={{ fontSize: "0.75rem", color: "#3b82f6", textDecoration: "none" }}>
                {t("forgotPassword")}?
              </Link>
            </div>

            {error && (
              <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: "10px", padding: "0.625rem 0.875rem", display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="#ef4444" strokeWidth={2} style={{ flexShrink: 0 }}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <p style={{ fontSize: "0.875rem", color: "#dc2626", margin: 0 }}>{error}</p>
              </div>
            )}

            <button
              type="submit"
              disabled={loading}
              style={{ width: "100%", background: loading ? "#93c5fd" : "#3b82f6", color: "white", border: "none", borderRadius: "10px", padding: "0.75rem", fontSize: "0.875rem", fontWeight: 600, cursor: loading ? "not-allowed" : "pointer", marginTop: "0.5rem" }}
            >
              {loading ? t("signingIn") : t("loginButton")}
            </button>
          </form>

          <p style={{ textAlign: "center", fontSize: "0.875rem", color: "#64748b", marginTop: "1.25rem" }}>
            {t("noAccount")}{" "}
            <Link href="/register" style={{ color: "#3b82f6", fontWeight: 500, textDecoration: "none" }}>
              {t("registerButton")}
            </Link>
          </p>

          <div style={{ marginTop: "1.25rem", paddingTop: "1rem", borderTop: "1px solid #f1f5f9" }}>
            <LanguageSwitcher variant="auth" />
          </div>

          <div style={{ textAlign: "center", marginTop: "1.5rem" }}>
            <a
              href="https://ofeklabs.com"
              target="_blank"
              rel="noopener noreferrer"
              style={{ fontSize: "0.6875rem", color: "#94a3b8", textDecoration: "none", letterSpacing: "0.02em" }}
            >
              Built by <span style={{ fontWeight: 600, color: "#64748b" }}>ofeklabs.com</span>
            </a>
          </div>
        </div>
      </div>

      {/* Biometric Registration Prompt Modal */}
      {showBiometricPrompt && (
        <div style={{
          position: "fixed",
          inset: 0,
          background: "rgba(0, 0, 0, 0.5)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          zIndex: 50,
          padding: "1rem",
        }}>
          <div style={{
            background: "white",
            borderRadius: "16px",
            padding: "2rem",
            maxWidth: "340px",
            width: "100%",
            textAlign: "center",
            boxShadow: "0 20px 60px rgba(0, 0, 0, 0.3)",
          }}>
            {/* Fingerprint icon */}
            <div style={{ marginBottom: "1rem" }}>
              <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#7c3aed" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ margin: "0 auto" }}>
                <path d="M2 12C2 6.5 6.5 2 12 2a10 10 0 0 1 8 4" />
                <path d="M5 19.5C5.5 18 6 15 6 12c0-3.5 2.5-6 6-6 3.5 0 6 2.5 6 6 0 1.5-.5 3-1 4" />
                <path d="M9 12c0-1.5 1.5-3 3-3s3 1.5 3 3-1 4-2 6" />
                <path d="M12 12v4" />
                <path d="M2 16c1 2 2.5 3.5 4.5 4.5" />
                <path d="M15 17c1 1.5 2 3 2.5 4.5" />
                <path d="M19.5 8c.5 1 .5 2 .5 4 0 2-.5 4-1 6" />
              </svg>
            </div>
            <h2 style={{ fontSize: "1.125rem", fontWeight: 600, color: "#1e293b", margin: "0 0 0.5rem" }}>
              {t("enableBiometric")}
            </h2>
            <p style={{ fontSize: "0.875rem", color: "#64748b", margin: "0 0 1.5rem" }}>
              {t("enableBiometricDesc")}
            </p>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              <button
                onClick={handleBiometricRegister}
                disabled={biometricRegistering}
                style={{
                  width: "100%",
                  background: biometricRegistering
                    ? "linear-gradient(135deg, #a78bfa, #818cf8)"
                    : "linear-gradient(135deg, #7c3aed, #4f46e5)",
                  color: "white",
                  border: "none",
                  borderRadius: "10px",
                  padding: "0.75rem",
                  fontSize: "0.875rem",
                  fontWeight: 600,
                  cursor: biometricRegistering ? "not-allowed" : "pointer",
                  transition: "all 0.15s",
                }}
              >
                {biometricRegistering ? t("authenticating") : t("enableBiometricYes")}
              </button>
              <button
                onClick={handleBiometricPromptDismiss}
                disabled={biometricRegistering}
                style={{
                  width: "100%",
                  background: "transparent",
                  color: "#64748b",
                  border: "1px solid #e2e8f0",
                  borderRadius: "10px",
                  padding: "0.75rem",
                  fontSize: "0.875rem",
                  fontWeight: 500,
                  cursor: "pointer",
                  transition: "all 0.15s",
                }}
              >
                {t("enableBiometricSkip")}
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginForm />
    </Suspense>
  );
}
