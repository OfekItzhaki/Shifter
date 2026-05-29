"use client";

import { useState, useEffect } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { createSpace, joinSpaceByCode, getMySpaces } from "@/lib/api/spaces";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";

type Step = "choose" | "create" | "join";

export default function OnboardingPage() {
  const t = useTranslations("spaceOnboarding");
  const locale = useLocale();
  const router = useRouter();
  const { setCurrentSpace } = useSpaceStore();
  const { userId } = useAuthStore();
  const isRtl = locale === "he";
  const backArrow = locale === "he" ? "→" : "←";

  const [step, setStep] = useState<Step>("choose");
  const [spaceName, setSpaceName] = useState("");
  const [inviteCode, setInviteCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attempts, setAttempts] = useState(0);
  const [cooldownUntil, setCooldownUntil] = useState<number | null>(null);

  // Redirect if user already has spaces
  useEffect(() => {
    getMySpaces().then(spaces => {
      if (spaces.length > 0) {
        setCurrentSpace(spaces[0].id, spaces[0].name);
        router.replace("/home");
      }
    }).catch(() => {});
  }, []);

  const isCoolingDown = cooldownUntil !== null && Date.now() < cooldownUntil;

  async function handleCreateSpace() {
    const trimmed = spaceName.trim();
    if (trimmed.length < 2 || trimmed.length > 100) {
      setError(t("nameValidation"));
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const { spaceId } = await createSpace(trimmed, null, locale);
      setCurrentSpace(spaceId, trimmed);
      router.replace("/home");
    } catch (e: any) {
      setError(e?.response?.data?.message ?? t("createError"));
    } finally {
      setLoading(false);
    }
  }

  async function handleJoinSpace() {
    const code = inviteCode.trim().toUpperCase();
    if (!/^[A-Z0-9]{8}$/.test(code)) {
      setError(t("codeValidation"));
      return;
    }

    if (isCoolingDown) return;

    setLoading(true);
    setError(null);
    try {
      const result = await joinSpaceByCode(code);
      setCurrentSpace(result.spaceId, result.spaceName);
      router.replace("/home");
    } catch (e: any) {
      const newAttempts = attempts + 1;
      setAttempts(newAttempts);
      if (newAttempts >= 5) {
        setCooldownUntil(Date.now() + 60000);
        setError(t("tooManyAttempts"));
        setTimeout(() => {
          setCooldownUntil(null);
          setAttempts(0);
        }, 60000);
      } else {
        setError(e?.response?.data?.message ?? t("invalidCode"));
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", direction: isRtl ? "rtl" : "ltr" }}
      className="bg-slate-50 dark:bg-slate-900"
    >
      <div style={{ width: "100%", maxWidth: 440, padding: "2rem" }}>
        {/* Logo */}
        <div style={{ textAlign: "center", marginBottom: "2rem" }}>
          <div style={{ display: "flex", justifyContent: "center", marginBottom: 12 }}>
            <ShifterLogo size={48} />
          </div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
            {t("title")}
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            {t("subtitle")}
          </p>
        </div>

        {/* Card */}
        <div
          className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm"
          style={{ borderRadius: 16, padding: "1.5rem" }}
        >
          {step === "choose" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              <button
                onClick={() => setStep("create")}
                className="w-full p-4 rounded-xl border-2 border-slate-200 dark:border-slate-600 hover:border-sky-400 dark:hover:border-sky-500 bg-white dark:bg-slate-700 transition-all text-start"
              >
                <div className="font-semibold text-slate-900 dark:text-white text-sm">
                  {t("createNew")}
                </div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                  {t("createNewDesc")}
                </div>
              </button>

              <button
                onClick={() => setStep("join")}
                className="w-full p-4 rounded-xl border-2 border-slate-200 dark:border-slate-600 hover:border-sky-400 dark:hover:border-sky-500 bg-white dark:bg-slate-700 transition-all text-start"
              >
                <div className="font-semibold text-slate-900 dark:text-white text-sm">
                  {t("joinExisting")}
                </div>
                <div className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                  {t("joinExistingDesc")}
                </div>
              </button>
            </div>
          )}

          {step === "create" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div>
                <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                  {t("spaceNameLabel")}
                </label>
                <input
                  type="text"
                  value={spaceName}
                  onChange={e => { setSpaceName(e.target.value); setError(null); }}
                  placeholder={t("spaceNamePlaceholder")}
                  maxLength={100}
                  className="w-full px-3 py-2.5 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-sky-500"
                  autoFocus
                  onKeyDown={e => e.key === "Enter" && handleCreateSpace()}
                />
              </div>

              {error && (
                <p className="text-xs text-red-500">{error}</p>
              )}

              <button
                onClick={handleCreateSpace}
                disabled={loading || spaceName.trim().length < 2}
                className="w-full py-2.5 rounded-lg bg-sky-500 hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600 text-white font-semibold text-sm transition-colors"
              >
                {loading ? t("creating") : t("createButton")}
              </button>

              <button
                onClick={() => { setStep("choose"); setError(null); }}
                className="text-xs text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
              >
                {backArrow} {t("back")}
              </button>
            </div>
          )}

          {step === "join" && (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div>
                <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                  {t("inviteCodeLabel")}
                </label>
                <input
                  type="text"
                  value={inviteCode}
                  onChange={e => { setInviteCode(e.target.value.toUpperCase()); setError(null); }}
                  placeholder="ABCD1234"
                  maxLength={8}
                  className="w-full px-3 py-2.5 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm font-mono tracking-wider text-center focus:outline-none focus:border-sky-500"
                  autoFocus
                  onKeyDown={e => e.key === "Enter" && handleJoinSpace()}
                  disabled={isCoolingDown}
                />
              </div>

              {error && (
                <p className="text-xs text-red-500">{error}</p>
              )}

              <button
                onClick={handleJoinSpace}
                disabled={loading || inviteCode.trim().length !== 8 || isCoolingDown}
                className="w-full py-2.5 rounded-lg bg-sky-500 hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600 text-white font-semibold text-sm transition-colors"
              >
                {loading ? t("joining") : t("joinButton")}
              </button>

              <button
                onClick={() => { setStep("choose"); setError(null); }}
                className="text-xs text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
              >
                {backArrow} {t("back")}
              </button>
            </div>
          )}
        </div>

        {/* Language switcher */}
        <div style={{ marginTop: "1.5rem" }}>
          <LanguageSwitcher variant="auth" />
        </div>
      </div>
    </div>
  );
}
