/**
 * Shifter Service Worker
 * 
 * Strategy:
 * - Cached API endpoints (groups, members, tasks, schedule-versions, billing):
 *   Stale-while-revalidate with per-user cache. Returns cached response immediately,
 *   fetches fresh data in background, notifies clients when data changes.
 * - Schedule data (GET /spaces/{id}/schedule-versions/current, /my-assignments):
 *   Network-first with cache fallback. Soldiers can view their schedule offline.
 * - Static assets (JS, CSS, images): Cache-first for fast loads.
 * - API mutations (POST, PUT, DELETE): Always network, never cache.
 * - Auth endpoints: Never cache.
 */

const CACHE_VERSION = "1.9.0";
const CACHE_NAME = "shifter-" + CACHE_VERSION;
const STATIC_CACHE = "shifter-static-" + CACHE_VERSION;

// Active user ID for per-user cache partitioning
let currentUserId = null;

// Patterns for API endpoints to cache with stale-while-revalidate strategy
const CACHED_API_PATTERNS = [
  /\/spaces\/[^/]+\/groups$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/members$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/tasks$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/self-service-config$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/self-service-cycles\/status$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/self-service-cycles\/closeout$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-slots\/available$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-slots\/admin\/assignments$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-requests\/mine$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-requests\/absence-reports$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-requests\/absence-reports\/mine$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-change-requests\/mine$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-change-requests\/admin$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/waitlist\/mine$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/waitlist\/admin$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-swaps\/my$/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/shift-swaps\/admin$/,
  /\/spaces\/[^/]+\/schedule-versions$/,
  /\/spaces\/[^/]+\/billing\/subscription$/,
];

// Patterns for schedule-related GET requests to cache
const SCHEDULE_PATTERNS = [
  /\/spaces\/[^/]+\/schedule-versions\/current/,
  /\/spaces\/[^/]+\/my-assignments/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/schedule/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/home-leave-schedule/,
  /\/schedule\/my-missions/,
  /\/schedule\/today/,
];

// Patterns to never cache
const NEVER_CACHE_PATTERNS = [
  /\/auth\//,
  /\/refresh/,
];

// Static asset extensions to cache
const STATIC_EXTENSIONS = /\.(js|css|png|jpg|jpeg|svg|ico|woff2?|ttf)$/;

self.addEventListener("install", (event) => {
  // Skip waiting to activate immediately
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  // Clean up old caches but preserve per-user API caches (shifter-api-{userId})
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys
          .filter((key) =>
            key !== CACHE_NAME &&
            key !== STATIC_CACHE &&
            !key.startsWith("shifter-api-")
          )
          .map((key) => caches.delete(key))
      )
    ).then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (event) => {
  const { request } = event;
  const url = new URL(request.url);

  // Only handle GET requests
  if (request.method !== "GET") return;

  // Only handle http/https — ignore chrome-extension://, etc.
  if (!url.protocol.startsWith("http")) return;

  // Never cache auth endpoints
  if (NEVER_CACHE_PATTERNS.some((p) => p.test(url.pathname))) return;

  // Static assets: cache-first
  if (STATIC_EXTENSIONS.test(url.pathname)) {
    event.respondWith(cacheFirst(request, STATIC_CACHE));
    return;
  }

  // Cached API endpoints: stale-while-revalidate (per-user cache)
  if (currentUserId && CACHED_API_PATTERNS.some((p) => p.test(url.pathname))) {
    event.respondWith(staleWhileRevalidate(request));
    return;
  }

  // Schedule API endpoints: network-first with cache fallback
  if (SCHEDULE_PATTERNS.some((p) => p.test(url.pathname))) {
    event.respondWith(networkFirstWithCache(request, CACHE_NAME));
    return;
  }

  // Next.js pages and other requests: network-first, cache HTML for offline shell
  if (request.headers.get("accept")?.includes("text/html")) {
    event.respondWith(networkFirstWithCache(request, CACHE_NAME));
    return;
  }
});

/**
 * Network-first strategy: try network, fall back to cache.
 * On success, update the cache for next offline use.
 */
