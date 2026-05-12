"use client";

import AppShell from "@/components/shell/AppShell";
import { useTranslations } from "next-intl";

type ChangeType = "new" | "fix" | "improved";

interface Change {
  type: ChangeType;
  text: string;
}

interface ChangelogEntry {
  version: string;
  date: string;
  changes: Change[];
}

const CHANGELOG: ChangelogEntry[] = [
  {
    version: "1.5.0",
    date: "2026-05-12",
    changes: [
      { type: "new", text: "Onboarding wizard for new users" },
      { type: "new", text: "24h/AM-PM time format toggle in profile" },
      { type: "new", text: "Push notifications for schedule updates" },
      { type: "new", text: "Email verification flow" },
      { type: "improved", text: "Smooth page transitions" },
      { type: "improved", text: "Deep health checks for monitoring" },
      { type: "fix", text: "Docker build compatibility with Alpine Linux" },
    ],
  },
  {
    version: "1.4.0",
    date: "2026-04-28",
    changes: [
      { type: "new", text: "Manual import fallback (CSV/Excel)" },
      { type: "new", text: "Custom error pages (404, 403, 500)" },
      { type: "new", text: "Group ownership transfer" },
      { type: "improved", text: "Schedule solver performance" },
      { type: "fix", text: "Schedule publish notification timing" },
    ],
  },
  {
    version: "1.3.0",
    date: "2026-04-15",
    changes: [
      { type: "new", text: "Dark mode support" },
      { type: "new", text: "Group join codes" },
      { type: "new", text: "Statistics dashboard with burden tracking" },
      { type: "improved", text: "Mobile responsive layout" },
      { type: "fix", text: "RTL layout issues in Hebrew" },
    ],
  },
  {
    version: "1.2.0",
    date: "2026-04-01",
    changes: [
      { type: "new", text: "AI-powered schedule import" },
      { type: "new", text: "Group alerts and messaging" },
      { type: "new", text: "Member qualifications system" },
      { type: "improved", text: "Constraint management UI" },
    ],
  },
  {
    version: "1.1.0",
    date: "2026-03-15",
    changes: [
      { type: "new", text: "Multi-language support (English, Hebrew, Russian)" },
      { type: "new", text: "Profile image upload" },
      { type: "new", text: "Schedule export (CSV/PDF)" },
      { type: "fix", text: "Token refresh race condition" },
    ],
  },
  {
    version: "1.0.0",
    date: "2026-03-01",
    changes: [
      { type: "new", text: "Initial release" },
      { type: "new", text: "Automatic shift scheduling with OR-Tools solver" },
      { type: "new", text: "Group and member management" },
      { type: "new", text: "Constraint-based scheduling" },
      { type: "new", text: "Schedule versioning and rollback" },
    ],
  },
];

const BADGE_STYLES: Record<ChangeType, string> = {
  new: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300",
  fix: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
  improved: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
};

export default function ChangelogPage() {
  const t = useTranslations();

  return (
    <AppShell>
      <div className="max-w-3xl mx-auto">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-6">
          {t("changelog.title")}
        </h1>

        <div className="flex flex-col gap-6">
          {CHANGELOG.map((entry) => (
            <div
              key={entry.version}
              className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 p-6"
            >
              <div className="flex items-baseline gap-3 mb-1">
                <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                  v{entry.version}
                </h2>
              </div>
              <p className="text-sm text-slate-500 dark:text-slate-400 mb-4">
                {entry.date}
              </p>

              <ul className="flex flex-col gap-2">
                {entry.changes.map((change, idx) => (
                  <li key={idx} className="flex items-start gap-2">
                    <span
                      className={`inline-block px-2 py-0.5 rounded text-xs font-medium shrink-0 mt-0.5 ${BADGE_STYLES[change.type]}`}
                    >
                      {t(`changelog.${change.type}`)}
                    </span>
                    <span className="text-sm text-slate-700 dark:text-slate-300">
                      {change.text}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </AppShell>
  );
}
