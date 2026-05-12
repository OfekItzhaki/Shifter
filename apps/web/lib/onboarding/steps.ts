import type { StepCompletionMap } from "./storage";

export interface OnboardingStepConfig {
  key: keyof StepCompletionMap;
  titleKey: string;
  descriptionKey: string;
  ctaLabelKey: string;
  icon: string;
}

export const ONBOARDING_STEPS: OnboardingStepConfig[] = [
  {
    key: "createGroup",
    titleKey: "onboarding.steps.createGroup.title",
    descriptionKey: "onboarding.steps.createGroup.description",
    ctaLabelKey: "onboarding.steps.createGroup.cta",
    icon: "👥",
  },
  {
    key: "addMembers",
    titleKey: "onboarding.steps.addMembers.title",
    descriptionKey: "onboarding.steps.addMembers.description",
    ctaLabelKey: "onboarding.steps.addMembers.cta",
    icon: "➕",
  },
  {
    key: "defineTasks",
    titleKey: "onboarding.steps.defineTasks.title",
    descriptionKey: "onboarding.steps.defineTasks.description",
    ctaLabelKey: "onboarding.steps.defineTasks.cta",
    icon: "📋",
  },
  {
    key: "setConstraints",
    titleKey: "onboarding.steps.setConstraints.title",
    descriptionKey: "onboarding.steps.setConstraints.description",
    ctaLabelKey: "onboarding.steps.setConstraints.cta",
    icon: "⚙️",
  },
  {
    key: "runSolver",
    titleKey: "onboarding.steps.runSolver.title",
    descriptionKey: "onboarding.steps.runSolver.description",
    ctaLabelKey: "onboarding.steps.runSolver.cta",
    icon: "🚀",
  },
];
