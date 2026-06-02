export const AUTH_TOKEN_CHANGED_EVENT = "auth-token-changed";

export function hasStoredAccessToken(): boolean {
  return typeof window !== "undefined" && !!localStorage.getItem("access_token");
}

export function notifyAuthTokenChanged(): void {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(AUTH_TOKEN_CHANGED_EVENT));
  }
}
