"use client";

import { useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";

type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed"; platform: string }>;
};

const DISMISSED_KEY = "shifter-pwa-install-dismissed-at";
const DISMISS_DAYS = 30;

function isStandaloneDisplay(): boolean {
  if (typeof window === "undefined") return false;
  return (
    window.matchMedia("(display-mode: standalone)").matches ||
    window.matchMedia("(display-mode: fullscreen)").matches ||
    (window.navigator as Navigator & { standalone?: boolean }).standalone === true
  );
}

function isIosSafari(): boolean {
  if (typeof window === "undefined") return false;
  const ua = window.navigator.userAgent;
  const isiOS = /iPad|iPhone|iPod/.test(ua) || (ua.includes("Macintosh") && "ontouchend" in document);
  const isSafari = /^((?!CriOS|FxiOS|EdgiOS|OPiOS).)*Safari/i.test(ua);
  return isiOS && isSafari;
}

function recentlyDismissed(): boolean {
  if (typeof window === "undefined") return true;
  const raw = window.localStorage.getItem(DISMISSED_KEY);
  if (!raw) return false;

  const dismissedAt = Number(raw);
  if (!Number.isFinite(dismissedAt)) return false;

  return Date.now() - dismissedAt < DISMISS_DAYS * 24 * 60 * 60 * 1000;
}

export default function PwaInstallPrompt() {
  const t = useTranslations("pwaInstall");
  const [installEvent, setInstallEvent] = useState<BeforeInstallPromptEvent | null>(null);
  const [dismissed, setDismissed] = useState(() => isStandaloneDisplay() || recentlyDismissed());
  const [showIosHint, setShowIosHint] = useState(() => !dismissed && isIosSafari());

  const canShow = useMemo(
    () => !dismissed && !isStandaloneDisplay() && (Boolean(installEvent) || showIosHint),
    [dismissed, installEvent, showIosHint]
  );

  useEffect(() => {
    if (dismissed || isStandaloneDisplay()) return;

    function handleBeforeInstallPrompt(event: Event) {
      event.preventDefault();
      setInstallEvent(event as BeforeInstallPromptEvent);
      setShowIosHint(false);
    }

    function handleAppInstalled() {
      setInstallEvent(null);
      setShowIosHint(false);
      setDismissed(true);
    }

    window.addEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
    window.addEventListener("appinstalled", handleAppInstalled);

    return () => {
      window.removeEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
      window.removeEventListener("appinstalled", handleAppInstalled);
    };
  }, [dismissed]);

  async function install() {
    if (!installEvent) return;
    await installEvent.prompt();
    const choice = await installEvent.userChoice;
    if (choice.outcome === "accepted") {
      setDismissed(true);
    }
    setInstallEvent(null);
  }

  function dismiss() {
    window.localStorage.setItem(DISMISSED_KEY, String(Date.now()));
    setDismissed(true);
  }

  if (!canShow) return null;

  return (
    <div className="fixed bottom-5 left-5 right-5 z-[1100] sm:left-auto sm:right-5 sm:max-w-sm">
      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-2xl shadow-slate-900/20 dark:border-slate-700 dark:bg-slate-900">
        <div className="flex items-start gap-3">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-sky-50 text-sky-600 dark:bg-sky-900/40 dark:text-sky-300">
            <span aria-hidden="true" className="text-lg font-bold">S</span>
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold text-slate-900 dark:text-white">{t("title")}</p>
            <p className="mt-1 text-xs leading-5 text-slate-600 dark:text-slate-300">
              {showIosHint ? t("iosDescription") : t("description")}
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              {installEvent && (
                <button
                  type="button"
                  onClick={install}
                  className="rounded-xl bg-sky-500 px-3 py-2 text-xs font-semibold text-white transition-colors hover:bg-sky-600"
                >
                  {t("install")}
                </button>
              )}
              <button
                type="button"
                onClick={dismiss}
                className="rounded-xl border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600 transition-colors hover:border-slate-300 hover:text-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:border-slate-600 dark:hover:text-white"
              >
                {t("notNow")}
              </button>
            </div>
          </div>
          <button
            type="button"
            onClick={dismiss}
            aria-label={t("dismiss")}
            className="rounded-lg px-2 py-1 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800 dark:hover:text-slate-200"
          >
            x
          </button>
        </div>
      </div>
    </div>
  );
}
