import axios from "axios";
import { useAuthStore } from "@/lib/store/authStore";
import { useConnectivityStore } from "@/lib/store/connectivityStore";
import { writeGuardInterceptor } from "@/lib/api/writeGuard";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export const apiClient = axios.create({
  baseURL: API_URL,
  headers: { "Content-Type": "application/json" },
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
        const refreshToken = localStorage.getItem("refresh_token");
        if (!refreshToken) throw new Error("No refresh token");

        // Attempt refresh with retry for network failures
        let refreshData: any;
        try {
          const { data } = await axios.post(`${API_URL}/auth/refresh`, { refreshToken }, { timeout: 10000 });
          refreshData = data;
        } catch (firstErr: any) {
          // If it's a network error (no response) or 5xx, retry once after a short delay
          const isNetworkOrServerError = !firstErr.response || (firstErr.response?.status >= 500);
          if (isNetworkOrServerError) {
            await new Promise(r => setTimeout(r, 2000));
            const { data } = await axios.post(`${API_URL}/auth/refresh`, { refreshToken }, { timeout: 10000 });
            refreshData = data;
          } else {
            throw firstErr;
          }
        }

        localStorage.setItem("access_token", refreshData.accessToken);
        localStorage.setItem("refresh_token", refreshData.refreshToken);
        document.cookie = `access_token=${refreshData.accessToken}; path=/; max-age=900; SameSite=Strict`;

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
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        document.cookie = "access_token=; path=/; max-age=0";
        if (!isRedirecting) {
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

    // 5xx or network error: update connectivity store (only when device is online)
    if ((status >= 500 && status <= 599 || !error.response) && typeof window !== "undefined") {
      apiOnline = false;
      // Only mark server-unavailable if the device itself is online (navigator.onLine).
      // If the device is offline, the offline event listener handles that state.
      if (navigator.onLine) {
        useConnectivityStore.getState().setServerUnavailable();
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
  const handleOnline = () => store().goOnline();

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
