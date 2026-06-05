"use client";

import { useEffect, useRef, useState, useCallback, FormEvent, KeyboardEvent } from "react";
import { useTranslations, useLocale } from "next-intl";
import { apiClient } from "@/lib/api/client";
import { isRtl as isRtlLocale } from "@/lib/i18n/locales";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface ReAuthDialogProps {
  open: boolean;
  onSuccess: () => void;
  onCancel: () => void;
  mode: "management" | "platform";
  spaceId?: string;
}

interface CredentialState {
  hasPassword: boolean;
  loading: boolean;
  hasWebAuthnCredentials: boolean;
  credentialCheckLoading: boolean;
}

export type ActiveAuthMethod = "webauthn" | "password";

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Check if the browser supports WebAuthn credentials.get AND a platform authenticator is available */
async function isWebAuthnPlatformAvailable(): Promise<boolean> {
  if (typeof window === "undefined") return false;
  if (!window.navigator?.credentials || typeof window.navigator.credentials.get !== "function") return false;
  // Check if a platform authenticator (fingerprint, Face ID, Windows Hello) is actually available
  try {
    if (typeof window.PublicKeyCredential?.isUserVerifyingPlatformAuthenticatorAvailable === "function") {
      return await window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
    }
  } catch {
    // If the check fails, fall back to basic API support check
  }
  return false;
}

/** Basic check if WebAuthn API exists (doesn't verify platform authenticator) */
function isWebAuthnSupported(): boolean {
  return typeof window !== "undefined" &&
    !!window.navigator?.credentials &&
    typeof window.navigator.credentials.get === "function";
}

