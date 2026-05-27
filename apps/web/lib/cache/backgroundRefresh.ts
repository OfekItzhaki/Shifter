import { useConnectivityStore, ConnectivityStatus } from "@/lib/store/connectivityStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { apiClient } from "@/lib/api/client";

/**
 * Endpoints to refresh when connectivity is restored.
 * These match the CACHED_API_PATTERNS in the service worker.
 */
const CACHED_ENDPOINTS = [
  "/spaces/{spaceId}/groups",
  "/spaces/{spaceId}/schedule-versions",
  "/spaces/{spaceId}/billing/subscription",
] as const;

const RETRY_DELAY_MS = 30_000;
const MAX_RETRIES = 3;

let retryTimeoutId: ReturnType<typeof setTimeout> | null = null;
let retryCount = 0;

/**
 * Silently fetches all cached endpoints for the given space.
 * Does NOT use React Query's refetchQueries — instead makes raw API calls
 * that flow through the service worker, which updates the cache and posts
 * CACHE_UPDATED messages to all clients.
 *
 * On failure: retains existing cache, schedules retry after 30s, max 3 retries.
 */
async function refreshEndpoints(spaceId: string): Promise<void> {
  const urls = CACHED_ENDPOINTS.map((pattern) =>
    pattern.replace("{spaceId}", spaceId)
  );

  const results = await Promise.allSettled(
    urls.map((url) => apiClient.get(url))
  );

  const hasFailure = results.some((r) => r.status === "rejected");

  if (hasFailure && retryCount < MAX_RETRIES) {
    retryCount++;
    retryTimeoutId = setTimeout(() => {
      // Only retry if still online
      const { status } = useConnectivityStore.getState();
      if (status === "online") {
        refreshEndpoints(spaceId);
      }
    }, RETRY_DELAY_MS);
  } else {
    // Reset retry count on full success or after max retries exhausted
    retryCount = 0;
  }
}

/**
 * Cancels any pending retry timeout and resets the retry counter.
 */
function cancelPendingRetry(): void {
  if (retryTimeoutId !== null) {
    clearTimeout(retryTimeoutId);
    retryTimeoutId = null;
  }
  retryCount = 0;
}

/**
 * Initializes the background refresh subscription.
 * Subscribes to the connectivity store and triggers a silent refresh
 * of all cached endpoints when the status transitions TO "online"
 * (from "offline" or "server-unavailable").
 *
 * Returns a cleanup function that unsubscribes and cancels pending retries.
 */
export function initBackgroundRefresh(): () => void {
  let previousStatus: ConnectivityStatus = useConnectivityStore.getState().status;

  const unsubscribe = useConnectivityStore.subscribe((state) => {
    const currentStatus = state.status;

    // Only trigger refresh on transitions TO online
    if (
      currentStatus === "online" &&
      (previousStatus === "offline" || previousStatus === "server-unavailable")
    ) {
      // Cancel any pending retry from a previous cycle
      cancelPendingRetry();

      const spaceId = useSpaceStore.getState().currentSpaceId;
      if (spaceId) {
        refreshEndpoints(spaceId);
      }
    }

    previousStatus = currentStatus;
  });

  return () => {
    unsubscribe();
    cancelPendingRetry();
  };
}
