"use client";

import { useTranslations } from "next-intl";
import CycleControlPanel from "./CycleControlPanel";

type SelfServiceOpsTarget =
  | "absence-reports"
  | "admin-overrides"
  | "shift-templates"
  | "self-service-config"
  | "waitlist";

interface SelfServiceOperationsTabProps {
  spaceId: string;
  groupId: string;
  onNavigate: (tab: SelfServiceOpsTarget) => void;
}

const ACTIONS: { target: SelfServiceOpsTarget; key: string }[] = [
  { target: "absence-reports", key: "reviews" },
  { target: "waitlist", key: "waitlist" },
  { target: "admin-overrides", key: "overrides" },
  { target: "shift-templates", key: "templates" },
  { target: "self-service-config", key: "policy" },
];

export default function SelfServiceOperationsTab({
  spaceId,
  groupId,
  onNavigate,
}: SelfServiceOperationsTabProps) {
  const t = useTranslations("selfService.operations");

  return (
    <div className="space-y-5">
      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="max-w-3xl">
          <h2 className="text-base font-semibold text-slate-900">{t("title")}</h2>
          <p className="mt-1 text-sm text-slate-500">{t("description")}</p>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {ACTIONS.map((action) => (
            <button
              key={action.target}
              type="button"
              onClick={() => onNavigate(action.target)}
              className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-left transition-colors hover:border-sky-200 hover:bg-sky-50 focus:outline-none focus:ring-2 focus:ring-sky-400"
            >
              <span className="block text-sm font-semibold text-slate-900">
                {t(`actions.${action.key}.title`)}
              </span>
              <span className="mt-1 block text-xs leading-5 text-slate-500">
                {t(`actions.${action.key}.description`)}
              </span>
            </button>
          ))}
        </div>
      </div>

      <CycleControlPanel spaceId={spaceId} groupId={groupId} onNavigate={onNavigate} />
    </div>
  );
}