async function networkFirstWithCache(request, cacheName) {
  try {
    const response = await fetch(request);
    // Only cache successful responses
    if (response.ok) {
      const cache = await caches.open(cacheName);
      cache.put(request, response.clone());
    }
    return response;
  } catch (error) {
    // Network failed — try cache
    const cached = await caches.match(request);
    if (cached) {
      return cached;
    }
    // If it's an HTML request, return the offline page
    if (request.headers.get("accept")?.includes("text/html")) {
      return new Response(offlineHTML(), {
        headers: { "Content-Type": "text/html" },
      });
    }
    // For API requests, return a proper error response
    return new Response(
      JSON.stringify({ error: "offline", message: "No internet connection" }),
      {
        status: 503,
        headers: { "Content-Type": "application/json" },
      }
    );
  }
}

/**
 * Stale-while-revalidate strategy for per-user cached API endpoints.
 * Returns cached response immediately, fetches fresh data in background.
 * Posts CACHE_UPDATED message to all clients when fresh data differs from cached.
 * Returns 503 with {"error": "offline"} when no cache exists and network fails.
 */
async function staleWhileRevalidate(request) {
  const cacheName = getUserCacheName();
  const cache = await caches.open(cacheName);
  const cached = await cache.match(request);

  if (cached) {
    // Return cached response immediately, then revalidate in background
    revalidateInBackground(request, cache, cached.clone());
    return cached;
  }

  // No cache — try network directly
  try {
    const response = await fetch(request);
    if (response.ok) {
      await putWithMetadata(cache, request, response.clone());
    }
    return response;
  } catch (error) {
    // No cache and network failed — return 503 offline indicator
    return new Response(
      JSON.stringify({ error: "offline" }),
      {
        status: 503,
        headers: { "Content-Type": "application/json" },
      }
    );
  }
}

/**
 * Fetch fresh data in background and update cache.
 * Posts CACHE_UPDATED message to all clients if data differs.
 */
async function revalidateInBackground(request, cache, cachedResponse) {
  try {
    const freshResponse = await fetch(request);
    if (!freshResponse.ok) return;

    // Compare response bodies to detect changes
    const [cachedBody, freshBody] = await Promise.all([
      cachedResponse.text(),
      freshResponse.clone().text(),
    ]);

    // Update cache with fresh response
    await putWithMetadata(cache, request, freshResponse.clone());

    // Notify clients if data changed
    if (cachedBody !== freshBody) {
      const clients = await self.clients.matchAll();
      const message = {
        type: "CACHE_UPDATED",
        url: request.url,
        timestamp: Date.now(),
      };
      clients.forEach((client) => client.postMessage(message));
    }
  } catch (error) {
    // Network failed during background revalidation — silently ignore,
    // the cached response was already served to the client
  }
}

/**
 * Store a response in cache with X-Cache-Timestamp and X-Cache-Size metadata headers.
 * After storing, evicts least-recently-used entries if total cache size exceeds 50MB.
 */
async function putWithMetadata(cache, request, response) {
  const body = await response.arrayBuffer();
  const headers = new Headers(response.headers);
  headers.set("X-Cache-Timestamp", String(Date.now()));
  headers.set("X-Cache-Size", String(body.byteLength));

  const metadataResponse = new Response(body, {
    status: response.status,
    statusText: response.statusText,
    headers: headers,
  });

  await cache.put(request, metadataResponse);
  await evictIfNeeded(cache);
}

// Maximum per-user cache size: 50MB
const MAX_CACHE_SIZE_BYTES = 50 * 1024 * 1024;

/**
 * Evict least-recently-used cache entries when total cache size exceeds 50MB.
 * Uses X-Cache-Timestamp to determine LRU ordering and X-Cache-Size to track total size.
 */
async function evictIfNeeded(cache) {
  const keys = await cache.keys();
  if (keys.length === 0) return;

  // Collect metadata for all entries
  const entries = [];
  let totalSize = 0;

  for (const request of keys) {
    const response = await cache.match(request);
    if (!response) continue;

    const size = parseInt(response.headers.get("X-Cache-Size") || "0", 10);
    const timestamp = parseInt(response.headers.get("X-Cache-Timestamp") || "0", 10);

    entries.push({ request, size, timestamp });
    totalSize += size;
  }

  // If within limit, no eviction needed
  if (totalSize <= MAX_CACHE_SIZE_BYTES) return;

  // Sort by timestamp ascending (oldest first = least recently used)
  entries.sort((a, b) => a.timestamp - b.timestamp);

  // Evict oldest entries until total size is within limit
  for (const entry of entries) {
    if (totalSize <= MAX_CACHE_SIZE_BYTES) break;
    await cache.delete(entry.request);
    totalSize -= entry.size;
  }
}

