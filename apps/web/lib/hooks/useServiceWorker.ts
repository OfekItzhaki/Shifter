"use client";

import { useEffect, useState } from "react";

interface SWState {
  isSupported: boolean;
  isRegistered: boolean;
  isOffline: boolean;
  updateAvailable: boolean;
  update: () => void;
}

function shouldUseServiceWorker(): boolean {
  if (typeof window === "undefined") return false;
  if (process.env.NODE_ENV !== "production") return false;

  const hostname = window.location.hostname;
  return hostname !== "localhost" && hostname !== "127.0.0.1" && hostname !== "::1";
}

async function clearLocalServiceWorkers(): Promise<void> {
  const registrations = await navigator.serviceWorker.getRegistrations();
  await Promise.all(registrations.map((registration) => registration.unregister()));

  if ("caches" in window) {
    const cacheNames = await caches.keys();
    await Promise.all(cacheNames.map((cacheName) => caches.delete(cacheName)));
  }
}

/**
 * Registers the service worker and tracks its state.
 * Call this once at the app root level.
 */
export function useServiceWorker(): SWState {
  const [isRegistered, setIsRegistered] = useState(false);
  const [updateAvailable, setUpdateAvailable] = useState(false);
  const [isOffline, setIsOffline] = useState(false);
  const [registration, setRegistration] = useState<ServiceWorkerRegistration | null>(null);

  const isSupported = typeof window !== "undefined" && "serviceWorker" in navigator;

  useEffect(() => {
    if (!isSupported) return;

    if (!shouldUseServiceWorker()) {
      clearLocalServiceWorkers()
        .then(() => {
          setRegistration(null);
          setIsRegistered(false);
          setUpdateAvailable(false);
        })
        .catch((err) => {
          console.warn("SW cleanup failed:", err);
        });
      return;
    }

    // Track online/offline status
    const handleOnline = () => setIsOffline(false);
    const handleOffline = () => setIsOffline(true);
    setIsOffline(!navigator.onLine);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    // Register service worker with version query param to force re-download on deploy
    const appVersion = process.env.NEXT_PUBLIC_APP_VERSION || "dev";
    navigator.serviceWorker
      .register(`/sw.js?v=${appVersion}`, { scope: "/" })
      .then((reg) => {
        setRegistration(reg);
        setIsRegistered(true);

        // Check for updates periodically (every 60 minutes)
        const interval = setInterval(() => {
          reg.update();
        }, 60 * 60 * 1000);

        // Listen for new service worker waiting to activate
        reg.addEventListener("updatefound", () => {
          const newWorker = reg.installing;
          if (!newWorker) return;

          newWorker.addEventListener("statechange", () => {
            if (newWorker.state === "installed" && navigator.serviceWorker.controller) {
              // New version available
              setUpdateAvailable(true);
            }
          });
        });

        return () => clearInterval(interval);
      })
      .catch((err) => {
        console.warn("SW registration failed:", err);
      });

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [isSupported]);

  function update() {
    if (registration?.waiting) {
      registration.waiting.postMessage("skipWaiting");
      // Reload once the new SW takes over
      navigator.serviceWorker.addEventListener("controllerchange", () => {
        window.location.reload();
      });
      // Fallback: if controllerchange doesn't fire within 2s, force reload
      setTimeout(() => window.location.reload(), 2000);
    } else {
      // No waiting worker — just reload to get the latest
      window.location.reload();
    }
  }

  return { isSupported, isRegistered, isOffline, updateAvailable, update };
}
