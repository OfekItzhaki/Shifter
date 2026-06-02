"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter, usePathname } from "next/navigation";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { getMySpaces, migrateUserSpace } from "@/lib/api/spaces";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";

/**
 * useSpaceGuard — Central redirect logic for space membership.
 *
 * Behaviors:
 * 1. If user has NO spaces → attempt migration (if user has groups), then redirect to /onboarding
 * 2. If user HAS spaces → ensure a valid space is selected in spaceStore
 * 3. If user is on /onboarding but has spaces → redirect to /home
 * 4. If user is removed from all spaces → clear spaceStore, redirect to /onboarding
 * 5. For existing users with groups but no spaces → trigger migrateUserSpace() API
 */
export function useSpaceGuard() {
  const router = useRouter();
  const pathname = usePathname();
  const { currentSpaceId, setCurrentSpace, clearSpace } = useSpaceStore();
  const { isLoggedIn, isHydrated } = useEffectiveAuth();
  const [isReady, setIsReady] = useState(false);
  const guardRan = useRef(false);

  useEffect(() => {
    if (!isHydrated) return;

    // Only run for authenticated users
    if (!isLoggedIn) {
      setIsReady(true);
      return;
    }

    // Prevent double-execution in strict mode
    if (guardRan.current) return;
    guardRan.current = true;

    async function checkSpaceMembership() {
      try {
        const spaces = await getMySpaces();

        if (spaces.length === 0) {
          // User has no spaces — attempt migration for existing users with groups
          try {
            const migrationResult = await migrateUserSpace();
            if (migrationResult.spaceId && !migrationResult.alreadyMigrated) {
              // Migration created a new space — select it and go to /home
              setCurrentSpace(migrationResult.spaceId, migrationResult.spaceName ?? "My Space");
              if (pathname !== "/home") {
                router.replace("/home");
              }
              setIsReady(true);
              return;
            }
          } catch {
            // Migration failed or user has no groups to migrate — continue to onboarding
          }

          // No spaces and migration didn't help → clear store and redirect to onboarding
          clearSpace();
          if (pathname !== "/onboarding") {
            router.replace("/onboarding");
          }
          setIsReady(true);
          return;
        }

        // User HAS spaces
        // If on /onboarding but has spaces → redirect to /home
        if (pathname === "/onboarding") {
          // Ensure a valid space is selected
          const storedIsValid = currentSpaceId && spaces.some(s => s.id === currentSpaceId);
          if (!storedIsValid) {
            setCurrentSpace(spaces[0].id, spaces[0].name);
          }
          router.replace("/home");
          setIsReady(true);
          return;
        }

        // Ensure valid space selected
        const storedIsValid = currentSpaceId && spaces.some(s => s.id === currentSpaceId);
        if (!storedIsValid) {
          // Fall back to most recently available space (first in list)
          setCurrentSpace(spaces[0].id, spaces[0].name);
        }

        setIsReady(true);
      } catch {
        // Network error — allow page to render with whatever state exists
        setIsReady(true);
      }
    }

    checkSpaceMembership();

    return () => {
      guardRan.current = false;
    };
  }, [isHydrated, isLoggedIn, pathname]);

  return { isReady };
}
