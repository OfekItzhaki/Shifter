"use client";

import { useEffect, useRef, useState } from "react";
import {
  getHomeLeavePreview,
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
 * @param balanceValue - The slider balance value (0–100)
 */
export function useHomeLeavePreview(
  spaceId: string | null | undefined,
  groupId: string | null | undefined,
  balanceValue: number | null | undefined
): UseHomeLeavePreviewReturn {
  const [data, setData] = useState<HomeLeavePreviewResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // AbortController for cancelling in-flight requests
  const abortControllerRef = useRef<AbortController | null>(null);
  // Request counter to ignore stale responses
  const requestCounterRef = useRef(0);

  useEffect(() => {
    // Only fire when all parameters are truthy
    if (!spaceId || !groupId || balanceValue == null) {
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
          balanceValue,
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

        setError("לא ניתן לטעון תצוגה מקדימה");
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
  }, [spaceId, groupId, balanceValue]);

  return { data, isLoading, error };
}
