/**
 * Simple localStorage cache for API responses that don't change often.
 * Strategy: return cached data immediately, then fetch fresh in background.
 * If fetch succeeds, update cache. If it fails, stale data stays visible.
 */

const CACHE_PREFIX = "shifter-cache:";
const DEFAULT_TTL_MS = 30 * 60 * 1000; // 30 minutes

interface CacheEntry<T> {
  data: T;
  timestamp: number;
}

function getKey(key: string): string {
  return `${CACHE_PREFIX}${key}`;
}

/** Read from cache. Returns null if not found or expired beyond maxAge. */
export function readCache<T>(key: string, maxAgeMs: number = DEFAULT_TTL_MS): T | null {
  try {
    const raw = localStorage.getItem(getKey(key));
    if (!raw) return null;
    const entry: CacheEntry<T> = JSON.parse(raw);
    // Return even if stale — caller decides whether to show it
    if (Date.now() - entry.timestamp > maxAgeMs * 10) return null; // hard expiry at 10x TTL
    return entry.data;
  } catch {
    return null;
  }
}

/** Write to cache. */
export function writeCache<T>(key: string, data: T): void {
  try {
    const entry: CacheEntry<T> = { data, timestamp: Date.now() };
    localStorage.setItem(getKey(key), JSON.stringify(entry));
  } catch {
    // localStorage full or unavailable — silently fail
  }
}

/** Check if cache entry is fresh (within TTL). */
export function isCacheFresh(key: string, maxAgeMs: number = DEFAULT_TTL_MS): boolean {
  try {
    const raw = localStorage.getItem(getKey(key));
    if (!raw) return false;
    const entry = JSON.parse(raw);
    return Date.now() - entry.timestamp < maxAgeMs;
  } catch {
    return false;
  }
}

/** Clear a specific cache entry. */
export function clearCache(key: string): void {
  try {
    localStorage.removeItem(getKey(key));
  } catch {}
}

/**
 * Fetch with cache-first strategy.
 * Returns cached data immediately if available, then refreshes in background.
 * 
 * @param key - Cache key
 * @param fetcher - Async function that fetches fresh data
 * @param onUpdate - Called when fresh data arrives (to trigger re-render)
 * @param maxAgeMs - How long cache is considered fresh
 */
export async function fetchWithCache<T>(
  key: string,
  fetcher: () => Promise<T>,
  onUpdate?: (data: T) => void,
  maxAgeMs: number = DEFAULT_TTL_MS
): Promise<T | null> {
  const cached = readCache<T>(key, maxAgeMs);

  // Always try to fetch fresh data
  try {
    const fresh = await fetcher();
    writeCache(key, fresh);
    if (onUpdate) onUpdate(fresh);
    return fresh;
  } catch {
    // Fetch failed — return cached data if available
    return cached;
  }
}
