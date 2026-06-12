"use client";

import { useEffect, useRef } from "react";
import { useAuthStore } from "@/lib/store/authStore";
import { queryClient } from "@/lib/query/queryClient";

function shouldUseServiceWorkerCache(): boolean {
  if (typeof window === "undefined") return false;
  if (process.env.NODE_ENV !== "production") return false;

  const hostname = window.location.hostname;
  return hostname !== "localhost" && hostname !== "127.0.0.1" && hostname !== "::1";
}

/**
 * Maps a cached API URL to the corresponding React Query key(s) to invalidate.
 * Returns null if the URL doesn't match any known pattern.
 */
export function getQueryKeysForUrl(url: string): unknown[][] | null {
  try {
    const { pathname } = new URL(url);

    // /spaces/{spaceId}/groups
    const groupsMatch = pathname.match(/\/spaces\/([^/]+)\/groups$/);
    if (groupsMatch) {
      return [["groups", groupsMatch[1]]];
    }

    // /spaces/{spaceId}/groups/{groupId}/members
    const membersMatch = pathname.match(
      /\/spaces\/([^/]+)\/groups\/([^/]+)\/members$/
    );
    if (membersMatch) {
      return [["group-members", membersMatch[1], membersMatch[2]]];
    }

    // /spaces/{spaceId}/groups/{groupId}/tasks
    const tasksMatch = pathname.match(
      /\/spaces\/([^/]+)\/groups\/([^/]+)\/tasks$/
    );
    if (tasksMatch) {
      // Invalidate broadly — tasks don't have a dedicated query key in the keys file
      return [["group-tasks", tasksMatch[1], tasksMatch[2]]];
    }

    // /spaces/{spaceId}/schedule-versions
    const scheduleMatch = pathname.match(
      /\/spaces\/([^/]+)\/schedule-versions$/
    );
    if (scheduleMatch) {
      return [["draft-versions", scheduleMatch[1]]];
    }

    // /spaces/{spaceId}/billing/subscription
    const billingMatch = pathname.match(
      /\/spaces\/([^/]+)\/billing\/subscription$/
    );
    if (billingMatch) {
      return [["billing", billingMatch[1]]];
    }

    const selfServiceMatch = pathname.match(
      /\/spaces\/([^/]+)\/groups\/([^/]+)\/(self-service-config|self-service-cycles\/(?:status|closeout)|shift-slots\/available|shift-requests\/mine|shift-requests\/absence-reports(?:\/mine)?|shift-change-requests\/(?:mine|admin)|waitlist\/(?:mine|admin)|shift-swaps\/(?:my|admin))$/
    );
    if (selfServiceMatch) {
      const [, spaceId, groupId, endpoint] = selfServiceMatch;
      const keys: unknown[][] = [["self-service", spaceId, groupId]];

      if (endpoint === "self-service-config") {
        keys.push(["self-service-config", spaceId, groupId]);
      } else if (endpoint.startsWith("self-service-cycles/")) {
        keys.push(["self-service-cycle", spaceId, groupId]);
      } else if (endpoint === "shift-slots/available") {
        keys.push(["self-service-slots", spaceId, groupId]);
      } else if (endpoint === "shift-requests/mine") {
        keys.push(["self-service-my-shifts", spaceId, groupId]);
      } else if (endpoint.startsWith("shift-requests/absence-reports")) {
        keys.push(["self-service-absence-reports", spaceId, groupId]);
      } else if (endpoint.startsWith("shift-change-requests/")) {
        keys.push(["self-service-shift-changes", spaceId, groupId]);
      } else if (endpoint.startsWith("waitlist/")) {
        keys.push(["self-service-waitlist", spaceId, groupId]);
      } else if (endpoint.startsWith("shift-swaps/")) {
        keys.push(["self-service-swaps", spaceId, groupId]);
      }

      return keys;
    }

    return null;
  } catch {
    return null;
  }
}

/**
 * Manages the cache lifecycle between the app and the service worker.
 *
 * - On mount: sends SET_CURRENT_USER to the SW with the current userId
 * - On logout: sends CLEAR_USER_CACHE to the SW
 * - Listens for CACHE_UPDATED messages from the SW and invalidates React Query keys
 *
 * Call this once at the app root level (e.g., in Providers).
 */
export function useCacheLifecycle(): void {
  const userId = useAuthStore((s) => s.userId);
  const prevUserIdRef = useRef<string | null>(null);

  useEffect(() => {
    // Guard: service worker not available (SSR, unsupported browser)
    if (
      typeof window === "undefined" ||
      !("serviceWorker" in navigator) ||
      !navigator.serviceWorker ||
      !shouldUseServiceWorkerCache()
    ) {
      return;
    }

    // Send SET_CURRENT_USER when userId is available
    if (userId) {
      navigator.serviceWorker.controller?.postMessage({
        type: "SET_CURRENT_USER",
        userId,
      });
    }

    // Detect logout: previous userId existed but now it's null
    if (prevUserIdRef.current && !userId) {
      navigator.serviceWorker.controller?.postMessage({
        type: "CLEAR_USER_CACHE",
        userId: prevUserIdRef.current,
      });
    }

    prevUserIdRef.current = userId;
  }, [userId]);

  useEffect(() => {
    // Guard: service worker not available (SSR, unsupported browser)
    if (
      typeof window === "undefined" ||
      !("serviceWorker" in navigator) ||
      !navigator.serviceWorker ||
      !shouldUseServiceWorkerCache()
    ) {
      return;
    }

    function handleMessage(event: MessageEvent) {
      const { data } = event;
      if (!data || data.type !== "CACHE_UPDATED") return;

      const keys = getQueryKeysForUrl(data.url);
      if (!keys) return;

      for (const key of keys) {
        queryClient.invalidateQueries({ queryKey: key });
      }
    }

    navigator.serviceWorker.addEventListener("message", handleMessage);

    return () => {
      navigator.serviceWorker.removeEventListener("message", handleMessage);
    };
  }, []);
}
