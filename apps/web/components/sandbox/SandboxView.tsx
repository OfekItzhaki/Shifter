"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useSandboxStore } from "@/lib/store/sandboxStore";
import { useAuthStore } from "@/lib/store/authStore";
import SandboxSettingsPanel from "./SandboxSettingsPanel";
import SandboxSchedulePreview from "./SandboxSchedulePreview";

/**
 * SandboxView — Split-view container for the simulation sandbox.
 *
 * Renders the SandboxSettingsPanel (left) and SandboxSchedulePreview (right)
 * as independent React components with separate state subscriptions.
 *
 * This component subscribes ONLY to `isActive` and `groupId` from the sandbox store.
 * It does NOT subscribe to override state or simulation results, ensuring
 * that neither child component causes the other to re-render.
 *
 * The split view is rendered as a full-screen overlay when the sandbox is active,
 * replacing the normal group page content.
 *
 * Access control: If the user is not an admin for the sandbox's group, the sandbox
 * is exited and the user is redirected to the forbidden page.
 *
 * Requirements: 8.1, 8.2, 8.3, 8.4, 11.1, 11.2
 */
export default function SandboxView() {
  const t = useTranslations("sandbox");
  const router = useRouter();
  const isActive = useSandboxStore((s) => s.isActive);
  const sandboxGroupId = useSandboxStore((s) => s.groupId);
  const exitSandbox = useSandboxStore((s) => s.exitSandbox);
  const adminGroupId = useAuthStore((s) => s.adminGroupId);

  // Access control guard: redirect unauthorized users (Req 11.1, 11.2)
  useEffect(() => {
    if (isActive && sandboxGroupId && adminGroupId !== sandboxGroupId) {
      exitSandbox();
      router.push("/error/forbidden");
    }
  }, [isActive, sandboxGroupId, adminGroupId, exitSandbox, router]);

  if (!isActive) return null;

  // Don't render sandbox content if user is not admin for this group
  if (sandboxGroupId && adminGroupId !== sandboxGroupId) return null;

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-slate-50 dark:bg-slate-900">
      {/* Header bar */}
      <div className="flex items-center gap-3 px-4 py-3 border-b border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 shrink-0">
        <div className="flex items-center gap-2">
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-purple-100 dark:bg-purple-900/40 text-purple-700 dark:text-purple-300 border border-purple-200 dark:border-purple-700">
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="text-purple-500">
              <path strokeLinecap="round" strokeLinejoin="round" d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z" />
            </svg>
            {t("sandboxBadge")}
          </span>
          <h1 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            {t("title")}
          </h1>
        </div>
      </div>

      {/* Split view: Settings (left) + Preview (right) */}
      <div className="flex flex-1 min-h-0">
        {/* Settings panel — left side */}
        <div className="w-[420px] shrink-0 overflow-hidden border-r border-slate-200 dark:border-slate-700">
          <SandboxSettingsPanel />
        </div>

        {/* Schedule preview — right side */}
        <div className="flex-1 overflow-hidden">
          <SandboxSchedulePreview />
        </div>
      </div>
    </div>
  );
}
