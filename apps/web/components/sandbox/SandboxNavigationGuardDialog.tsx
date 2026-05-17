"use client";

import { useTranslations } from "next-intl";

interface SandboxNavigationGuardDialogProps {
  open: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * In-app confirmation dialog shown when the admin tries to navigate away
 * from the sandbox without publishing or discarding changes.
 *
 * Warns that unsaved simulation changes will be lost.
 *
 * Requirements: 7.3
 */
export default function SandboxNavigationGuardDialog({
  open,
  onConfirm,
  onCancel,
}: SandboxNavigationGuardDialogProps) {
  const t = useTranslations("sandbox.navigationGuard");

  if (!open) return null;

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 100,
        background: "rgba(0, 0, 0, 0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1rem",
      }}
      onClick={onCancel}
      role="dialog"
      aria-modal="true"
      aria-labelledby="sandbox-nav-guard-title"
    >
      <div
        style={{
          background: "white",
          borderRadius: 16,
          boxShadow: "0 20px 60px rgba(0, 0, 0, 0.2)",
          maxWidth: 400,
          width: "100%",
          padding: "1.5rem",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Warning icon */}
        <div className="flex items-center gap-3 mb-3">
          <div
            className="flex items-center justify-center w-10 h-10 rounded-full bg-amber-100"
            aria-hidden="true"
          >
            <svg
              width="20"
              height="20"
              fill="none"
              viewBox="0 0 24 24"
              stroke="#d97706"
              strokeWidth={2}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4.5c-.77-.833-2.694-.833-3.464 0L3.34 16.5c-.77.833.192 2.5 1.732 2.5z"
              />
            </svg>
          </div>
          <h3
            id="sandbox-nav-guard-title"
            className="text-base font-semibold text-slate-900"
            style={{ margin: 0 }}
          >
            {t("title")}
          </h3>
        </div>

        {/* Message */}
        <p className="text-sm text-slate-600 mb-5" style={{ margin: "0 0 1.25rem 0" }}>
          {t("message")}
        </p>

        {/* Actions */}
        <div className="flex gap-2 justify-end">
          <button
            onClick={onCancel}
            className="px-4 py-2 text-sm font-medium text-slate-700 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 transition-colors"
            autoFocus
          >
            {t("cancel")}
          </button>
          <button
            onClick={onConfirm}
            className="px-4 py-2 text-sm font-medium text-white bg-red-500 border border-red-500 rounded-xl hover:bg-red-600 transition-colors"
          >
            {t("confirm")}
          </button>
        </div>
      </div>
    </div>
  );
}
