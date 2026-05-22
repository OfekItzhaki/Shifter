"use client";

import { useEffect, useRef, useState, useCallback, FormEvent, KeyboardEvent } from "react";
import { useTranslations, useLocale } from "next-intl";
import { apiClient } from "@/lib/api/client";

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
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function ReAuthDialog({ open, onSuccess, onCancel, mode, spaceId }: ReAuthDialogProps) {
  const t = useTranslations("reAuth");
  const locale = useLocale();
  const isRtl = locale === "he";

  // Credential availability
  const [credentials, setCredentials] = useState<CredentialState>({
    hasPassword: false,
    loading: true,
  });

  // Form state
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Refs for focus management
  const dialogRef = useRef<HTMLDivElement>(null);
  const submitButtonRef = useRef<HTMLButtonElement>(null);
  const passwordInputRef = useRef<HTMLInputElement>(null);

  // ── Fetch credential availability ────────────────────────────────────────

  useEffect(() => {
    if (!open) return;
    // All registered users have a password — no need to check WebAuthn
    setCredentials({ hasPassword: true, loading: false });
  }, [open]);

  // ── Reset state when dialog opens/closes ─────────────────────────────────

  useEffect(() => {
    if (open) {
      setPassword("");
      setError(null);
      setIsSubmitting(false);
    } else {
      setCredentials({ hasPassword: false, loading: true });
    }
  }, [open]);

  // ── Focus management ─────────────────────────────────────────────────────

  useEffect(() => {
    if (!open || credentials.loading) return;

    const timer = setTimeout(() => {
      if (credentials.hasPassword && passwordInputRef.current) {
        passwordInputRef.current.focus();
      } else if (submitButtonRef.current) {
        submitButtonRef.current.focus();
      }
    }, 50);

    return () => clearTimeout(timer);
  }, [open, credentials.loading, credentials.hasPassword]);

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

  // ── Render ───────────────────────────────────────────────────────────────

  if (!open) return null;

  const { hasPassword, loading: credentialsLoading } = credentials;

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
          {!credentialsLoading && !hasPassword && (
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

          {/* Password form */}
          {!credentialsLoading && hasPassword && (
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
                    <div style={{ position: "relative" }}>
                      <input
                        ref={passwordInputRef}
                        id="reauth-password"
                        type={showPassword ? "text" : "password"}
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
                          padding: "0.75rem 2.5rem 0.75rem 1rem",
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
                        : "linear-gradient(135deg, #0ea5e9, #0284c7)",
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


