import type { InternalAxiosRequestConfig } from "axios";
import { useConnectivityStore } from "@/lib/store/connectivityStore";

/**
 * Error code thrown when a mutation request is blocked due to offline/server-unavailable state.
 */
export const OFFLINE_WRITE_BLOCKED = "OFFLINE_WRITE_BLOCKED";

/**
 * Axios request interceptor that blocks non-GET requests when the app is disconnected.
 * GET requests always pass through regardless of connectivity state.
 */
export function writeGuardInterceptor(
  config: InternalAxiosRequestConfig
): InternalAxiosRequestConfig {
  const method = (config.method ?? "get").toLowerCase();

  // GET requests always pass through
  if (method === "get") {
    return config;
  }

  const { status } = useConnectivityStore.getState();

  if (status !== "online") {
    const error = new Error("Cannot perform write operations while offline");
    Object.assign(error, {
      code: OFFLINE_WRITE_BLOCKED,
      config,
    });
    throw error;
  }

  return config;
}

/**
 * React hook that provides write guard state for UI controls.
 * Returns whether mutation controls should be disabled and a tooltip message.
 * Subscribes reactively to the connectivity store.
 */
export function useWriteGuard(): { isDisabled: boolean; tooltipText: string } {
  const status = useConnectivityStore((state) => state.status);

  const isDisabled = status !== "online";
  const tooltipText = isDisabled
    ? "לא ניתן לבצע פעולה זו ללא חיבור לשרת"
    : "";

  return { isDisabled, tooltipText };
}
