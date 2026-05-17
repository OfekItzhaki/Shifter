"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { useAdminSessionStore, ExitContext } from "@/lib/store/adminSessionStore";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { apiClient } from "@/lib/api/client";

// ── Types ─────────────────────────────────────────────────────────────────────

interface SessionExitHandlerState {
  /** Whether the timeout toast should be visible. */
  toastVisible: boolean;
  /** Dismiss the toast. */
  dismissToast: () => void;
}

// ── Hook ──────────────────────────────────────────────────────────────────────
/**
 * Watches for timeout/prompt_no exits from the admin session store and
 * performs the required side effects:
 * - Clears authStore admin mode state
 * - Redirects to the appropriate page
 * - Shows a toast notification
 * - Sends POST /auth/session-timeout-event to backend
 *
 * Requirements: 7.1, 7.2, 7.3, 7.4, 7.5
 */
export function useSessionExitHandler(): SessionExitHandlerState {
  const router = useRouter();
  const lastExitContext = useAdminSessionStore((s) => s.lastExitContext);
  const clearExitContext = useAdminSessionStore((s) => s.clearExitContext);
  const exitAdminMode = useAuthStore((s) => s.exitAdminMode);
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);

  const [toastVisible, setToastVisible] = useState(false);

  // Track the last processed exit timestamp to avoid re-processing
  const lastProcessedRef = useRef<number>(0);

  useEffect(() => {
    if (!lastExitContext) return;
    if (lastExitContext.timestamp <= lastProcessedRef.current) return;

    const { reason, mode, groupId } = lastExitContext;

    // Only handle timeout-related exits
    if (reason !== "timeout" && reason !== "prompt_no") {
      clearExitContext();
      return;
    }

    // Mark as processed
    lastProcessedRef.current = lastExitContext.timestamp;

    // 1. Clear authStore admin mode state (Req 7.1)
    exitAdminMode();

    // 2. Redirect based on mode (Req 7.2, 7.3)
    if (mode === "management" && groupId) {
      // Redirect to group page in standard (non-admin) view
      router.push(`/groups/${groupId}`);
    } else if (mode === "platform") {
      // Redirect to application home page
      router.push("/");
    } else {
      // Fallback: go home
      router.push("/");
    }

    // 3. Show toast notification (Req 7.4)
    setToastVisible(true);

    // 4. Send timeout event to backend (Req 7.5) — fire and forget
    apiClient
      .post("/auth/session-timeout-event", {
        spaceId: mode === "management" ? currentSpaceId : null,
        mode,
      })
      .catch(() => {
        // Best effort — don't block the user experience on failure
      });

    // Clear the exit context from the store
    clearExitContext();
  }, [lastExitContext, clearExitContext, exitAdminMode, router, currentSpaceId]);

  const dismissToast = useCallback(() => {
    setToastVisible(false);
  }, []);

  return { toastVisible, dismissToast };
}
