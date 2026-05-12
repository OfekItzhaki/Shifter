"use client";

import { useEffect, useState } from "react";

interface SWState {
  isSupported: boolean;
  isRegistered: boolean;
  isOffline: boolean;
  updateAvailable: boolean;
  update: () => void;
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

    // Track online/offline status
    const handleOnline = () => setIsOffline(false);
    const handleOffline = () => setIsOffline(true);
    setIsOffline(!navigator.onLine);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    // Register service worker with version query param to force re-download on deploy
    const appVersion = process.env.NEXT_PUBLIC_APP_VERSION || "1.5.0";
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
    }
  }

  return { isSupported, isRegistered, isOffline, updateAvailable, update };
}
