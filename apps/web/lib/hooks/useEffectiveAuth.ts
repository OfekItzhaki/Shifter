"use client";

import { useEffect, useState } from "react";
import { useAuthStore } from "@/lib/store/authStore";
import { AUTH_TOKEN_CHANGED_EVENT, hasStoredAccessToken } from "@/lib/auth/tokenState";

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
    if (!persistApi || persistApi.hasHydrated()) {
      setIsHydrated(true);
    }

    const unsubHydration = persistApi?.onFinishHydration(() => {
      syncAccessToken();
      setIsHydrated(true);
    }) ?? (() => {});

    window.addEventListener("storage", syncAccessToken);
    window.addEventListener(AUTH_TOKEN_CHANGED_EVENT, syncAccessToken);
    return () => {
      unsubHydration();
      window.removeEventListener("storage", syncAccessToken);
      window.removeEventListener(AUTH_TOKEN_CHANGED_EVENT, syncAccessToken);
    };
  }, []);

  return {
    isAuthenticated,
    hasAccessToken,
    isHydrated,
    isLoggedIn: isAuthenticated || hasAccessToken,
  };
}