/**
 * Get the per-user cache name for the current user.
 */
function getUserCacheName() {
  return `shifter-api-${currentUserId}`;
}

/**
 * Cache-first strategy: try cache, fall back to network.
 * On network success, update the cache.
 */
async function cacheFirst(request, cacheName) {
  const cached = await caches.match(request);
  if (cached) return cached;

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(cacheName);
      cache.put(request, response.clone());
    }
    return response;
  } catch (e) {
    return new Response("", { status: 503 });
  }
}

/**
 * Minimal offline HTML page shown when no cached version is available.
 */
function offlineHTML() {
  return `<!DOCTYPE html>
<html dir="rtl" lang="he">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Shifter — Offline</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body {
      font-family: system-ui, -apple-system, sans-serif;
      background: #f8fafc;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      padding: 1rem;
      color: #0f172a;
    }
    .container {
      text-align: center;
      max-width: 360px;
    }
    .icon {
      width: 64px;
      height: 64px;
      margin: 0 auto 1.5rem;
      background: #e2e8f0;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    h1 { font-size: 1.25rem; font-weight: 700; margin-bottom: 0.5rem; }
    p { font-size: 0.875rem; color: #64748b; line-height: 1.5; margin-bottom: 1.5rem; }
    button {
      background: #3b82f6;
      color: white;
      border: none;
      border-radius: 10px;
      padding: 0.75rem 1.5rem;
      font-size: 0.875rem;
      font-weight: 600;
      cursor: pointer;
    }
    button:active { background: #2563eb; }
  </style>
</head>
<body>
  <div class="container">
    <div class="icon">
      <svg width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="#94a3b8" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M18.364 5.636a9 9 0 010 12.728M5.636 18.364a9 9 0 010-12.728M12 9v2m0 4h.01"/>
      </svg>
    </div>
    <h1>אין חיבור לאינטרנט</h1>
    <p>לא ניתן לטעון את העמוד. בדוק את החיבור לרשת ונסה שוב.</p>
    <button onclick="location.reload()">נסה שוב</button>
  </div>
</body>
</html>`;
}

// Push notification: display native notification from server payload
self.addEventListener("push", (event) => {
  if (!event.data) return;

  let payload;
  try {
    payload = event.data.json();
  } catch (e) {
    // Malformed payload — ignore gracefully
    return;
  }

  const { title, body, icon, url, tag, timestamp } = payload;

  // title is required for showNotification; skip if missing
  if (!title) return;

  event.waitUntil(
    self.registration.showNotification(title, {
      body: body || "",
      icon: icon || "/shifter_icon.png",
      badge: "/shifter_favicon32.png",
      tag: tag || undefined,
      timestamp: timestamp || Date.now(),
      data: { url },
      dir: "auto",
    })
  );
});

// Push notification click: close notification and navigate to target URL
self.addEventListener("notificationclick", (event) => {
  event.notification.close();

  const url = event.notification.data?.url || "/";

  event.waitUntil(
    self.clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then((clients) => {
        // Focus existing app window if one is open
        const existing = clients.find((c) =>
          c.url.includes(self.location.origin)
        );
        if (existing) {
          existing.navigate(url);
          return existing.focus();
        }
        // Otherwise open a new window
        return self.clients.openWindow(url);
      })
  );
});

// Listen for messages from the app
self.addEventListener("message", (event) => {
  if (event.data === "skipWaiting") {
    self.skipWaiting();
  }

  // Allow the app to request cache clearing
  if (event.data === "clearCache") {
    caches.keys().then((keys) =>
      Promise.all(keys.map((key) => caches.delete(key)))
    );
  }

  // Set the current user ID for per-user cache partitioning
  if (event.data && event.data.type === "SET_CURRENT_USER") {
    currentUserId = event.data.userId;
  }

  // Clear a specific user's cache (e.g., on logout)
  if (event.data && event.data.type === "CLEAR_USER_CACHE") {
    const userId = event.data.userId;
    if (userId) {
      caches.delete(`shifter-api-${userId}`);
    }
  }
});
