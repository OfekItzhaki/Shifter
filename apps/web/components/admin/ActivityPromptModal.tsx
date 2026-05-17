"use client";

import { useEffect, useRef, useCallback, useState, KeyboardEvent } from "react";
import { useTranslations } from "next-intl";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface ActivityPromptModalProps {
  open: boolean;
  countdownSeconds: number;
  onYes: () => void;
  onNo: () => void;
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function ActivityPromptModal({
  open,
  countdownSeconds,
  onYes,
  onNo,
}: ActivityPromptModalProps) {
  const t = useTranslations("activityPrompt");
  const [remaining, setRemaining] = useState(countdownSeconds);
  const dialogRef = useRef<HTMLDivElement>(null);
  const yesButtonRef = useRef<HTMLButtonElement>(null);

  // ── Sync remaining when countdownSeconds prop changes ────────────────────

  useEffect(() => {
    if (open) {
      setRemaining(countdownSeconds);
    }
  }, [open, countdownSeconds]);

  // ── Countdown timer ──────────────────────────────────────────────────────

  useEffect(() => {
    if (!open) return;

    const interval = setInterval(() => {
      setRemaining((prev) => {
        if (prev <= 1) {
          clearInterval(interval);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(interval);
  }, [open]);

  // ── Auto-trigger onNo when countdown reaches zero ────────────────────────

  useEffect(() => {
    if (open && remaining === 0) {
      onNo();
    }
  }, [open, remaining, onNo]);

  // ── Focus management: "Yes" button receives initial focus ────────────────

  useEffect(() => {
    if (!open) return;

    const timer = setTimeout(() => {
      yesButtonRef.current?.focus();
    }, 50);

    return () => clearTimeout(timer);
  }, [open]);

  // ── Focus trap ───────────────────────────────────────────────────────────

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLDivElement>) => {
      if (e.key === "Tab") {
        const dialog = dialogRef.current;
        if (!dialog) return;

        const focusableElements = dialog.querySelectorAll<HTMLElement>(
          'button:not([disabled]), [tabindex]:not([tabindex="-1"])'
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
    },
    []
  );

  // ── Render ───────────────────────────────────────────────────────────────

  if (!open) return null;

  const minutes = Math.floor(remaining / 60);
  const seconds = remaining % 60;
  const timeDisplay = `${minutes}:${seconds.toString().padStart(2, "0")}`;

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
      role="presentation"
    >
      <div
        ref={dialogRef}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="activity-prompt-title"
        aria-describedby="activity-prompt-countdown"
        onKeyDown={handleKeyDown}
        style={{
          background: "white",
          borderRadius: 20,
          boxShadow: "0 20px 60px rgba(0,0,0,0.15)",
          width: "100%",
          maxWidth: 380,
          direction: "rtl",
          position: "relative",
          overflow: "hidden",
        }}
      >
        {/* Body */}
        <div style={{ padding: "2rem 1.5rem", textAlign: "center" }}>
          {/* Title / Message */}
          <h2
            id="activity-prompt-title"
            style={{
              fontSize: "1.125rem",
              fontWeight: 600,
              color: "#0f172a",
              margin: "0 0 0.75rem 0",
            }}
          >
            {t("title")}
          </h2>

          <p
            style={{
              fontSize: "0.875rem",
              color: "#64748b",
              margin: "0 0 1.25rem 0",
              lineHeight: 1.5,
            }}
          >
            {t("description")}
          </p>

          {/* Countdown timer */}
          <div
            id="activity-prompt-countdown"
            aria-live="polite"
            aria-atomic="true"
            style={{
              fontSize: "2rem",
              fontWeight: 700,
              color: remaining <= 10 ? "#dc2626" : "#1e40af",
              fontVariantNumeric: "tabular-nums",
              margin: "0 0 1.5rem 0",
              transition: "color 0.3s",
            }}
          >
            {timeDisplay}
          </div>

          {/* Buttons */}
          <div
            style={{
              display: "flex",
              gap: "0.75rem",
              justifyContent: "center",
            }}
          >
            <button
              ref={yesButtonRef}
              type="button"
              onClick={onYes}
              style={{
                flex: 1,
                padding: "0.75rem 1.5rem",
                background: "linear-gradient(135deg, #3b82f6, #2563eb)",
                color: "white",
                border: "none",
                borderRadius: 10,
                fontSize: "0.9375rem",
                fontWeight: 600,
                cursor: "pointer",
                transition: "all 0.15s",
              }}
            >
              {t("yes")}
            </button>

            <button
              type="button"
              onClick={onNo}
              style={{
                flex: 1,
                padding: "0.75rem 1.5rem",
                background: "none",
                color: "#64748b",
                border: "1px solid #e2e8f0",
                borderRadius: 10,
                fontSize: "0.9375rem",
                fontWeight: 500,
                cursor: "pointer",
                transition: "all 0.15s",
              }}
            >
              {t("no")}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
