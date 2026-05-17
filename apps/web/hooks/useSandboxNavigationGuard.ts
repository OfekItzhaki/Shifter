"use client";

import { useEffect, useCallback, useState } from "react";
import { useSandboxStore } from "@/lib/store/sandboxStore";

/**
 * Navigation guard hook for the simulation sandbox.
 *
 * When the sandbox is active (isActive === true):
 * 1. Adds a `beforeunload` event listener so the browser shows its native
 *    "are you sure you want to leave?" dialog on tab close/refresh.
 * 2. Intercepts in-app navigation by monkey-patching `history.pushState`
 *    and listening to `popstate` events. Shows a custom confirmation dialog
 *    via the returned state.
 *
 * When the sandbox becomes inactive or the component unmounts, all listeners
 * are cleaned up automatically.
 *
 * Requirements: 7.3, 7.4
 */
export function useSandboxNavigationGuard() {
  const isActive = useSandboxStore((s) => s.isActive);
  const [showLeaveDialog, setShowLeaveDialog] = useState(false);
  const [pendingNavigation, setPendingNavigation] = useState<string | null>(null);

  // Confirm leaving — execute the pending navigation
  const confirmLeave = useCallback(() => {
    setShowLeaveDialog(false);
    const url = pendingNavigation;
    setPendingNavigation(null);

    if (url) {
      // Temporarily disable the guard by exiting sandbox before navigating
      useSandboxStore.getState().exitSandbox();
      // Use window.location for the navigation since we intercepted pushState
      window.location.href = url;
    }
  }, [pendingNavigation]);

  // Cancel leaving — stay on the page
  const cancelLeave = useCallback(() => {
    setShowLeaveDialog(false);
    setPendingNavigation(null);
  }, []);

  useEffect(() => {
    if (!isActive) {
      // Clean state when sandbox is not active
      setShowLeaveDialog(false);
      setPendingNavigation(null);
      return;
    }

    // ── 1. Browser close/refresh guard ──────────────────────────────────────
    function handleBeforeUnload(e: BeforeUnloadEvent) {
      e.preventDefault();
      // Modern browsers ignore custom messages but still show the native dialog
      return "";
    }

    window.addEventListener("beforeunload", handleBeforeUnload);

    // ── 2. In-app navigation interception ───────────────────────────────────
    // Monkey-patch history.pushState to intercept Next.js App Router navigation
    const originalPushState = window.history.pushState.bind(window.history);
    const originalReplaceState = window.history.replaceState.bind(window.history);

    window.history.pushState = function (
      data: unknown,
      unused: string,
      url?: string | URL | null
    ) {
      // Check if sandbox is still active at the time of navigation
      if (useSandboxStore.getState().isActive && url) {
        const targetUrl = typeof url === "string" ? url : url.toString();
        // Allow same-page navigations (hash changes, query params on same path)
        const currentPath = window.location.pathname;
        const targetPath = new URL(targetUrl, window.location.origin).pathname;

        if (currentPath !== targetPath) {
          setPendingNavigation(targetUrl);
          setShowLeaveDialog(true);
          return; // Block the navigation
        }
      }
      return originalPushState(data, unused, url);
    };

    // Also intercept replaceState for completeness
    window.history.replaceState = function (
      data: unknown,
      unused: string,
      url?: string | URL | null
    ) {
      if (useSandboxStore.getState().isActive && url) {
        const targetUrl = typeof url === "string" ? url : url.toString();
        const currentPath = window.location.pathname;
        const targetPath = new URL(targetUrl, window.location.origin).pathname;

        if (currentPath !== targetPath) {
          setPendingNavigation(targetUrl);
          setShowLeaveDialog(true);
          return; // Block the navigation
        }
      }
      return originalReplaceState(data, unused, url);
    };

    // Handle browser back/forward buttons
    function handlePopState() {
      if (useSandboxStore.getState().isActive) {
        // Push the current URL back to prevent the navigation
        window.history.pushState(null, "", window.location.href);
        setPendingNavigation(document.referrer || "/");
        setShowLeaveDialog(true);
      }
    }

    window.addEventListener("popstate", handlePopState);

    // ── Cleanup ─────────────────────────────────────────────────────────────
    return () => {
      window.removeEventListener("beforeunload", handleBeforeUnload);
      window.removeEventListener("popstate", handlePopState);
      window.history.pushState = originalPushState;
      window.history.replaceState = originalReplaceState;
    };
  }, [isActive]);

  return {
    showLeaveDialog,
    confirmLeave,
    cancelLeave,
  };
}
