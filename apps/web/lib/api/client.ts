import axios from "axios";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export const apiClient = axios.create({
  baseURL: API_URL,
  headers: { "Content-Type": "application/json" },
});

// Module-level redirect guard — prevents multiple concurrent API failures
// from triggering multiple redirects. Never reset; page navigation clears it.
let isRedirecting = false;

/**
 * Redirects to an error page with a `?from=` query parameter encoding the
 * current pathname. Skips if a redirect is already in progress.
 */
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
  (res) => res,
  async (error) => {
    const status = error.response?.status;
    const original = error.config;

    // 401: Attempt token refresh, then retry once
    if (status === 401 && !original._retry) {
      original._retry = true;
      try {
        const refreshToken = localStorage.getItem("refresh_token");
        if (!refreshToken) throw new Error("No refresh token");

        const { data } = await axios.post(`${API_URL}/auth/refresh`, { refreshToken });
        localStorage.setItem("access_token", data.accessToken);
        localStorage.setItem("refresh_token", data.refreshToken);

        original.headers.Authorization = `Bearer ${data.accessToken}`;
        return apiClient(original);
      } catch {
        // Refresh failed — clear tokens and redirect to unauthorized page
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        redirectToErrorPage("/error/unauthorized");
        return Promise.reject(error);
      }
    }

    // 403: Redirect to forbidden page
    if (status === 403) {
      redirectToErrorPage("/error/forbidden");
      return Promise.reject(error);
    }

    // 5xx: Redirect to server error page
    if (status === 500 || status === 502 || status === 503 || status === 504) {
      redirectToErrorPage("/error/server-error");
      return Promise.reject(error);
    }

    // 404 and all other errors: no redirect, reject with original error
    return Promise.reject(error);
  }
);
