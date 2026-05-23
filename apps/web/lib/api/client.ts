import axios from "axios";
import { useAuthStore } from "@/lib/store/authStore";

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
 * Simple localStorage cache for all GET API responses.
 * Strategy: cache every successful GET, serve from cache when API fails.
 */
const CACHE_PREFIX = "shifter-api:";

function getCacheKey(url: string): string {
  return `${CACHE_PREFIX}${url}`;
}

function readApiCache(url: string): any | null {
  try {
    const raw = localStorage.getItem(getCacheKey(url));
    if (!raw) return null;
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function writeApiCache(url: string, data: any): void {
  try {
    localStorage.setItem(getCacheKey(url), JSON.stringify(data));
  } catch {
    // localStorage full — evict oldest entries
    try {
      const keys = Object.keys(localStorage).filter(k => k.startsWith(CACHE_PREFIX));
      if (keys.length > 50) {
        // Remove first 10 entries
        keys.slice(0, 10).forEach(k => localStorage.removeItem(k));
        localStorage.setItem(getCacheKey(url), JSON.stringify(data));
      }
    } catch {}
  }
}

/** Track API connectivity status */
let apiOnline = true;
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

// Response interceptor: error handling order is 401 → 403 → 5xx → 404
apiClient.interceptors.response.use(
  (res) => {
    // Cache all successful GET responses
    if (res.config.method === "get" && res.config.url) {
      const fullUrl = res.config.baseURL ? `${res.config.url}` : res.config.url;
      writeApiCache(fullUrl, res.data);
      apiOnline = true;
      if (typeof window !== "undefined") {
        window.dispatchEvent(new CustomEvent("api-online"));
      }
    }
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

        const { data } = await axios.post(`${API_URL}/auth/refresh`, { refreshToken });
        localStorage.setItem("access_token", data.accessToken);
        localStorage.setItem("refresh_token", data.refreshToken);
        document.cookie = `access_token=${data.accessToken}; path=/; max-age=900; SameSite=Strict`;

        // Update timezone from refresh response (handles DST changes between sessions)
        if (data.timezoneId !== undefined || data.timezoneOffsetMinutes !== undefined) {
          useAuthStore.getState().setTimezone(
            data.timezoneId ?? null,
            data.timezoneOffsetMinutes ?? 120
          );
        }

        isRefreshing = false;
        onTokenRefreshed(data.accessToken);

        original.headers.Authorization = `Bearer ${data.accessToken}`;
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

    // 5xx or network error: serve from cache if available
    if ((status === 500 || status === 502 || status === 503 || status === 504 || !error.response) && typeof window !== "undefined") {
      apiOnline = false;
      window.dispatchEvent(new CustomEvent("api-error", { detail: { status, url: error.config?.url } }));

      // Try to serve cached response for GET requests
      if (error.config?.method === "get" && error.config?.url) {
        const cached = readApiCache(error.config.url);
        if (cached !== null) {
          // Return a fake successful response with cached data
          return { data: cached, status: 200, statusText: "OK (cached)", config: error.config, headers: {} };
        }
      }
      return Promise.reject(error);
    }

    // 404 and all other errors: no redirect, reject with original error
    return Promise.reject(error);
  }
);
