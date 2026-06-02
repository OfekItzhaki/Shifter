"use client";

import { useEffect, useState } from "react";
import { useAuthStore } from "@/lib/store/authStore";

export function hasStoredAccessToken(): boolean {
  return typeof window !== "undefined" && !!localStorage.getItem("access_token");
}

export function useEffectiveAuth() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [hasAccessToken, setHasAccessToken] = useState(false);
  const [isHydrated, setIsHydrated] = useState(false);

  useEffect(() => {
    const persistApi = useAuthStore.persist;

    function syncAccessToken() {
      setHasAccessToken(hasStoredAccessToken());
    }

    syncAccessToken();
    if (persistApi.hasHydrated()) {
      setIsHydrated(true);
    }

    const unsubHydration = persistApi.onFinishHydration(() => {
      syncAccessToken();
      setIsHydrated(true);
    });

    window.addEventListener("storage", syncAccessToken);
    return () => {
      unsubHydration();
      window.removeEventListener("storage", syncAccessToken);
    };
  }, []);

  return {
    isAuthenticated,
    hasAccessToken,
    isHydrated,
    isLoggedIn: isAuthenticated || hasAccessToken,
  };
}
