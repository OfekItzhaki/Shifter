/**
 * Shifter Service Worker
 * 
 * Strategy:
 * - Schedule data (GET /spaces/*/schedule-versions/current, /my-assignments):
 *   Network-first with cache fallback. Soldiers can view their schedule offline.
 * - Static assets (JS, CSS, images): Cache-first for fast loads.
 * - API mutations (POST, PUT, DELETE): Always network, never cache.
 * - Auth endpoints: Never cache.
 */

const CACHE_NAME = "shifter-v1";
const STATIC_CACHE = "shifter-static-v1";

// Patterns for schedule-related GET requests to cache
const SCHEDULE_PATTERNS = [
  /\/spaces\/[^/]+\/schedule-versions\/current/,
  /\/spaces\/[^/]+\/my-assignments/,
  /\/spaces\/[^/]+\/groups\/[^/]+\/schedule/,
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
  // Clean up old caches
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys
          .filter((key) => key !== CACHE_NAME && key !== STATIC_CACHE)
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

  // Never cache auth endpoints
  if (NEVER_CACHE_PATTERNS.some((p) => p.test(url.pathname))) return;

  // Static assets: cache-first
  if (STATIC_EXTENSIONS.test(url.pathname)) {
    event.respondWith(cacheFirst(request, STATIC_CACHE));
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
      icon: icon || "/favicon.jpeg",
      badge: "/favicon.jpeg",
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
});