/** Convert an ArrayBuffer to a base64url string */
function bufferToBase64url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.byteLength; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** Convert a base64url string to an ArrayBuffer */
function base64urlToBuffer(base64url: string): ArrayBuffer {
  const base64 = base64url.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes.buffer as ArrayBuffer;
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function ReAuthDialog({ open, onSuccess, onCancel, mode, spaceId }: ReAuthDialogProps) {
  const t = useTranslations("reAuth");
  const locale = useLocale();
  const isRtl = isRtlLocale(locale);

  // Credential availability
  const [credentials, setCredentials] = useState<CredentialState>({
    hasPassword: false,
    loading: true,
    hasWebAuthnCredentials: false,
    credentialCheckLoading: true,
  });

  // WebAuthn browser support
  const [webAuthnSupported, setWebAuthnSupported] = useState(false);

  // Active authentication method view
  const [activeMethod, setActiveMethod] = useState<ActiveAuthMethod>("password");

  // WebAuthn state
  const [webAuthnLoading, setWebAuthnLoading] = useState(false);
  const [webAuthnError, setWebAuthnError] = useState<string | null>(null);

  // Lockout state
  const [isLockedOut, setIsLockedOut] = useState(false);
  const [lockoutRemainingSeconds, setLockoutRemainingSeconds] = useState(0);
  const lockoutIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Form state
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [passwordValidationError, setPasswordValidationError] = useState<string | null>(null);
  const [isReadonly, setIsReadonly] = useState(true);

  // Refs for focus management
  const dialogRef = useRef<HTMLDivElement>(null);
  const submitButtonRef = useRef<HTMLButtonElement>(null);
  const passwordInputRef = useRef<HTMLInputElement>(null);
  const biometricButtonRef = useRef<HTMLButtonElement>(null);
  const autoTriggeredRef = useRef(false);

  // ── Check WebAuthn platform authenticator availability ──────────────────

  useEffect(() => {
    isWebAuthnPlatformAvailable().then(setWebAuthnSupported);
  }, []);

  // ── Lockout countdown timer ───────────────────────────────────────────────

  useEffect(() => {
    if (!isLockedOut || lockoutRemainingSeconds <= 0) {
      if (lockoutIntervalRef.current) {
        clearInterval(lockoutIntervalRef.current);
        lockoutIntervalRef.current = null;
      }
      return;
    }

    lockoutIntervalRef.current = setInterval(() => {
      setLockoutRemainingSeconds((prev) => {
        if (prev <= 1) {
          setIsLockedOut(false);
          setError(null);
          setWebAuthnError(null);
          if (lockoutIntervalRef.current) {
            clearInterval(lockoutIntervalRef.current);
            lockoutIntervalRef.current = null;
          }
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => {
      if (lockoutIntervalRef.current) {
        clearInterval(lockoutIntervalRef.current);
        lockoutIntervalRef.current = null;
      }
    };
  }, [isLockedOut, lockoutRemainingSeconds]);

  // Clean up lockout interval on unmount or dialog close
  useEffect(() => {
    if (!open && lockoutIntervalRef.current) {
      clearInterval(lockoutIntervalRef.current);
      lockoutIntervalRef.current = null;
    }
  }, [open]);

  // ── Fetch credential availability (WebAuthn check) ────────────────────────

  useEffect(() => {
    if (!open) return;

    const controller = new AbortController();
    const timeoutId = setTimeout(() => {
      controller.abort();
    }, 5000);

    setCredentials((prev) => ({
      ...prev,
      loading: true,
      credentialCheckLoading: true,
    }));

    apiClient
      .get("/auth/webauthn/credentials", { signal: controller.signal })
      .then(async (response) => {
        clearTimeout(timeoutId);
        const credentialsList = response.data;
        const hasWebAuthn = Array.isArray(credentialsList) && credentialsList.length > 0;
        const platformAvailable = await isWebAuthnPlatformAvailable();
        setWebAuthnSupported(platformAvailable);
        setCredentials({
          hasPassword: true,
          loading: false,
          hasWebAuthnCredentials: hasWebAuthn && platformAvailable,
          credentialCheckLoading: false,
        });
        // Set active method based on credential availability AND platform authenticator
        if (hasWebAuthn && platformAvailable) {
          setActiveMethod("webauthn");
        } else {
          setActiveMethod("password");
        }
      })
      .catch((err) => {
        clearTimeout(timeoutId);
        if (err.name === "AbortError" || err.name === "CanceledError" || controller.signal.aborted) {
          console.warn("[ReAuthDialog] WebAuthn credential check timed out (>5s), falling back to password form.");
        } else {
          console.error("[ReAuthDialog] WebAuthn credential check failed, falling back to password form.", err);
        }
        // Fall back to password form on any failure
        setCredentials({
          hasPassword: true,
          loading: false,
          hasWebAuthnCredentials: false,
          credentialCheckLoading: false,
        });
        setActiveMethod("password");
      });

    return () => {
      clearTimeout(timeoutId);
      controller.abort();
    };
  }, [open]);

  // ── Reset state when dialog opens/closes ─────────────────────────────────

  useEffect(() => {
    if (open) {
      setPassword("");
      setError(null);
      setPasswordValidationError(null);
      setWebAuthnError(null);
      setIsSubmitting(false);
      setWebAuthnLoading(false);
      setIsLockedOut(false);
      setLockoutRemainingSeconds(0);
      setIsReadonly(true);
      autoTriggeredRef.current = false;
    } else {
      setCredentials({
        hasPassword: false,
        loading: true,
        hasWebAuthnCredentials: false,
        credentialCheckLoading: true,
      });
      setActiveMethod("password");
      setIsLockedOut(false);
      setLockoutRemainingSeconds(0);
    }
  }, [open]);

  // ── Focus management ─────────────────────────────────────────────────────

  useEffect(() => {
    if (!open || credentials.credentialCheckLoading) return;

    const timer = setTimeout(() => {
      if (activeMethod === "webauthn" && biometricButtonRef.current) {
        biometricButtonRef.current.focus();
      } else if (credentials.hasPassword && passwordInputRef.current) {
        passwordInputRef.current.focus();
      } else if (submitButtonRef.current) {
        submitButtonRef.current.focus();
      }
    }, 50);

    return () => clearTimeout(timer);
  }, [open, credentials.credentialCheckLoading, credentials.hasPassword, activeMethod]);

  // ── Auto-trigger biometric on mobile ─────────────────────────────────────
  // On touch devices (phones/tablets), automatically start the WebAuthn ceremony
  // when the dialog opens and biometrics are available — no button click needed.

  useEffect(() => {
    if (!open || credentials.credentialCheckLoading || autoTriggeredRef.current) return;
    if (activeMethod !== "webauthn" || !webAuthnSupported) return;
    if (!credentials.hasWebAuthnCredentials) return;

    // Detect touch device (mobile/tablet)
    const isTouchDevice = "ontouchstart" in window || navigator.maxTouchPoints > 0;
    if (!isTouchDevice) return;

    // Auto-trigger after a short delay to let the dialog render
    autoTriggeredRef.current = true;
    const timer = setTimeout(() => {
      biometricButtonRef.current?.click();
    }, 300);

    return () => clearTimeout(timer);
  }, [open, credentials.credentialCheckLoading, activeMethod, webAuthnSupported, credentials.hasWebAuthnCredentials]);

  // ── Focus trap ───────────────────────────────────────────────────────────

  const handleKeyDown = useCallback((e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "Escape") {
      e.preventDefault();
      if (!isSubmitting && !webAuthnLoading && !credentials.credentialCheckLoading) onCancel();
      return;
    }

    if (e.key === "Tab") {
      const dialog = dialogRef.current;
      if (!dialog) return;

      const focusableElements = dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])'
      );
      if (focusableElements.length === 0) return;

      const first = focusableElements[0];
      const last = focusableElements[focusableElements.length - 1];

      if (e.shiftKey) {
        if (document.activeElement === first) {
          e.preventDefault();
          last.focus();
        }
      } else {
        if (document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    }
  }, [isSubmitting, webAuthnLoading, credentials.credentialCheckLoading, onCancel]);

  // ── WebAuthn biometric authentication ────────────────────────────────────

  const handleWebAuthnAuthenticate = useCallback(async () => {
    if (webAuthnLoading || isLockedOut) return;

    setWebAuthnLoading(true);
    setWebAuthnError(null);

    try {
      // Step 1: Get assertion options from the backend
      const optionsResponse = await apiClient.post("/auth/webauthn/login/options");
      const { optionsJson, challengeId } = optionsResponse.data;
      const options = JSON.parse(optionsJson);

      // Build the credential request options for navigator.credentials.get()
      const publicKeyOptions: PublicKeyCredentialRequestOptions = {
        challenge: base64urlToBuffer(options.challenge),
        timeout: 60000,
        rpId: options.rpId,
        allowCredentials: (options.allowCredentials || []).map((cred: any) => ({
          id: base64urlToBuffer(cred.id),
          type: cred.type,
          transports: cred.transports,
        })),
        userVerification: options.userVerification || "preferred",
      };

      // Step 2: Invoke the browser's WebAuthn API
      const assertion = await navigator.credentials.get({
        publicKey: publicKeyOptions,
      }) as PublicKeyCredential | null;

      if (!assertion) {
        setWebAuthnError(t("webAuthnCancelled"));
        setWebAuthnLoading(false);
        return;
      }

      // Step 3: Serialize the assertion response
      const assertionResponse = assertion.response as AuthenticatorAssertionResponse;
      const assertionJson = JSON.stringify({
        id: assertion.id,
        rawId: bufferToBase64url(assertion.rawId),
        type: assertion.type,
        response: {
          authenticatorData: bufferToBase64url(assertionResponse.authenticatorData),
          clientDataJSON: bufferToBase64url(assertionResponse.clientDataJSON),
          signature: bufferToBase64url(assertionResponse.signature),
          userHandle: assertionResponse.userHandle
            ? bufferToBase64url(assertionResponse.userHandle)
            : null,
        },
      });

      // Step 4: Submit to the re-authenticate endpoint
      const reAuthResponse = await apiClient.post("/auth/re-authenticate", {
        webAuthnChallengeId: challengeId,
        webAuthnAssertionJson: assertionJson,
        spaceId: spaceId || null,
      });

      if (reAuthResponse.data?.success) {
        onSuccess();
      }
    } catch (err: any) {
      // Determine the failure reason
      const status = err?.response?.status;

      if (status === 429) {
        const retryAfterSeconds = err?.response?.data?.retryAfterSeconds;
        if (retryAfterSeconds && typeof retryAfterSeconds === "number" && retryAfterSeconds > 0) {
          setIsLockedOut(true);
          setLockoutRemainingSeconds(retryAfterSeconds);
          const minutes = Math.ceil(retryAfterSeconds / 60);
          setWebAuthnError(t("lockedOut", { minutes }));
        } else {
          setWebAuthnError(t("rateLimited"));
        }
      } else if (status === 401) {
        setWebAuthnError(t("webAuthnNotRecognized"));
      } else if (err?.name === "NotAllowedError") {
        // User cancelled or timed out the WebAuthn ceremony
        setWebAuthnError(t("webAuthnCancelled"));
      } else if (err?.name === "AbortError") {
        setWebAuthnError(t("webAuthnTimedOut"));
      } else if (err?.name === "InvalidStateError") {
        setWebAuthnError(t("webAuthnNotRecognized"));
      } else if (err?.name === "SecurityError") {
        setWebAuthnError(t("webAuthnNotRecognized"));
      } else if (err?.message?.includes("timed out") || err?.message?.includes("timeout")) {
        setWebAuthnError(t("webAuthnTimedOut"));
      } else {
        setWebAuthnError(t("webAuthnNotRecognized"));
      }
    } finally {
      setWebAuthnLoading(false);
    }
  }, [webAuthnLoading, isLockedOut, spaceId, onSuccess, t]);

  // ── Password submission ──────────────────────────────────────────────────

  const handlePasswordSubmit = useCallback(async (e?: FormEvent) => {
    if (e) e.preventDefault();

    // Whitespace-only validation: show inline error and keep focus
    if (!password.trim()) {
      setPasswordValidationError(t("passwordRequired"));
      setTimeout(() => passwordInputRef.current?.focus(), 50);
      return;
    }

    if (isSubmitting || isLockedOut) return;

    setIsSubmitting(true);
    setError(null);
    setPasswordValidationError(null);

    try {
      const response = await apiClient.post("/auth/re-authenticate", {
        password,
        spaceId: spaceId || null,
      });

      if (response.data?.success) {
        onSuccess();
      }
    } catch (err: any) {
      const status = err?.response?.status;
      if (status === 429) {
        // Rate limited / lockout
        const retryAfterSeconds = err?.response?.data?.retryAfterSeconds;
        if (retryAfterSeconds && typeof retryAfterSeconds === "number" && retryAfterSeconds > 0) {
          setIsLockedOut(true);
          setLockoutRemainingSeconds(retryAfterSeconds);
          const minutes = Math.ceil(retryAfterSeconds / 60);
          setError(t("lockedOut", { minutes }));
        } else {
          setError(t("rateLimited"));
        }
        setPassword("");
      } else if (status === 401) {
        // Invalid credentials: clear password, show error, refocus
        setError(t("invalidCredentials"));
        setPassword("");
      } else {
        // Network error: retain password for retry
        setError(t("connectionProblem"));
      }
      // Re-focus password input after error
      setTimeout(() => passwordInputRef.current?.focus(), 50);
    } finally {
      setIsSubmitting(false);
    }
  }, [password, isSubmitting, isLockedOut, spaceId, onSuccess, t]);

  // ── Render ───────────────────────────────────────────────────────────────

  if (!open) return null;

  const { hasPassword, credentialCheckLoading } = credentials;
  const showWebAuthnOption = credentials.hasWebAuthnCredentials && webAuthnSupported;

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 60,
        background: "rgba(0,0,0,0.45)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1rem",
      }}
      onClick={(e) => {
        if (e.target === e.currentTarget && !isSubmitting && !webAuthnLoading && !credentialCheckLoading) onCancel();
      }}
      role="presentation"
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="reauth-dialog-title"
        aria-describedby="reauth-dialog-description"
        onKeyDown={handleKeyDown}
        style={{
          background: "white",
          borderRadius: 20,
          boxShadow: "0 20px 60px rgba(0,0,0,0.15)",
          width: "100%",
          maxWidth: 400,
          direction: isRtl ? "rtl" : "ltr",
          position: "relative",
          overflow: "hidden",
        }}
      >
        {/* Header */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "1.25rem 1.5rem",
            borderBottom: "1px solid #f1f5f9",
          }}
        >
          <h2
            id="reauth-dialog-title"
            style={{ fontSize: "1rem", fontWeight: 600, color: "#0f172a", margin: 0 }}
          >
            {t("title")}
          </h2>
          <button
            onClick={onCancel}
            disabled={isSubmitting || webAuthnLoading || credentialCheckLoading}
            aria-label={t("close")}
            style={{
              background: "none",
              border: "none",
              cursor: isSubmitting || webAuthnLoading || credentialCheckLoading ? "not-allowed" : "pointer",
              color: "#94a3b8",
              padding: 4,
              display: "flex",
              alignItems: "center",
              borderRadius: 8,
            }}
          >
            <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: "1.5rem" }}>
          {/* Explanatory message */}
          <p
            id="reauth-dialog-description"
            style={{
              fontSize: "0.875rem",
              color: "#64748b",
              margin: "0 0 1.25rem 0",
              lineHeight: 1.5,
            }}
          >
            {t("description")}
          </p>

          {/* Loading state while fetching credentials */}
          {credentialCheckLoading && (
            <div style={{ textAlign: "center", padding: "1rem 0", color: "#64748b", display: "flex", flexDirection: "column", alignItems: "center", gap: "0.5rem" }}>
              <svg
                width="24"
                height="24"
                viewBox="0 0 24 24"
                fill="none"
                style={{ animation: "spin 1s linear infinite" }}
              >
                <circle cx="12" cy="12" r="10" stroke="#e2e8f0" strokeWidth="3" />
                <path d="M12 2a10 10 0 0 1 10 10" stroke="#0ea5e9" strokeWidth="3" strokeLinecap="round" />
              </svg>
              <style>{`@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>
              <span>{t("loadingCredentials")}</span>
            </div>
          )}

          {/* No credentials configured */}
          {!credentialCheckLoading && !hasPassword && (
            <div
              style={{
                background: "#fef3c7",
                border: "1px solid #fde68a",
                borderRadius: 10,
                padding: "0.75rem 1rem",
                fontSize: "0.875rem",
                color: "#92400e",
              }}
            >
              {t("noCredentials")}
            </div>
          )}

          {/* ── WebAuthn biometric view ─────────────────────────────────── */}
          {!credentialCheckLoading && hasPassword && activeMethod === "webauthn" && showWebAuthnOption && (
            <div>
              {/* Biometric button — primary action */}
              <button
                ref={biometricButtonRef}
                type="button"
                onClick={handleWebAuthnAuthenticate}
                disabled={webAuthnLoading || isLockedOut}
                aria-label={t("webAuthnButton")}
                style={{
                  width: "100%",
                  padding: "1rem",
                  background: webAuthnLoading || isLockedOut
                    ? "#94a3b8"
                    : "linear-gradient(135deg, #0ea5e9, #0284c7)",
                  color: "white",
                  border: "none",
                  borderRadius: 12,
                  fontSize: "1rem",
                  fontWeight: 600,
                  cursor: webAuthnLoading || isLockedOut ? "not-allowed" : "pointer",
                  transition: "all 0.15s",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  gap: "0.625rem",
                }}
              >
                {webAuthnLoading ? (
                  <>
                    <svg
                      width="20"
                      height="20"
                      viewBox="0 0 24 24"
                      fill="none"
                      style={{ animation: "spin 1s linear infinite" }}
                    >
                      <circle cx="12" cy="12" r="10" stroke="rgba(255,255,255,0.3)" strokeWidth="3" />
                      <path d="M12 2a10 10 0 0 1 10 10" stroke="white" strokeWidth="3" strokeLinecap="round" />
                    </svg>
                    {t("webAuthnVerifying")}
                  </>
                ) : (
                  <>
                    {/* Fingerprint icon */}
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.75} strokeLinecap="round" strokeLinejoin="round">
                      <path d="M2 12C2 6.5 6.5 2 12 2a10 10 0 0 1 8 4" />
                      <path d="M5 19.5C5.5 18 6 15 6 12c0-3.5 2.5-6 6-6 3.5 0 6 2.5 6 6 0 4-1 8-4 10" />
                      <path d="M12 12v4c0 2.5-1 4-2.5 5.5" />
                      <path d="M8.5 16c0 2-1 3.5-2 4.5" />
                      <path d="M14 13.12c0 2.38 0 4.38-1.5 5.88" />
                      <path d="M18 12c0 4-1.5 8-4 10" />
                      <path d="M22 12c0 4-2 8-5 10" />
                    </svg>
                    {t("webAuthnButton")}
                  </>
                )}
              </button>

              {/* WebAuthn error message */}
              {webAuthnError && (
                <div
                  role="alert"
                  aria-live="assertive"
                  style={{
                    marginTop: "0.75rem",
                    background: "#fef2f2",
                    border: "1px solid #fecaca",
                    borderRadius: 10,
                    padding: "0.625rem 0.875rem",
                    fontSize: "0.8125rem",
                    color: "#dc2626",
                  }}
                >
                  {webAuthnError}
                </div>
              )}

              {/* "Use password instead" link */}
              <button
                type="button"
                onClick={() => {
                  setActiveMethod("password");
                  setWebAuthnError(null);
                }}
                disabled={webAuthnLoading}
                style={{
                  display: "block",
                  width: "100%",
                  marginTop: "1rem",
                  padding: "0.5rem",
                  background: "none",
                  border: "none",
                  color: "#0ea5e9",
                  fontSize: "0.875rem",
                  fontWeight: 500,
                  cursor: webAuthnLoading ? "not-allowed" : "pointer",
                  textDecoration: "underline",
                  textUnderlineOffset: "2px",
                  textAlign: "center",
                }}
              >
                {t("usePasswordInstead")}
              </button>
            </div>
          )}

          {/* ── Password form view ──────────────────────────────────────── */}
          {!credentialCheckLoading && hasPassword && activeMethod === "password" && (
            <div>
              <form onSubmit={handlePasswordSubmit} noValidate autoComplete="off">
                <div style={{ marginBottom: "1rem" }}>
                  <label
                    htmlFor="reauth-password"
                    style={{
                      display: "block",
                      fontSize: "0.875rem",
                      fontWeight: 500,
                      color: "#374151",
                      marginBottom: "0.375rem",
                    }}
                  >
                    {t("passwordLabel")}
                  </label>
                  <div style={{ position: "relative" }}>
                    <input
                      ref={passwordInputRef}
                      id="reauth-password"
                      type={showPassword ? "text" : "password"}
                      autoComplete="new-password"
                      name="reauth-verify"
                      readOnly={isReadonly}
                      onFocus={() => setIsReadonly(false)}
                      value={password}
                      onChange={(e) => {
                        setPassword(e.target.value);
                        if (passwordValidationError) setPasswordValidationError(null);
                      }}
                      disabled={isSubmitting || isLockedOut}
                      aria-required="true"
                      aria-invalid={error || passwordValidationError ? "true" : undefined}
                      aria-describedby={passwordValidationError ? "reauth-validation-error" : error ? "reauth-error" : undefined}
                      placeholder={t("passwordPlaceholder")}
                      style={{
                        width: "100%",
                        padding: "0.75rem 2.5rem 0.75rem 1rem",
                        border: `1px solid ${passwordValidationError ? "#fca5a5" : "#e2e8f0"}`,
                        borderRadius: 10,
                        fontSize: "0.9375rem",
                        outline: "none",
                        transition: "border-color 0.15s",
                        boxSizing: "border-box",
                        direction: "ltr",
                        textAlign: "left",
                      }}
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      style={{
                        position: "absolute",
                        right: "0.75rem",
                        top: "50%",
                        transform: "translateY(-50%)",
                        background: "none",
                        border: "none",
                        cursor: "pointer",
                        color: "#94a3b8",
                        padding: 0,
                        display: "flex",
                        alignItems: "center",
                      }}
                      aria-label={showPassword ? "Hide password" : "Show password"}
                      tabIndex={-1}
                    >
                      {showPassword ? (
                        <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                        </svg>
                      ) : (
                        <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                          <path strokeLinecap="round" strokeLinejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                        </svg>
                      )}
                    </button>
                  </div>
                  {/* Inline validation error */}
                  {passwordValidationError && (
                    <div
                      id="reauth-validation-error"
                      role="alert"
                      aria-live="assertive"
                      style={{
                        marginTop: "0.375rem",
                        fontSize: "0.8125rem",
                        color: "#dc2626",
                      }}
                    >
                      {passwordValidationError}
                    </div>
                  )}
                </div>

                <button
                  ref={submitButtonRef}
                  type="submit"
                  disabled={isSubmitting || !password.trim() || isLockedOut}
                  style={{
                    width: "100%",
                    padding: "0.75rem",
                    background: isSubmitting || !password.trim() || isLockedOut
                      ? "#94a3b8"
                      : "linear-gradient(135deg, #0ea5e9, #0284c7)",
                    color: "white",
                    border: "none",
                    borderRadius: 10,
                    fontSize: "0.9375rem",
                    fontWeight: 600,
                    cursor: isSubmitting || !password.trim() || isLockedOut ? "not-allowed" : "pointer",
                    transition: "all 0.15s",
                  }}
                >
                  {isSubmitting ? t("verifying") : t("confirm")}
                </button>
              </form>

              {/* Error message */}
              {error && (
                <div
                  id="reauth-error"
                  role="alert"
                  aria-live="assertive"
                  style={{
                    marginTop: "1rem",
                    background: "#fef2f2",
                    border: "1px solid #fecaca",
                    borderRadius: 10,
                    padding: "0.625rem 0.875rem",
                    fontSize: "0.8125rem",
                    color: "#dc2626",
                  }}
                >
                  {error}
                </div>
              )}

              {/* "Use biometric instead" link — only if WebAuthn is available */}
              {showWebAuthnOption && (
                <button
                  type="button"
                  onClick={() => {
                    setActiveMethod("webauthn");
                    setError(null);
                    setPassword("");
                  }}
                  disabled={isSubmitting}
                  style={{
                    display: "block",
                    width: "100%",
                    marginTop: "0.75rem",
                    padding: "0.5rem",
                    background: "none",
                    border: "none",
                    color: "#0ea5e9",
                    fontSize: "0.875rem",
                    fontWeight: 500,
                    cursor: isSubmitting ? "not-allowed" : "pointer",
                    textDecoration: "underline",
                    textUnderlineOffset: "2px",
                    textAlign: "center",
                  }}
                >
                  {t("useBiometricInstead")}
                </button>
              )}
            </div>
          )}

          {/* Cancel button */}
          <button
            type="button"
            onClick={onCancel}
            disabled={isSubmitting || webAuthnLoading || credentialCheckLoading}
            style={{
              width: "100%",
              marginTop: "1rem",
              padding: "0.625rem",
              background: "none",
              color: "#64748b",
              border: "1px solid #e2e8f0",
              borderRadius: 10,
              fontSize: "0.875rem",
              fontWeight: 500,
              cursor: isSubmitting || webAuthnLoading || credentialCheckLoading ? "not-allowed" : "pointer",
              transition: "all 0.15s",
            }}
          >
            {t("cancel")}
          </button>
        </div>
      </div>
    </div>
  );
}
