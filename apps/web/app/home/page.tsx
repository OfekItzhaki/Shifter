"use client";

import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useOnboardingStore } from "@/lib/store/onboardingStore";
import { useStepCompletion } from "@/lib/hooks/useStepCompletion";
import AppShell from "@/components/shell/AppShell";
import Link from "next/link";

function HomeIcon({ d, className }: { d: string; className?: string }) {
  return (
    <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8} className={className}>
      <path strokeLinecap="round" strokeLinejoin="round" d={d} />
    </svg>
  );
}

function HomePage() {
  const t = useTranslations("home");
  const { displayName, userId } = useAuthStore();
  const { show: showOnboarding, reset: resetOnboarding } = useOnboardingStore();
  const { refresh: refreshSteps } = useStepCompletion();

  const today = new Date();
  const hebrewDate = today.toLocaleDateString("he-IL", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });

  function handleRestartOnboarding() {
    if (!userId) return;
    resetOnboarding(userId);
    showOnboarding();
    refreshSteps();
  }

  const tips = [
    { label: t("tipCreateGroup"), href: "/groups", icon: "M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" },
    { label: t("tipAddMembers"), href: "/groups", icon: "M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" },
    { label: t("tipDefineTasks"), href: "/groups", icon: "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" },
    { label: t("tipRunSolver"), href: "/groups", icon: "M13 10V3L4 14h7v7l9-11h-7z" },
  ];

  return (
    <div className="w-full max-w-3xl mx-auto py-6 px-4 space-y-6">
      {/* Welcome section */}
      <div className="space-y-1">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
          {t("welcome", { name: displayName ?? "" })}
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">{hebrewDate}</p>
      </div>

      {/* What's New card */}
      <div className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-5 space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-slate-900 dark:text-white flex items-center gap-2">
            <span>✨</span>
            {t("whatsNew")}
          </h2>
          <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300">
            v{process.env.NEXT_PUBLIC_APP_VERSION ?? "1.5.0"}
          </span>
        </div>
        <p className="text-sm text-slate-600 dark:text-slate-300">
          {t("latestVersion")}: v{process.env.NEXT_PUBLIC_APP_VERSION ?? "1.5.0"}
        </p>
        <Link
          href="/changelog"
          className="inline-flex items-center gap-1 text-sm text-blue-600 dark:text-blue-400 hover:underline"
        >
          {t("viewAllUpdates")}
          <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </Link>
      </div>

      {/* Getting Started card */}
      <div className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-5 space-y-4">
        <h2 className="text-base font-semibold text-slate-900 dark:text-white flex items-center gap-2">
          <span>🚀</span>
          {t("gettingStarted")}
        </h2>
        <ul className="space-y-3">
          {tips.map((tip, i) => (
            <li key={i}>
              <Link
                href={tip.href}
                className="flex items-center gap-3 p-2.5 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors group"
              >
                <span className="flex-shrink-0 w-8 h-8 rounded-lg bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
                  <HomeIcon d={tip.icon} />
                </span>
                <span className="text-sm font-medium text-slate-700 dark:text-slate-200 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">
                  {tip.label}
                </span>
              </Link>
            </li>
          ))}
        </ul>
        <button
          onClick={handleRestartOnboarding}
          className="inline-flex items-center gap-1 text-sm text-blue-600 dark:text-blue-400 hover:underline bg-transparent border-none cursor-pointer p-0"
        >
          {t("viewFullGuide")}
          <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </button>
      </div>

      {/* Quick Actions */}
      <div className="space-y-3">
        <h2 className="text-base font-semibold text-slate-900 dark:text-white">
          {t("quickActions")}
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Link
            href="/schedule/my-missions"
            className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-5 hover:border-blue-300 dark:hover:border-blue-600 hover:shadow-sm transition-all group no-underline"
          >
            <div className="flex items-center gap-3">
              <span className="flex-shrink-0 w-10 h-10 rounded-xl bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
                <HomeIcon d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" />
              </span>
              <span className="text-sm font-semibold text-slate-800 dark:text-slate-100 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">
                {t("myMissions")}
              </span>
            </div>
          </Link>
          <Link
            href="/groups"
            className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-5 hover:border-blue-300 dark:hover:border-blue-600 hover:shadow-sm transition-all group no-underline"
          >
            <div className="flex items-center gap-3">
              <span className="flex-shrink-0 w-10 h-10 rounded-xl bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
                <HomeIcon d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
              </span>
              <span className="text-sm font-semibold text-slate-800 dark:text-slate-100 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">
                {t("myGroups")}
              </span>
            </div>
          </Link>
        </div>
      </div>
    </div>
  );
}

export default function HomeDashboardPage() {
  return (
    <AppShell>
      <HomePage />
    </AppShell>
  );
}
