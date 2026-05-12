"use client";

import { useEffect } from "react";
import { useOnboardingStore } from "@/lib/store/onboardingStore";
import { useStepCompletion } from "@/lib/hooks/useStepCompletion";
import { shouldShowOnboarding } from "@/lib/onboarding/decisions";
import { readOnboardingState } from "@/lib/onboarding/storage";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { getGroups } from "@/lib/api/groups";

interface OnboardingProviderProps {
  children: React.ReactNode;
}

export default function OnboardingProvider({ children }: OnboardingProviderProps) {
  const userId = useAuthStore((s) => s.userId);
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);
  const { isVisible, show, hydrate, setSteps } = useOnboardingStore();
  const { steps: completionSteps, refresh } = useStepCompletion();

  // Initial mount: hydrate store and determine if onboarding should show
  useEffect(() => {
    if (!userId) return;

    hydrate(userId);

    if (!currentSpaceId) return;

    let cancelled = false;

    async function detect() {
      try {
        const groups = await getGroups(currentSpaceId!);
        const groupCount = groups.length;
        const storageState = readOnboardingState(userId!);

        if (cancelled) return;

        if (shouldShowOnboarding({ groupCount, storageState })) {
          show();
          refresh();
        }
      } catch {
        // Silently fail — onboarding is non-critical
      }
    }

    detect();

    return () => {
      cancelled = true;
    };
  }, [userId, currentSpaceId, hydrate, show, refresh]);

  // When step completion data comes back, persist to store
  useEffect(() => {
    if (!userId) return;

    // Only update if at least one step has a value (refresh has completed)
    const hasData = Object.values(completionSteps).some(Boolean);
    if (hasData) {
      setSteps(userId, completionSteps);
    }
  }, [userId, completionSteps, setSteps]);

  // Re-evaluate when panel becomes visible (user may have completed steps externally)
  useEffect(() => {
    if (!isVisible || !userId || !currentSpaceId) return;

    refresh();
  }, [isVisible, userId, currentSpaceId, refresh]);

  return <>{children}</>;
}
