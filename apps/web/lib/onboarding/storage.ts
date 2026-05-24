export type OnboardingStatus = "in-progress" | "completed" | "dismissed";

export type StepCompletionMap = {
  createGroup: boolean;
  addMembers: boolean;
  defineTasks: boolean;
  setConstraints: boolean;
  runSolver: boolean;
};

export interface OnboardingState {
  status: OnboardingStatus;
  steps: StepCompletionMap;
}

export const EMPTY_STEPS: StepCompletionMap = {
  createGroup: false,
  addMembers: false,
  defineTasks: false,
  setConstraints: false,
  runSolver: false,
};

/** Returns the localStorage key for a given user */
export function getStorageKey(userId: string, spaceId?: string): string {
  if (spaceId) return `shifter-onboarding-${userId}-${spaceId}`;
  return `shifter-onboarding-${userId}`;
}

/** Reads onboarding state from localStorage. Returns null if not found or on error. */
export function readOnboardingState(userId: string, spaceId?: string): OnboardingState | null {
  try {
    const raw = localStorage.getItem(getStorageKey(userId, spaceId));
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed as OnboardingState;
  } catch {
    return null;
  }
}

/** Writes onboarding state to localStorage. Silently fails if localStorage is unavailable. */
export function writeOnboardingState(userId: string, state: OnboardingState, spaceId?: string): void {
  try {
    localStorage.setItem(getStorageKey(userId, spaceId), JSON.stringify(state));
  } catch {
    // Silently fail — localStorage may be unavailable (private browsing, quota exceeded)
  }
}

/** Computes the overall status from step completions */
export function computeStatus(steps: StepCompletionMap): OnboardingStatus {
  const allComplete = Object.values(steps).every(Boolean);
  return allComplete ? "completed" : "in-progress";
}
