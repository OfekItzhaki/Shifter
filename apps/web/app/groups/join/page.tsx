"use client";

import { useState, useEffect, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { joinGroupByCode } from "@/lib/api/groups";
import ShifterLogo from "@/components/shell/ShifterLogo";

function JoinContent() {
  const t = useTranslations("groups.join");
  const router = useRouter();
  const searchParams = useSearchParams();
  const codeFromUrl = searchParams.get("code") ?? "";
  const { isAuthenticated } = useAuthStore();
  const { setCurrentSpace } = useSpaceStore();

  const [code, setCode] = useState(codeFromUrl);
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<{ groupId: string; spaceId: string; groupName: string; alreadyMember: boolean } | null>(null);
  const [autoJoinDone, setAutoJoinDone] = useState(false);
  const [hydrated, setHydrated] = useState(false);

  // Wait for Zustand to rehydrate before checking auth
  useEffect(() => {
    if (useAuthStore.persist.hasHydrated()) { setHydrated(true); return; }
    const unsub = useAuthStore.persist.onFinishHydration(() => setHydrated(true));
    const fallback = setTimeout(() => setHydrated(true), 500);
    return () => { unsub(); clearTimeout(fallback); };
  }, []);

  async function handleJoin(e?: React.FormEvent) {
    e?.preventDefault();
    if (!code.trim()) return;
    setStatus("loading");
    setError(null);
    try {
      const res = await joinGroupByCode(code.trim());
      setResult(res);
      setCurrentSpace(res.spaceId, "");
      setStatus("success");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string; message?: string } } })?.response?.data?.error
        ?? (err as { response?: { data?: { error?: string; message?: string } } })?.response?.data?.message
        ?? t("invalidCode");
      setError(msg);
      setStatus("error");
    }
  }

  // Auto-join when user is authenticated and code is provided in URL
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (hydrated && isAuthenticated && codeFromUrl && !autoJoinDone) {
      setAutoJoinDone(true);
      handleJoin();
    }
  }, [hydrated, isAuthenticated, codeFromUrl]);

  if (!hydrated) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <svg className="animate-spin h-6 w-6 text-blue-400" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
      </div>
    );
  }

  if (!isAuthenticated) {
    const redirectUrl = `/groups/join?code=${encodeURIComponent(code)}`;
    return (
      <div className="text-center space-y-4">
        <div className="w-16 h-16 rounded-full bg-blue-50 flex items-center justify-center mx-auto">
          <svg width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="#3b82f6" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </div>
        <h2 className="text-lg font-bold text-slate-900">{t("loginRequired")}</h2>
        <p className="text-sm text-slate-500">{t("loginRequiredDesc")}</p>
        <div className="flex flex-col gap-2">
          <Link href={`/login?redirect=${encodeURIComponent(redirectUrl)}`} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-6 py-3 rounded-xl transition-colors">
            {t("signIn")}
          </Link>
          <Link href={`/register?redirect=${encodeURIComponent(redirectUrl)}`} className="text-sm text-blue-600 hover:underline">
            {t("createAccount")}
          </Link>
        </div>
      </div>
    );
  }

  if (status === "success" && result) {
    return (
      <div className="text-center space-y-4">
        <div className="w-16 h-16 rounded-full bg-emerald-50 flex items-center justify-center mx-auto">
          <svg width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="#10b981" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
        </div>
        <h2 className="text-lg font-bold text-slate-900">
          {result.alreadyMember ? t("alreadyMember") : t("success")}
        </h2>
        <p className="text-sm text-slate-500">{t("successDesc", { name: result.groupName })}</p>
        <button
          onClick={() => router.push(`/groups/${result.groupId}`)}
          className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-6 py-3 rounded-xl transition-colors"
        >
          {t("goToGroup")}
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="text-center">
        <h2 className="text-lg font-bold text-slate-900">{t("title")}</h2>
        <p className="text-sm text-slate-500 mt-1">{t("subtitle")}</p>
      </div>

      <form onSubmit={handleJoin} className="space-y-4">
        <div>
          <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
            {t("codeLabel")}
          </label>
          <input
            type="text"
            value={code}
            onChange={e => setCode(e.target.value.toUpperCase())}
            placeholder="ABCD1234"
            maxLength={8}
            className="w-full border border-slate-200 rounded-xl px-4 py-3 text-center text-lg font-mono font-bold tracking-widest focus:outline-none focus:ring-2 focus:ring-blue-500 uppercase"
            autoFocus
          />
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}

        <button
          type="submit"
          disabled={!code.trim() || status === "loading"}
          className="w-full bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-6 py-3 rounded-xl disabled:opacity-50 transition-colors"
        >
          {status === "loading" ? t("joining") : t("joinButton")}
        </button>
      </form>
    </div>
  );
}

export default function JoinGroupPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-6">
      <div className="w-full max-w-sm">
        <div className="flex justify-center mb-6">
          <Link href="/" className="flex items-center gap-2">
            <ShifterLogo size={32} />
            <span className="text-lg font-bold text-slate-900">Shifter</span>
          </Link>
        </div>
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
          <Suspense fallback={
            <div className="flex items-center justify-center py-8">
              <svg className="animate-spin h-6 w-6 text-blue-400" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
            </div>
          }>
            <JoinContent />
          </Suspense>
        </div>
      </div>
    </div>
  );
}
