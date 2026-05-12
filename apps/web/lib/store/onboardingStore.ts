import { create } from "zustand";
import type { OnboardingStatus, StepCompletionMap } from "@/lib/onboarding/storage";
import {
  EMPTY_STEPS,
  readOnboardingState,
  writeOnboardingState,
  computeStatus,
} from "@/lib/onboarding/storage";

interface OnboardingStoreState {
  isVisible: boolean;
  steps: StepCompletionMap;
  status: OnboardingStatus;

  show: () => void;
  hide: () => void;
  dismiss: (userId: string) => void;
  completeStep: (userId: string, stepKey: keyof StepCompletionMap) => void;
  reset: (userId: string) => void;
  hydrate: (userId: string) => void;
  setSteps: (userId: string, steps: StepCompletionMap) => void;
}

export const useOnboardingStore = create<OnboardingStoreState>()((set, get) => ({
  isVisible: false,
  steps: { ...EMPTY_STEPS },
  status: "in-progress",

  show: () => set({ isVisible: true }),

  hide: () => set({ isVisible: false }),

  dismiss: (userId: string) => {
    const steps = get().steps;
    writeOnboardingState(userId, { status: "dismissed", steps });
    set({ isVisible: false, status: "dismissed" });
  },

  completeStep: (userId: string, stepKey: keyof StepCompletionMap) => {
    const steps = { ...get().steps, [stepKey]: true };
    const status = computeStatus(steps);
    writeOnboardingState(userId, { status, steps });
    set({ steps, status });
  },

  reset: (userId: string) => {
    const steps = { ...EMPTY_STEPS };
    const status: OnboardingStatus = "in-progress";
    writeOnboardingState(userId, { status, steps });
    set({ steps, status });
  },

  hydrate: (userId: string) => {
    const state = readOnboardingState(userId);
    if (state) {
      set({ steps: state.steps, status: state.status });
    } else {
      set({ steps: { ...EMPTY_STEPS }, status: "in-progress" });
    }
  },

  setSteps: (userId: string, steps: StepCompletionMap) => {
    const status = computeStatus(steps);
    writeOnboardingState(userId, { status, steps });
    set({ steps, status });
  },
}));
