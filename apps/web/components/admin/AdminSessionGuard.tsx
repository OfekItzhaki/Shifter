"use client";

import { useCallback } from "react";
import { useAdminSessionStore } from "@/lib/store/adminSessionStore";
import { useAdminSessionWiring } from "@/lib/hooks/useAdminSessionWiring";
import { useSessionExitHandler } from "@/lib/hooks/useSessionExitHandler";
import ActivityPromptModal from "./ActivityPromptModal";
import SessionTimeoutToast from "./SessionTimeoutToast";

// ── Constants ─────────────────────────────────────────────────────────────────

const PROMPT_COUNTDOWN_SECONDS = 60;
const TIMEOUT_TOAST_MESSAGE = "Session expired due to inactivity";

// ── Component ─────────────────────────────────────────────────────────────────
// Renders the ActivityPromptModal globally when the admin session store
// signals that the inactivity prompt should be visible.
// Also wires the InactivityTimer, MultiTabSync, and timeout exit behavior to the store.
// Requirements: 5.3, 6.1, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 7.5, 11.1, 11.2, 11.3

export default function AdminSessionGuard() {
  const isPromptVisible = useAdminSessionStore((s) => s.isPromptVisible);
  const dismissPrompt = useAdminSessionStore((s) => s.dismissPrompt);

  // Wire up the inactivity timer, API call listener, and multi-tab sync
  useAdminSessionWiring();

  // Wire timeout exit behavior: redirect, toast, and backend event
  const { toastVisible, dismissToast } = useSessionExitHandler();

  const handleYes = useCallback(() => {
    dismissPrompt("yes");
  }, [dismissPrompt]);

  const handleNo = useCallback(() => {
    dismissPrompt("no");
  }, [dismissPrompt]);

  return (
    <>
      <ActivityPromptModal
        open={isPromptVisible}
        countdownSeconds={PROMPT_COUNTDOWN_SECONDS}
        onYes={handleYes}
        onNo={handleNo}
      />
      <SessionTimeoutToast
        visible={toastVisible}
        message={TIMEOUT_TOAST_MESSAGE}
        durationMs={6000}
        onDismiss={dismissToast}
      />
    </>
  );
}
