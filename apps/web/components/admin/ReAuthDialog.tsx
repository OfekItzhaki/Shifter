"use client";

import { useEffect, useRef, useState, useCallback, FormEvent, KeyboardEvent } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "@/lib/api/client";
import { isWebAuthnSupported, listCredentials } from "@/lib/webauthn";

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
  hasWebAuthn: boolean;
  loading: boolean;
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function ReAuthDialog({ open, onSuccess, onCancel, mode, spaceId }: ReAuthDialogProps) {
  const t = useTranslations("reAuth");

  // Credential availability
  const [credentials, setCredentials] = useState<CredentialState>({
    hasPassword: false,
    hasWebAuthn: false,
    loading: true,
  });

  // Form state
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Refs for focus management
  const dialogRef = useRef<HTMLDivElement>(null);
  const submitButtonRef = useRef<HTMLButtonElement>(null);
  const passwordInputRef = useRef<HTMLInputElement>(null);
  const firstFocusableRef = useRef<HTMLElement | null>(null);

  // ── Fetch credential availability ────────────────────────────────────────

  useEffect(() => {
    if (!open) return;

    let cancelled = false;

    async function fetchCredentials() {
      try {
        // All registered users have a password
        let hasWebAuthn = false;

        if (isWebAuthnSupported()) {
          try {
            const creds = await listCredentials();
            hasWebAuthn = creds.length > 0;
          } catch {
            // If we can't fetch WebAuthn credentials, just don't show the option
            hasWebAuthn = false;
          }
        }

        if (!cancelled) {
          setCredentials({ hasPassword: true, hasWebAuthn, loading: false });
        }
      } catch {
        if (!cancelled) {
          setCredentials({ hasPassword: true, hasWebAuthn: false, loading: false });
        }
      }
    }

    fetchCredentials();
    return () => { cancelled = true; };
  }, [open]);

  // ── Reset state when dialog opens/closes ─────────────────────────────────

  useEffect(() => {
    if (open) {
      setPassword("");
      setError(null);
      setIsSubmitting(false);
    } else {
      setCredentials({ hasPassword: false, hasWebAuthn: false, loading: true });
    }
  }, [open]);

  // ── Focus management ─────────────────────────────────────────────────────

  useEffect(() => {
    if (!open || credentials.loading) return;

    // Focus the submit button initially (or password input if available)
    const timer = setTimeout(() => {
      if (submitButtonRef.current) {
        submitButtonRef.current.focus();
      } else if (passwordInputRef.current) {
        passwordInputRef.current.focus();
      }
    }, 50);

    return () => clearTimeout(timer);
  }, [open, credentials.loading]);

  // ── Focus trap ───────────────────────────────────────────────────────────

  const handleKeyDown = useCallback((e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "Escape") {
      e.preventDefault();
      if (!isSubmitting) onCancel();
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
  }, [isSubmitting, onCancel]);

  // ── Password submission ──────────────────────────────────────────────────

  const handlePasswordSubmit = useCallback(async (e?: FormEvent) => {
    if (e) e.preventDefault();
    if (!password.trim() || isSubmitting) return;

    setIsSubmitting(true);
    setError(null);

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
        setError(t("rateLimited"));
      } else if (status === 401) {
        setError(t("authFailed"));
      } else {
        setError(t("networkError"));
      }
      setPassword("");
      // Re-focus password input after error
      setTimeout(() => passwordInputRef.current?.focus(), 50);
    } finally {
      setIsSubmitting(false);
    }
  }, [password, isSubmitting, spaceId, onSuccess, t]);

  // ── WebAuthn submission ──────────────────────────────────────────────────

  const handleWebAuthnSubmit = useCallback(async () => {
    if (isSubmitting) return;

    setIsSubmitting(true);
    setError(null);

    try {
      // Step 1: Get authentication options from server
      const { data: optionsData } = await apiClient.post("/auth/webauthn/login/options");
      const options = JSON.parse(optionsData.optionsJson);
      const challengeId: string = optionsData.challengeId;

      // Convert base64url fields to ArrayBuffer
      options.challenge = base64urlToArrayBuffer(options.challenge);
      if (options.allowCredentials) {
        options.allowCredentials = options.allowCredentials.map(
          (cred: { id: string; type: string; transports?: string[] }) => ({
            ...cred,
            id: base64urlToArrayBuffer(cred.id),
          })
        );
      }

      // Step 2: Call navigator.credentials.get()
      let credential: PublicKeyCredential;
      try {
        const result = await navigator.credentials.get({ publicKey: options });
        if (!result) throw new Error("USER_CANCELLED");
        credential = result as PublicKeyCredential;
      } catch (err: any) {
        if (err.name === "NotAllowedError" || err.message === "USER_CANCELLED") {
          setError(t("webAuthnCancelled"));
          setIsSubmitting(false);
          return;
        }
        throw err;
      }

      // Step 3: Send assertion to re-authenticate endpoint
      const assertionResponse = credential.response as AuthenticatorAssertionResponse;
      const assertionResponseJson = JSON.stringify({
        id: credential.id,
        rawId: arrayBufferToBase64url(credential.rawId),
        type: credential.type,
        response: {
          authenticatorData: arrayBufferToBase64url(assertionResponse.authenticatorData),
          clientDataJSON: arrayBufferToBase64url(assertionResponse.clientDataJSON),
          signature: arrayBufferToBase64url(assertionResponse.signature),
          userHandle: assertionResponse.userHandle
            ? arrayBufferToBase64url(assertionResponse.userHandle)
            : null,
        },
      });

      const response = await apiClient.post("/auth/re-authenticate", {
        webAuthnChallengeId: challengeId,
        webAuthnAssertionJson: assertionResponseJson,
        spaceId: spaceId || null,
      });

      if (response.data?.success) {
        onSuccess();
      }
    } catch (err: any) {
      const status = err?.response?.status;
      if (status === 429) {
        setError(t("rateLimited"));
      } else if (status === 401) {
        setError(t("authFailed"));
      } else {
        setError(t("networkError"));
      }
    } finally {
      setIsSubmitting(false);
    }
  }, [isSubmitting, spaceId, onSuccess, t]);

  // ── Render ───────────────────────────────────────────────────────────────

  if (!open) return null;

  const { hasPassword, hasWebAuthn, loading: credentialsLoading } = credentials;

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
        if (e.target === e.currentTarget && !isSubmitting) onCancel();
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
          direction: "rtl",
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
            disabled={isSubmitting}
            aria-label={t("close")}
            style={{
              background: "none",
              border: "none",
              cursor: isSubmitting ? "not-allowed" : "pointer",
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
          {credentialsLoading && (
            <div style={{ textAlign: "center", padding: "1rem 0", color: "#64748b" }}>
              {t("loadingCredentials")}
            </div>
          )}

          {/* No credentials configured */}
          {!credentialsLoading && !hasPassword && !hasWebAuthn && (
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

          {/* Credential forms */}
          {!credentialsLoading && (hasPassword || hasWebAuthn) && (
            <>
              {/* Password form */}
              {hasPassword && (
                <form onSubmit={handlePasswordSubmit} noValidate>
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
                    <input
                      ref={passwordInputRef}
                      id="reauth-password"
                      type="password"
                      autoComplete="current-password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      disabled={isSubmitting}
                      aria-required="true"
                      aria-invalid={error ? "true" : undefined}
                      aria-describedby={error ? "reauth-error" : undefined}
                      placeholder={t("passwordPlaceholder")}
                      style={{
                        width: "100%",
                        padding: "0.75rem 1rem",
                        border: "1px solid #e2e8f0",
                        borderRadius: 10,
                        fontSize: "0.9375rem",
                        outline: "none",
                        transition: "border-color 0.15s",
                        boxSizing: "border-box",
                        direction: "ltr",
                        textAlign: "left",
                      }}
                    />
                  </div>

                  <button
                    ref={submitButtonRef}
                    type="submit"
                    disabled={isSubmitting || !password.trim()}
                    style={{
                      width: "100%",
                      padding: "0.75rem",
                      background: isSubmitting || !password.trim()
                        ? "#94a3b8"
                        : "linear-gradient(135deg, #3b82f6, #2563eb)",
                      color: "white",
                      border: "none",
                      borderRadius: 10,
                      fontSize: "0.9375rem",
                      fontWeight: 600,
                      cursor: isSubmitting || !password.trim() ? "not-allowed" : "pointer",
                      transition: "all 0.15s",
                    }}
                  >
                    {isSubmitting ? t("verifying") : t("confirm")}
                  </button>
                </form>
              )}

              {/* Divider between methods */}
              {hasPassword && hasWebAuthn && (
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    margin: "1rem 0",
                    gap: "0.75rem",
                  }}
                >
                  <div style={{ flex: 1, height: 1, background: "#e2e8f0" }} />
                  <span style={{ fontSize: "0.8125rem", color: "#94a3b8" }}>{t("or")}</span>
                  <div style={{ flex: 1, height: 1, background: "#e2e8f0" }} />
                </div>
              )}

              {/* WebAuthn button */}
              {hasWebAuthn && (
                <button
                  type="button"
                  onClick={handleWebAuthnSubmit}
                  disabled={isSubmitting}
                  aria-label={t("webAuthnLabel")}
                  style={{
                    width: "100%",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: "0.625rem",
                    background: isSubmitting
                      ? "linear-gradient(135deg, #a78bfa, #818cf8)"
                      : "linear-gradient(135deg, #7c3aed, #4f46e5)",
                    color: "white",
                    border: "none",
                    borderRadius: 10,
                    padding: "0.75rem",
                    fontSize: "0.9375rem",
                    fontWeight: 600,
                    cursor: isSubmitting ? "not-allowed" : "pointer",
                    transition: "all 0.15s",
                    boxShadow: "0 2px 8px rgba(79, 70, 229, 0.3)",
                  }}
                >
                  {/* Fingerprint icon */}
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M2 12C2 6.5 6.5 2 12 2a10 10 0 0 1 8 4" />
                    <path d="M5 19.5C5.5 18 6 15 6 12c0-3.5 2.5-6 6-6 3.5 0 6 2.5 6 6 0 1.5-.5 3-1 4" />
                    <path d="M9 12c0-1.5 1.5-3 3-3s3 1.5 3 3-1 4-2 6" />
                    <path d="M12 12v4" />
                    <path d="M2 16c1 2 2.5 3.5 4.5 4.5" />
                    <path d="M15 17c1 1.5 2 3 2.5 4.5" />
                    <path d="M19.5 8c.5 1 .5 2 .5 4 0 2-.5 4-1 6" />
                  </svg>
                  {isSubmitting ? t("verifying") : t("webAuthnButton")}
                </button>
              )}

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
            </>
          )}

          {/* Cancel button */}
          <button
            type="button"
            onClick={onCancel}
            disabled={isSubmitting}
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
              cursor: isSubmitting ? "not-allowed" : "pointer",
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

// ── Base64url helpers (duplicated from webauthn.ts to avoid coupling) ────────

function base64urlToArrayBuffer(base64url: string): ArrayBuffer {
  let base64 = base64url.replace(/-/g, "+").replace(/_/g, "/");
  while (base64.length % 4 !== 0) base64 += "=";
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

function arrayBufferToBase64url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
