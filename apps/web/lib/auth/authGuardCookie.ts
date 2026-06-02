const AUTH_GUARD_COOKIE = "auth_guard";
const LEGACY_ACCESS_TOKEN_COOKIE = "access_token";
const COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 30;

function secureCookieSuffix(): string {
  if (typeof window === "undefined") return "";
  return window.location.protocol === "https:" ? "; Secure" : "";
}

export function setAuthGuardCookie(): void {
  if (typeof document === "undefined") return;
  document.cookie = `${AUTH_GUARD_COOKIE}=1; path=/; max-age=${COOKIE_MAX_AGE_SECONDS}; SameSite=Strict${secureCookieSuffix()}`;
}

export function clearAuthGuardCookie(): void {
  if (typeof document === "undefined") return;
  document.cookie = `${AUTH_GUARD_COOKIE}=; path=/; max-age=0; SameSite=Strict${secureCookieSuffix()}`;
  document.cookie = `${LEGACY_ACCESS_TOKEN_COOKIE}=; path=/; max-age=0; SameSite=Strict${secureCookieSuffix()}`;
}

export function setLocaleCookie(locale: string): void {
  if (typeof document === "undefined") return;
  document.cookie = `locale=${locale}; path=/; max-age=31536000; SameSite=Strict${secureCookieSuffix()}`;
}

export function clearLocaleCookie(): void {
  if (typeof document === "undefined") return;
  document.cookie = `locale=; path=/; max-age=0; SameSite=Strict${secureCookieSuffix()}`;
}
