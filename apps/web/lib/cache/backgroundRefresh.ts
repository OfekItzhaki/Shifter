import { useConnectivityStore, ConnectivityStatus } from "@/lib/store/connectivityStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { apiClient } from "@/lib/api/client";
import { getLastGroup } from "@/lib/utils/pickLastGroup";

/**
 * Endpoints to refresh when connectivity is restored.
 * Space-level endpoints cached by the service worker.
 */
const SPACE_CACHED_ENDPOINTS = [
  "/spaces/{spaceId}/groups",
  "/spaces/{spaceId}/schedule-versions",
  "/spaces/{spaceId}/billing/subscription",
] as const;

/**
 * Member-safe group endpoints cached by the service worker.
 * Admin-only review endpoints are intentionally excluded to avoid 403 retry loops
 * for normal members.
 */
const GROUP_CACHED_ENDPOINTS = [
  "/spaces/{spaceId}/groups/{groupId}/members",
  "/spaces/{spaceId}/groups/{groupId}/tasks",
  "/spaces/{spaceId}/groups/{groupId}/self-service-config",
  "/spaces/{spaceId}/groups/{groupId}/self-service-cycles/status",
  "/spaces/{spaceId}/groups/{groupId}/shift-slots/available?cycleId=current",
  "/spaces/{spaceId}/groups/{groupId}/shift-requests/mine?schedulingCycleId=current",
  "/spaces/{spaceId}/groups/{groupId}/shift-requests/absence-reports/mine?cycleId=current",
  "/spaces/{spaceId}/groups/{groupId}/shift-change-requests/mine",
  "/spaces/{spaceId}/groups/{groupId}/waitlist/mine",
  "/spaces/{spaceId}/groups/{groupId}/shift-swaps/my",
] as const;

const RETRY_DELAY_MS = 30_000;
const MAX_RETRIES = 3;

let retryTimeoutId: ReturnType<typeof setTimeout> | null = null;
let retryCount = 0;

function getGroupIdFromPathname(pathname: string): string | null {
  const match = pathname.match(/^\/groups\/([^/?#]+)/);
  return match ? decodeURIComponent(match[1]) : null;
}

function uniqueValues(values: Array<string | null | undefined>): string[] {
  return Array.from(new Set(values.filter((value): value is string => Boolean(value))));
}

export function buildRefreshUrls(
  spaceId: string,
  groupIds: string[] = []
): string[] {
  const urls = SPACE_CACHED_ENDPOINTS.map((pattern) =>
    pattern.replace("{spaceId}", spaceId)
  );

  for (const groupId of uniqueValues(groupIds)) {
    urls.push(
      ...GROUP_CACHED_ENDPOINTS.map((pattern) =>
        pattern
          .replace("{spaceId}", spaceId)
          .replace("{groupId}", encodeURIComponent(groupId))
      )
    );
  }

  return urls;
}

/**
 * Silently fetches all cached endpoints for the given space.
 * Does NOT use React Query's refetchQueries — instead makes raw API calls
 * that flow through the service worker, which updates the cache and posts
 * CACHE_UPDATED messages to all clients.
 *
 * On failure: retains existing cache, schedules retry after 30s, max 3 retries.
 */
async function refreshEndpoints(spaceId: string, groupIds: string[] = []): Promise<void> {
  const urls = buildRefreshUrls(spaceId, groupIds);

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
        refreshEndpoints(spaceId, groupIds);
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
        refreshEndpoints(spaceId, uniqueValues([
          typeof window === "undefined" ? null : getGroupIdFromPathname(window.location.pathname),
          getLastGroup(),
        ]));
      }
    }

    previousStatus = currentStatus;
  });

  return () => {
    unsubscribe();
    cancelPendingRetry();
  };
}
