"use client";

import { useTranslations } from "next-intl";
import { usePushSubscription } from "@/lib/hooks/usePushSubscription";

interface PushNotificationSettingsProps {
  spaceId: string;
}

export default function PushNotificationSettings({ spaceId }: PushNotificationSettingsProps) {
  const t = useTranslations("profile.push");
  const { isSupported, permission, isSubscribed, isLoading, subscribe, unsubscribe } =
    usePushSubscription(spaceId);

  if (!isSupported) {
    return (
      <div className="space-y-2">
        <h2 className="text-sm font-semibold text-slate-900">{t("title")}</h2>
        <p className="text-xs text-slate-500">{t("notSupported")}</p>
      </div>
    );
  }

  if (permission === "denied") {
    return (
      <div className="space-y-2">
        <h2 className="text-sm font-semibold text-slate-900">{t("title")}</h2>
        <p className="text-xs text-slate-500">{t("enableDescription")}</p>
        <div className="rounded-lg bg-amber-50 border border-amber-200 px-3 py-2">
          <p className="text-xs text-amber-700">{t("permissionDenied")}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-sm font-semibold text-slate-900">{t("title")}</h2>
        <p className="text-xs text-slate-500 mt-1">{t("enableDescription")}</p>
      </div>

      <div className="flex items-center justify-between py-2.5 border-b border-slate-100">
        <div className="flex items-center gap-3">
          <span className="text-base">🔔</span>
          <div>
            <p className="text-sm font-medium text-slate-800">{t("enableLabel")}</p>
            <p className="text-xs text-slate-400">{t("enableDescription")}</p>
          </div>
        </div>
        <ToggleSwitch
          checked={isSubscribed}
          disabled={isLoading}
          onChange={() => (isSubscribed ? unsubscribe() : subscribe())}
        />
      </div>
    </div>
  );
}

function ToggleSwitch({
  checked,
  disabled,
  onChange,
}: {
  checked: boolean;
  disabled: boolean;
  onChange: () => void;
}) {
  return (
    <button
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={onChange}
      className={`relative inline-flex h-[22px] w-[40px] items-center rounded-full transition-colors flex-shrink-0 ${
        disabled
          ? "opacity-50 cursor-not-allowed"
          : "cursor-pointer"
      } ${checked ? "bg-sky-500" : "bg-slate-300 dark:bg-slate-500"}`}
    >
      <span
        className={`absolute h-[16px] w-[16px] rounded-full bg-white shadow transition-all ${
          checked ? "left-[21px]" : "left-[3px]"
        }`}
      />
    </button>
  );
}
