"use client";

import { useEffect, useCallback } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useOnboardingStore } from "@/lib/store/onboardingStore";
import { ONBOARDING_STEPS } from "@/lib/onboarding/steps";
import { getCurrentStepIndex, getStepRoute } from "@/lib/onboarding/decisions";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";

export default function OnboardingPanel() {
  const t = useTranslations("onboarding");
  const router = useRouter();

  const { isVisible, steps, status, dismiss } = useOnboardingStore();
  const userId = useAuthStore((s) => s.userId);
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);

  const currentStepIndex = getCurrentStepIndex(steps);
  const allComplete = currentStepIndex === -1;

  // Derive the first group ID from the step route logic — if createGroup is done,
  // subsequent steps need a groupId. We pass undefined and let getStepRoute handle fallback.
  const firstGroupId: string | undefined = undefined;

  const handleDismiss = useCallback(() => {
    if (userId) {
      dismiss(userId);
    }
  }, [userId, dismiss]);

  // Keyboard: dismiss on Escape
  useEffect(() => {
    if (!isVisible) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        handleDismiss();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isVisible, handleDismiss]);

  if (!isVisible) return null;

  return (
    <div
      role="dialog"
      aria-label={t("title")}
      className="fixed z-50 bg-white dark:bg-slate-800 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-700 flex flex-col overflow-hidden transition-all duration-300 ease-in-out"
      style={{
        insetInlineEnd: 24,
        bottom: 24,
        maxWidth: 340,
        width: "calc(100vw - 48px)",
      }}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100 dark:border-slate-700">
        <h2 className="text-base font-semibold text-slate-900 dark:text-white">
          {t("title")}
        </h2>
        <button
          onClick={handleDismiss}
          className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors"
          aria-label={t("dismiss")}
        >
          <svg
            width="16"
            height="16"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M6 18L18 6M6 6l12 12"
            />
          </svg>
        </button>
      </div>

      {/* Step list or success state */}
      {allComplete || status === "completed" ? (
        <div className="px-5 py-6 text-center">
          <div className="text-3xl mb-3">🎉</div>
          <h3 className="text-base font-semibold text-slate-900 dark:text-white mb-1">
            {t("success")}
          </h3>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {t("successMessage")}
          </p>
        </div>
      ) : (
        <ol
          className="flex flex-col gap-1 px-3 py-3 overflow-y-auto max-h-[360px]"
          aria-live="polite"
        >
          {ONBOARDING_STEPS.map((step, index) => {
            const isCompleted = steps[step.key];
            const isCurrent = index === currentStepIndex;

            return (
              <li
                key={step.key}
                className={`flex gap-3 rounded-xl px-3 py-3 transition-colors duration-200 ${
                  isCurrent
                    ? "bg-sky-50 dark:bg-sky-900/20 border border-sky-200 dark:border-sky-800"
                    : "border border-transparent"
                }`}
              >
                {/* Step indicator */}
                <div
                  className={`flex-shrink-0 w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold transition-colors ${
                    isCompleted
                      ? "bg-green-100 dark:bg-green-900/40 text-green-600 dark:text-green-400"
                      : isCurrent
                        ? "bg-sky-500 text-white"
                        : "bg-slate-100 dark:bg-slate-700 text-slate-400 dark:text-slate-500"
                  }`}
                >
                  {isCompleted ? (
                    <svg
                      width="14"
                      height="14"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                      strokeWidth={3}
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        d="M5 13l4 4L19 7"
                      />
                    </svg>
                  ) : (
                    index + 1
                  )}
                </div>

                {/* Step content */}
                <div className="flex-1 min-w-0">
                  <p
                    className={`text-sm font-medium leading-tight ${
                      isCompleted
                        ? "text-slate-400 dark:text-slate-500 line-through"
                        : isCurrent
                          ? "text-slate-900 dark:text-white"
                          : "text-slate-600 dark:text-slate-300"
                    }`}
                  >
                    {t(`steps.${step.key}.title`)}
                  </p>

                  {/* Description — only for current step (progressive disclosure) */}
                  {isCurrent && (
                    <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                      {t(`steps.${step.key}.description`)}
                    </p>
                  )}

                  {/* CTA button — only for current step */}
                  {isCurrent && currentSpaceId && (
                    <button
                      onClick={() => {
                        const route = getStepRoute(
                          step.key,
                          currentSpaceId,
                          firstGroupId
                        );
                        router.push(route);
                      }}
                      className="mt-2 inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white bg-sky-500 hover:bg-sky-600 rounded-lg transition-colors"
                    >
                      {t(`steps.${step.key}.cta`)}
                      <svg
                        width="12"
                        height="12"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                        strokeWidth={2.5}
                        className="rtl:rotate-180"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          d="M9 5l7 7-7 7"
                        />
                      </svg>
                    </button>
                  )}
                </div>
              </li>
            );
          })}
        </ol>
      )}

      {/* Progress bar */}
      <div className="px-5 py-3 border-t border-slate-100 dark:border-slate-700">
        <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 mb-1.5">
          <span>
            {Object.values(steps).filter(Boolean).length}/{ONBOARDING_STEPS.length}
          </span>
        </div>
        <div className="h-1.5 bg-slate-100 dark:bg-slate-700 rounded-full overflow-hidden">
          <div
            className="h-full bg-sky-500 rounded-full transition-all duration-500 ease-out"
            style={{
              width: `${(Object.values(steps).filter(Boolean).length / ONBOARDING_STEPS.length) * 100}%`,
            }}
          />
        </div>
      </div>
    </div>
  );
}
