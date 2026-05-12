import type { OnboardingState, StepCompletionMap } from "./storage";

export interface OnboardingContext {
  groupCount: number;
  storageState: OnboardingState | null;
}

/**
 * Determines whether the onboarding wizard should display.
 * Returns true iff groupCount === 0 AND the storage state is neither "completed" nor "dismissed".
 */
export function shouldShowOnboarding(ctx: OnboardingContext): boolean {
  if (ctx.groupCount !== 0) return false;

  const status = ctx.storageState?.status;
  if (status === "completed" || status === "dismissed") return false;

  return true;
}

/**
 * Returns the index of the first incomplete step, or -1 if all complete.
 */
export function getCurrentStepIndex(steps: StepCompletionMap): number {
  const keys: (keyof StepCompletionMap)[] = [
    "createGroup",
    "addMembers",
    "defineTasks",
    "setConstraints",
    "runSolver",
  ];

  for (let i = 0; i < keys.length; i++) {
    if (!steps[keys[i]]) return i;
  }

  return -1;
}

/**
 * Maps a step key to its navigation route.
 * Routes follow the app's existing structure:
 * - createGroup → /spaces/{spaceId}/groups (group creation flow)
 * - addMembers → /groups/{groupId}?tab=members
 * - defineTasks → /groups/{groupId}?tab=tasks
 * - setConstraints → /groups/{groupId}?tab=constraints
 * - runSolver → /groups/{groupId}?tab=schedule
 */
export function getStepRoute(
  stepKey: keyof StepCompletionMap,
  spaceId: string,
  groupId?: string
): string {
  switch (stepKey) {
    case "createGroup":
      return `/spaces/${spaceId}/groups`;
    case "addMembers":
      return groupId
        ? `/groups/${groupId}?tab=members`
        : `/spaces/${spaceId}/groups`;
    case "defineTasks":
      return groupId
        ? `/groups/${groupId}?tab=tasks`
        : `/spaces/${spaceId}/groups`;
    case "setConstraints":
      return groupId
        ? `/groups/${groupId}?tab=constraints`
        : `/spaces/${spaceId}/groups`;
    case "runSolver":
      return groupId
        ? `/groups/${groupId}?tab=schedule`
        : `/spaces/${spaceId}/groups`;
  }
}

/**
 * Computes step completion from application state counts.
 * Each step is true if its corresponding count exceeds the threshold:
 * - createGroup: groupCount > 0
 * - addMembers: memberCount > 1 (owner + at least 1 member)
 * - defineTasks: taskCount > 0
 * - setConstraints: constraintCount > 0
 * - runSolver: solverRunCount > 0
 */
export function computeStepCompletion(appState: {
  groupCount: number;
  memberCount: number;
  taskCount: number;
  constraintCount: number;
  solverRunCount: number;
}): StepCompletionMap {
  return {
    createGroup: appState.groupCount > 0,
    addMembers: appState.memberCount > 1,
    defineTasks: appState.taskCount > 0,
    setConstraints: appState.constraintCount > 0,
    runSolver: appState.solverRunCount > 0,
  };
}
