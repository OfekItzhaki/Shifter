"use client";

import { useState, useEffect, useRef, Suspense } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import AuthBrand from "@/components/shell/AuthBrand";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import LegalLinks from "@/components/legal/LegalLinks";
import {
  authenticateWithBiometric,
  isConditionalMediationAvailable,
  isPlatformAuthenticatorAvailable,
  isWebAuthnSupported,
  listCredentials,
  registerCredential,
  type LoginTokens,
} from "@/lib/webauthn";
import { detectBrowserLocale } from "@/lib/utils/detectLocale";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";
import { notifyAuthTokenChanged } from "@/lib/auth/tokenState";
import { setAuthGuardCookie, setLocaleCookie } from "@/lib/auth/authGuardCookie";

function completeLogin(tokens: LoginTokens) {
  localStorage.setItem("access_token", tokens.accessToken);
  localStorage.removeItem("refresh_token");
  notifyAuthTokenChanged();
  setAuthGuardCookie();
  const locale = tokens.preferredLocale || detectBrowserLocale();
  setLocaleCookie(locale);
  useAuthStore.setState({
    userId: tokens.userId,
    displayName: tokens.displayName,
    preferredLocale: locale,
    isAuthenticated: true,
    isPlatformAdmin: tokens.isPlatformAdmin ?? false,
    adminGroupId: null,
    timezoneId: tokens.timezoneId ?? "Asia/Jerusalem",
    timezoneOffsetMinutes: tokens.timezoneOffsetMinutes ?? 120,
  });
}

function isLikelyTouchDevice(): boolean {
  if (typeof window === "undefined" || typeof navigator === "undefined") return false;
  return window.matchMedia("(pointer: coarse)").matches || navigator.maxTouchPoints > 0;
}

