"use client";

import { useEffect, useRef, useState } from "react";
import {
  getHomeLeavePreview,
  HomeLeavePreviewRequest,
  HomeLeavePreviewResponse,
} from "@/lib/api/homeLeave";

interface UseHomeLeavePreviewReturn {
  data: HomeLeavePreviewResponse | null;
  isLoading: boolean;
  error: string | null;
}

/**
 * Custom hook that fetches a home-leave preview with debouncing and
 * request cancellation. Ignores stale responses when a newer request
 * is in flight.
 *
 * @param spaceId  - The current space ID
 * @param groupId  - The group ID to preview
 * @param request  - The preview request parameters (mode, baseDays, homeDays, sliderValue, leaveDurationHours)
 */
export function useHomeLeavePreview(
  spaceId: string | null | undefined,
  groupId: string | null | undefined,
  request: HomeLeavePreviewRequest | null | undefined
): UseHomeLeavePreviewReturn {
  const [data, setData] = useState<HomeLeavePreviewResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // AbortController for cancelling in-flight requests
  const abortControllerRef = useRef<AbortController | null>(null);
  // Request counter to ignore stale responses
  const requestCounterRef = useRef(0);

  // Serialize request to detect changes
  const requestKey = request ? JSON.stringify(request) : null;

  useEffect(() => {
    // Only fire when all parameters are truthy
    if (!spaceId || !groupId || !request) {
      return;
    }

    const currentRequest = ++requestCounterRef.current;

    const timeoutId = setTimeout(async () => {
      // Cancel any pending request
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }

      const controller = new AbortController();
      abortControllerRef.current = controller;

      setIsLoading(true);
      setError(null);

      try {
        const result = await getHomeLeavePreview(
          spaceId,
          groupId,
          request,
          controller.signal
        );

        // Ignore stale responses — only apply if this is still the latest request
        if (currentRequest !== requestCounterRef.current) {
          return;
        }

        setData(result);
        setError(null);
      } catch (err: unknown) {
        // Ignore aborted requests
        if (err instanceof Error && err.name === "CanceledError") {
          return;
        }
        // Also check for native AbortError
        if (err instanceof Error && err.name === "AbortError") {
          return;
        }

        // Ignore stale error responses
        if (currentRequest !== requestCounterRef.current) {
          return;
        }

        setError("PREVIEW_LOAD_FAILED");
      } finally {
        // Only clear loading if this is still the latest request
        if (currentRequest === requestCounterRef.current) {
          setIsLoading(false);
        }
      }
    }, 500);

    return () => {
      clearTimeout(timeoutId);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [spaceId, groupId, requestKey]);

  return { data, isLoading, error };
}
