"use client";

import { useState, useEffect, useCallback } from "react";
import { useTranslations, useLocale } from "next-intl";
import { triggerRegeneration } from "@/lib/api/schedule";

export interface RegenerateConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (runId: string) => void;
  spaceId: string;
  groupId: string;
}

export default function RegenerateConfirmDialog({
  open,
  onClose,
  onSuccess,
  spaceId,
  groupId,
}: RegenerateConfirmDialogProps) {
  const t = useTranslations("scheduleRegeneration");
  const locale = useLocale();
  const isRtl = locale === "he";

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset state when dialog opens
  useEffect(() => {
    if (open) {
      setIsSubmitting(false);
      setError(null);
    }
  }, [open]);

  // Close on Escape key
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !isSubmitting) onClose();
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open, isSubmitting, onClose]);

  const handleConfirm = useCallback(async () => {
    if (isSubmitting) return;

    setIsSubmitting(true);
    setError(null);

    try {
      const { runId } = await triggerRegeneration(spaceId, groupId);
      onClose();
      onSuccess(runId);
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } }).response?.status;

      if (status === 402) {
        setError(t("errorSubscriptionExpired"));
      } else if (status === 409) {
        setError(t("errorAlreadyInProgress"));
      } else if (status === 403) {
        setError(t("errorPermissionDenied"));
      } else {
        setError(t("errorGeneric"));
      }
    } finally {
      setIsSubmitting(false);
    }
  }, [isSubmitting, spaceId, groupId, onClose, onSuccess, t]);

  if (!open) return null;

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
        if (e.target === e.currentTarget && !isSubmitting) onClose();
      }}
      role="presentation"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="regenerate-dialog-title"
        aria-describedby="regenerate-dialog-description"
        style={{
          background: "white",
          borderRadius: 20,
          boxShadow: "0 20px 60px rgba(0,0,0,0.15)",
          width: "100%",
          maxWidth: 420,
          direction: isRtl ? "rtl" : "ltr",
          position: "relative",
          overflow: "hidden",
        }}
        onClick={(e) => e.stopPropagation()}
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
            id="regenerate-dialog-title"
            style={{ fontSize: "1rem", fontWeight: 600, color: "#0f172a", margin: 0 }}
          >
            {t("title")}
          </h2>
          <button
            onClick={onClose}
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
          <p
            id="regenerate-dialog-description"
            style={{
              fontSize: "0.875rem",
              color: "#64748b",
              margin: "0 0 1.25rem 0",
              lineHeight: 1.6,
            }}
          >
            {t("description")}
          </p>

          {/* Error message */}
          {error && (
            <div
              role="alert"
              aria-live="assertive"
              style={{
                marginBottom: "1rem",
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

          {/* Action buttons */}
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              style={{
                padding: "0.625rem 1.25rem",
                background: "none",
                border: "1px solid #e2e8f0",
                borderRadius: 10,
                fontSize: "0.875rem",
                fontWeight: 500,
                color: "#64748b",
                cursor: isSubmitting ? "not-allowed" : "pointer",
                transition: "all 0.15s",
              }}
            >
              {t("cancel")}
            </button>
            <button
              type="button"
              onClick={handleConfirm}
              disabled={isSubmitting}
              style={{
                padding: "0.625rem 1.25rem",
                background: isSubmitting ? "#94a3b8" : "linear-gradient(135deg, #0ea5e9, #0284c7)",
                color: "white",
                border: "none",
                borderRadius: 10,
                fontSize: "0.875rem",
                fontWeight: 600,
                cursor: isSubmitting ? "not-allowed" : "pointer",
                transition: "all 0.15s",
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
              }}
            >
              {isSubmitting && (
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  style={{ animation: "spin 1s linear infinite" }}
                >
                  <circle cx="12" cy="12" r="10" stroke="rgba(255,255,255,0.3)" strokeWidth="3" />
                  <path d="M12 2a10 10 0 0 1 10 10" stroke="white" strokeWidth="3" strokeLinecap="round" />
                </svg>
              )}
              {isSubmitting ? t("confirming") : t("confirm")}
            </button>
          </div>
        </div>
      </div>

      <style>{`@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}
