import axios from "axios";
import { useAuthStore } from "@/lib/store/authStore";
import { useConnectivityStore } from "@/lib/store/connectivityStore";
import { writeGuardInterceptor } from "@/lib/api/writeGuard";
import { notifyAuthTokenChanged } from "@/lib/auth/tokenState";
import { clearAuthGuardCookie, setAuthGuardCookie } from "@/lib/auth/authGuardCookie";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export const apiClient = axios.create({
  baseURL: API_URL,
  headers: { "Content-Type": "application/json" },
  withCredentials: true,
});

// Module-level redirect guard — prevents multiple concurrent API failures
// from triggering multiple redirects. Never reset; page navigation clears it.
let isRedirecting = false;

// Token refresh mutex — prevents multiple concurrent 401 responses from
// triggering multiple refresh requests (which would revoke the token and
// cause a cascade of failures leading to logout).
let isRefreshing = false;
let refreshSubscribers: Array<(token: string) => void> = [];

function subscribeTokenRefresh(cb: (token: string) => void) {
  refreshSubscribers.push(cb);
}

function onTokenRefreshed(token: string) {
  refreshSubscribers.forEach(cb => cb(token));
  refreshSubscribers = [];
}

/**
 * @deprecated Use `useConnectivityStore` instead. Kept temporarily for backward compatibility.
 */
let apiOnline = true;
/** @deprecated Use `useConnectivityStore().isConnected` instead. */
export function isApiOnline(): boolean { return apiOnline; }

/**
 * Probes real internet connectivity by fetching a lightweight external resource.
 * Used when navigator.onLine is true but an API call failed — distinguishes
 * "server is down" from "connected to WiFi but no internet".
 *
 * Tries the app's own /health endpoint first (same-origin, fast).
 * If that fails, tries a well-known external endpoint as a fallback.
 * Returns true if the device has internet, false otherwise.
 */
let probeInFlight: Promise<boolean> | null = null;

async function probeConnectivity(): Promise<boolean> {
  // Deduplicate concurrent probes — if one is already running, reuse it
  if (probeInFlight) return probeInFlight;

  probeInFlight = (async () => {
    try {
      // Try fetching a tiny external resource (Google's generate_204 endpoint)
      // This confirms internet access independent of our server's health.
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 3000);

      const response = await fetch("https://www.gstatic.com/generate_204", {
        method: "HEAD",
        mode: "no-cors",
        signal: controller.signal,
      });

      clearTimeout(timeout);
      // In no-cors mode, an opaque response (type "opaque") means the request succeeded
      return response.type === "opaque" || response.ok;
    } catch {
      // External probe failed — no internet
      return false;
    }
  })();

  const result = await probeInFlight;
  probeInFlight = null;
  return result;
}

function redirectToErrorPage(path: string): void {
  if (isRedirecting) return;
  isRedirecting = true;
  const from = encodeURIComponent(window.location.pathname);
  window.location.href = `${path}?from=${from}`;
}