function LoginForm() {
  const t = useTranslations("auth");
  const login = useAuthStore((s) => s.login);
  const { isLoggedIn } = useEffectiveAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const justRegistered = searchParams.get("registered") === "1";
  const redirectTo = searchParams.get("redirect") ?? "/home";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [suppressAuthRedirect, setSuppressAuthRedirect] = useState(false);

  // Biometric login state
  const [biometricAvailable, setBiometricAvailable] = useState(false);
  const [biometricLoading, setBiometricLoading] = useState(false);
  const [biometricError, setBiometricError] = useState<string | null>(null);
  const [showPasskeySetup, setShowPasskeySetup] = useState(false);
  const [pendingRedirect, setPendingRedirect] = useState(redirectTo);
  const [passkeySetupLoading, setPasskeySetupLoading] = useState(false);
  const [passkeySetupError, setPasskeySetupError] = useState<string | null>(null);
  const conditionalMediationAttemptedRef = useRef(false);

  // Redirect authenticated users away from login page
  useEffect(() => {
    if (isLoggedIn && !suppressAuthRedirect && !showPasskeySetup) {
      router.replace("/home");
    }
  }, [isLoggedIn, router, showPasskeySetup, suppressAuthRedirect]);

  useEffect(() => {
    let cancelled = false;

    async function detectBiometricAvailability() {
      const available = await isPlatformAuthenticatorAvailable();
      if (!cancelled) setBiometricAvailable(available && isLikelyTouchDevice());
    }

    detectBiometricAvailability();
    return () => { cancelled = true; };
  }, []);

  async function tryConditionalMediation() {
    if (!isWebAuthnSupported()) return;
    if (conditionalMediationAttemptedRef.current) return;
    conditionalMediationAttemptedRef.current = true;

    const available = await isConditionalMediationAvailable();
    if (!available) return;

    try {
      const tokens = await authenticateWithBiometric({ mediation: "conditional" });
      setSuppressAuthRedirect(true);
      completeLogin(tokens);
      router.push(redirectTo);
    } catch {
      // The user can keep using saved credentials or password login.
    }
  }

  async function finishPasswordLogin() {
    const canOfferPasskey = await isPlatformAuthenticatorAvailable();
    if (!canOfferPasskey || !isLikelyTouchDevice()) {
      router.push(redirectTo);
      return;
    }

    try {
      const token = localStorage.getItem("access_token") ?? undefined;
      const credentials = await listCredentials(token);
      if (credentials.length > 0) {
        router.push(redirectTo);
        return;
      }
    } catch {
      router.push(redirectTo);
      return;
    }

    setPendingRedirect(redirectTo);
    setShowPasskeySetup(true);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    setSuppressAuthRedirect(true);
    try {
      await login(email, password);
      await finishPasswordLogin();
    } catch {
      setSuppressAuthRedirect(false);
      setError(t("invalidCredentials"));
    } finally {
      setLoading(false);
    }
  }

  async function handleBiometricLogin() {
    setBiometricError(null);
    setBiometricLoading(true);
    setSuppressAuthRedirect(true);
    try {
      const tokens = await authenticateWithBiometric();
      completeLogin(tokens);
      router.push(redirectTo);
    } catch (err) {
      setSuppressAuthRedirect(false);
      const message = err instanceof Error && err.message === "USER_CANCELLED"
        ? t("noCredentialFound")
        : t("biometricFailed");
      setBiometricError(message);
    } finally {
      setBiometricLoading(false);
    }
  }

  async function handleEnablePasskey() {
    setPasskeySetupLoading(true);
    setPasskeySetupError(null);
    try {
      await registerCredential(t("thisDevice"));
      router.push(pendingRedirect);
    } catch (err) {
      if (err instanceof Error && err.message === "USER_CANCELLED") {
        router.push(pendingRedirect);
        return;
      }
      setPasskeySetupError(t("passkeySetupError"));
    } finally {
      setPasskeySetupLoading(false);
    }
  }

  function skipPasskeySetup() {
    router.push(pendingRedirect);
  }

  return (
    <main className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900 p-4">
      <div style={{ width: "100%", maxWidth: "380px" }}>
        {/* Logo */}
        <div style={{ display: "flex", justifyContent: "center", marginBottom: "2rem" }}>
          <AuthBrand />
        </div>

        {/* Card */}
        <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-lg border border-slate-200 dark:border-slate-700 p-8">
          <div style={{ marginBottom: "1.5rem" }}>
            <h1 className="text-xl font-semibold text-slate-900 dark:text-white m-0">{t("login")}</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">{t("loginSubtitle")}</p>
          </div>

          {/* Biometric error (shown if conditional mediation fails silently) */}
          {biometricError && (
            <div style={{
              marginBottom: "1rem",
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
                aria-label={t("close")}
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
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
                onFocus={tryConditionalMediation}
                placeholder={t("emailOrPhonePlaceholder")}
                autoComplete="username"
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
                  onFocus={tryConditionalMediation}
                  placeholder="••••••••"
                  autoComplete="current-password"
                  style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "0.625rem 2.5rem 0.625rem 0.875rem", fontSize: "0.875rem", color: "#0f172a", outline: "none", boxSizing: "border-box" }}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  style={{ position: "absolute", right: "0.75rem", top: "50%", transform: "translateY(-50%)", background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 0, display: "flex", alignItems: "center" }}
                  aria-label={showPassword ? t("hidePassword") : t("showPassword")}
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
              <Link href="/forgot-password" style={{ fontSize: "0.75rem", color: "#0ea5e9", textDecoration: "none" }}>
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
              style={{ width: "100%", minHeight: 44, background: loading ? "#7dd3fc" : "#0ea5e9", color: "white", border: "none", borderRadius: "10px", padding: "0.75rem", fontSize: "0.875rem", fontWeight: 600, cursor: loading ? "not-allowed" : "pointer", marginTop: "0.5rem" }}
            >
              {loading ? t("signingIn") : t("loginButton")}
            </button>
          </form>

          {biometricAvailable && (
            <>
              <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", margin: "1.25rem 0 0.25rem" }}>
                <div style={{ flex: 1, height: 1, background: "#f1f5f9" }} />
                <span style={{ fontSize: "0.75rem", color: "#94a3b8" }}>{t("or")}</span>
                <div style={{ flex: 1, height: 1, background: "#f1f5f9" }} />
              </div>
              <button
                type="button"
                onClick={handleBiometricLogin}
                disabled={biometricLoading || loading}
                style={{
                  width: "100%",
                  minHeight: 44,
                  border: "1px solid #bae6fd",
                  background: "#f0f9ff",
                  color: "#0369a1",
                  borderRadius: "10px",
                  padding: "0.75rem",
                  fontSize: "0.875rem",
                  fontWeight: 600,
                  cursor: biometricLoading || loading ? "not-allowed" : "pointer",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  gap: "0.5rem",
                }}
              >
                <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 11c0-2.21 1.79-4 4-4m-4 4v2m0 4v.01M6.5 8.5a7.5 7.5 0 0112.9 5.2c0 2.9-1.7 5.4-4.1 6.6M4.6 12.5A7.5 7.5 0 0112 4.5" />
                </svg>
                {biometricLoading ? t("authenticating") : t("biometricLogin")}
              </button>
            </>
          )}

          <p style={{ textAlign: "center", fontSize: "0.875rem", color: "#64748b", marginTop: "1.25rem" }}>
            {t("noAccount")}{" "}
            <Link href="/register" style={{ color: "#0ea5e9", fontWeight: 500, textDecoration: "none" }}>
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

          <LegalLinks compact className="mt-3" />
        </div>

        {showPasskeySetup && (
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="passkey-setup-title"
            style={{
              position: "fixed",
              inset: 0,
              background: "rgba(15, 23, 42, 0.45)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              padding: "1rem",
              zIndex: 50,
            }}
          >
            <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-xl" style={{ width: "100%", maxWidth: 360, borderRadius: 16, padding: "1.25rem" }}>
              <div style={{ width: 42, height: 42, borderRadius: 12, background: "#e0f2fe", color: "#0369a1", display: "flex", alignItems: "center", justifyContent: "center", marginBottom: "0.875rem" }}>
                <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 11c0-2.21 1.79-4 4-4m-4 4v2m0 4v.01M6.5 8.5a7.5 7.5 0 0112.9 5.2c0 2.9-1.7 5.4-4.1 6.6M4.6 12.5A7.5 7.5 0 0112 4.5" />
                </svg>
              </div>
              <h2 id="passkey-setup-title" className="text-base font-semibold text-slate-900 dark:text-white" style={{ margin: "0 0 0.375rem" }}>
                {t("enableBiometric")}
              </h2>
              <p className="text-sm text-slate-500 dark:text-slate-400" style={{ margin: "0 0 1rem" }}>
                {t("enableBiometricDesc")}
              </p>
              {passkeySetupError && (
                <p style={{ fontSize: "0.8125rem", color: "#dc2626", margin: "0 0 0.75rem" }}>
                  {passkeySetupError}
                </p>
              )}
              <div style={{ display: "flex", gap: "0.75rem" }}>
                <button
                  type="button"
                  onClick={handleEnablePasskey}
                  disabled={passkeySetupLoading}
                  style={{ flex: 1, minHeight: 44, background: passkeySetupLoading ? "#7dd3fc" : "#0ea5e9", color: "white", border: "none", borderRadius: 10, padding: "0.75rem", fontSize: "0.875rem", fontWeight: 600, cursor: passkeySetupLoading ? "not-allowed" : "pointer" }}
                >
                  {passkeySetupLoading ? t("authenticating") : t("enableBiometricYes")}
                </button>
                <button
                  type="button"
                  onClick={skipPasskeySetup}
                  disabled={passkeySetupLoading}
                  style={{ flex: 1, minHeight: 44, background: "white", color: "#64748b", border: "1px solid #e2e8f0", borderRadius: 10, padding: "0.75rem", fontSize: "0.875rem", fontWeight: 600, cursor: passkeySetupLoading ? "not-allowed" : "pointer" }}
                >
                  {t("enableBiometricSkip")}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
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