// Attach access token from localStorage on every request
apiClient.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("access_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Write guard: block non-GET requests when disconnected
apiClient.interceptors.request.use(writeGuardInterceptor);

// Response interceptor: error handling order is 401 → 403 → 5xx → 404
apiClient.interceptors.response.use(
  (res) => {
    // On successful response, notify connectivity store that server is reachable.
    // The store internally checks if we're in server-unavailable state before acting.
    useConnectivityStore.getState().setServerRecovered();
    apiOnline = true;
    return res;
  },
  async (error) => {
    const status = error.response?.status;
    const original = error.config;

    // 401: Attempt token refresh, then retry once
    if (status === 401 && !original._retry) {
      original._retry = true;

      // If a refresh is already in progress, queue this request to retry
      // once the refresh completes (avoids revoking the token multiple times).
      if (isRefreshing) {
        return new Promise((resolve) => {
          subscribeTokenRefresh((newToken: string) => {
            original.headers.Authorization = `Bearer ${newToken}`;
            resolve(apiClient(original));
          });
        });
      }

      isRefreshing = true;

      try {
        // Attempt refresh with retry for network failures
        let refreshData: any;
        try {
          const { data } = await axios.post(`${API_URL}/auth/refresh`, {}, { timeout: 10000, withCredentials: true });
          refreshData = data;
        } catch (firstErr: any) {
          // If it's a network error (no response) or 5xx, retry once after a short delay
          const isNetworkOrServerError = !firstErr.response || (firstErr.response?.status >= 500);
          if (isNetworkOrServerError) {
            await new Promise(r => setTimeout(r, 2000));
            const { data } = await axios.post(`${API_URL}/auth/refresh`, {}, { timeout: 10000, withCredentials: true });
            refreshData = data;
          } else {
            throw firstErr;
          }
        }

        localStorage.setItem("access_token", refreshData.accessToken);
        notifyAuthTokenChanged();
        setAuthGuardCookie();

        // Update timezone from refresh response (handles DST changes between sessions)
        if (refreshData.timezoneId !== undefined || refreshData.timezoneOffsetMinutes !== undefined) {
          useAuthStore.getState().setTimezone(
            refreshData.timezoneId ?? null,
            refreshData.timezoneOffsetMinutes ?? 120
          );
        }

        isRefreshing = false;
        onTokenRefreshed(refreshData.accessToken);

        original.headers.Authorization = `Bearer ${refreshData.accessToken}`;
        return apiClient(original);
      } catch {
        isRefreshing = false;
        refreshSubscribers = [];
        // Refresh failed — clear tokens and cookie, redirect to login
        // (unless the request opted out of redirect via _skipRedirect)
        localStorage.removeItem("access_token");
        notifyAuthTokenChanged();
        clearAuthGuardCookie();
        useAuthStore.getState().clearAuthState();
        if (!original._skipRedirect && !isRedirecting) {
          isRedirecting = true;
          window.location.href = "/login?redirect=" + encodeURIComponent(window.location.pathname + window.location.search);
        }
        return Promise.reject(error);
      }
    }

    // 403: Let the calling code handle it — don't redirect automatically.
    // Pages that need special 403 handling (like platform) check err.response.status themselves.
    if (status === 403) {
      return Promise.reject(error);
    }

    // 5xx or network error: determine if it's a client connectivity issue or server issue
    if ((status >= 500 && status <= 599 || !error.response) && typeof window !== "undefined") {
      apiOnline = false;

      if (!navigator.onLine) {
        // Browser already knows we're offline
        useConnectivityStore.getState().goOffline();
      } else {
        // navigator.onLine is true but the request failed — probe real connectivity.
        // This catches "connected to WiFi but no internet" scenarios.
        probeConnectivity().then((hasInternet) => {
          if (hasInternet) {
            // Internet works, server is genuinely down
            useConnectivityStore.getState().setServerUnavailable();
          } else {
            // No real internet despite navigator.onLine — treat as offline
            useConnectivityStore.getState().goOffline();
          }
        });
      }
      return Promise.reject(error);
    }

    // 404 and all other errors: no redirect, reject with original error
    return Promise.reject(error);
  }
);


/**
 * Initialize browser connectivity event listeners.
 * Call once at app startup (e.g., from providers.tsx).
 * Sets up online/offline event listeners that drive the connectivity store.
 */
export function initConnectivity(): () => void {
  if (typeof window === "undefined") return () => {};

  const store = useConnectivityStore.getState;

  const handleOffline = () => store().goOffline();
  const handleOnline = () => {
    // When the browser says we're back online, verify with a real probe
    // before transitioning to "online" state. This prevents false positives
    // when connected to a captive portal or WiFi with no internet.
    probeConnectivity().then((hasInternet) => {
      if (hasInternet) {
        store().goOnline();
      }
      // If probe fails, stay in current state (offline) — the next successful
      // API call will trigger setServerRecovered → online anyway.
    });
  };

  window.addEventListener("offline", handleOffline);
  window.addEventListener("online", handleOnline);

  // Sync initial state — if the browser is already offline when the app loads
  if (!navigator.onLine) {
    store().goOffline();
  }

  return () => {
    window.removeEventListener("offline", handleOffline);
    window.removeEventListener("online", handleOnline);
  };
}
