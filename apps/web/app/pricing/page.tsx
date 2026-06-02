"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import { getPlans, createSpaceCheckout, getSpaceSubscription, PlanDto } from "@/lib/api/billing";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { hasStoredAccessToken, useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";

// Fallback plans used when the API is unreachable
const FALLBACK_PLANS: PlanDto[] = [
  { variantId: "", name: "Starter", priceInCents: 5000, interval: "month", description: null, sortOrder: 1, memberLimit: 10 },
  { variantId: "", name: "Growth", priceInCents: 9000, interval: "month", description: null, sortOrder: 2, memberLimit: 20 },
  { variantId: "", name: "Team", priceInCents: 15000, interval: "month", description: null, sortOrder: 3, memberLimit: 30 },
  { variantId: "", name: "Organization", priceInCents: 25000, interval: "month", description: null, sortOrder: 4, memberLimit: 50 },
  { variantId: "", name: "Unlimited", priceInCents: 35000, interval: "month", description: null, sortOrder: 5, memberLimit: null },
];

type PaymentState =
  | { phase: "idle" }
  | { phase: "polling"; planName: string }
  | { phase: "success"; planName: string };

export default function PricingPage() {
  const t = useTranslations("pricing");
  const locale = useLocale();
  const router = useRouter();
  const { isLoggedIn } = useEffectiveAuth();
  const { currentSpaceId } = useSpaceStore();
  const backArrow = locale === "he" ? "→" : "←";

  const [plans, setPlans] = useState<PlanDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [checkoutLoading, setCheckoutLoading] = useState<string | null>(null);
  const [paymentState, setPaymentState] = useState<PaymentState>({ phase: "idle" });

  const pollIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const pollTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Cleanup polling on unmount
  useEffect(() => {
    return () => {
      if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
      if (pollTimeoutRef.current) clearTimeout(pollTimeoutRef.current);
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function fetchPlans() {
      try {
        const data = await getPlans();
        if (!cancelled) {
          setPlans(data.length > 0 ? data : FALLBACK_PLANS);
          setError(false);
        }
      } catch {
        if (!cancelled) {
          setPlans(FALLBACK_PLANS);
          setError(true);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    fetchPlans();
    return () => { cancelled = true; };
  }, []);

  const cancelPolling = useCallback(() => {
    if (pollIntervalRef.current) {
      clearInterval(pollIntervalRef.current);
      pollIntervalRef.current = null;
    }
    if (pollTimeoutRef.current) {
      clearTimeout(pollTimeoutRef.current);
      pollTimeoutRef.current = null;
    }
    setPaymentState({ phase: "idle" });
  }, []);

  function handleBack() {
    if (typeof window !== "undefined" && window.history.length <= 1) {
      router.push("/");
    } else {
      router.back();
    }
  }

  async function handleSelectPlan(plan: PlanDto) {
    // If plan has no variant ID (fallback plans), show coming soon message
    if (!plan.variantId) {
      alert(t("comingSoon"));
      return;
    }

    const spaceState = useSpaceStore.getState();

    // If not logged in, redirect to login with return URL
    if (!isLoggedIn && !hasStoredAccessToken()) {
      router.push("/login?redirect=/pricing");
      return;
    }

    // If no space selected, try to get it from localStorage (same rehydration issue)
    let spaceId = spaceState.currentSpaceId;
    if (!spaceId) {
      try {
        const raw = localStorage.getItem("jobuler-space");
        if (raw) {
          const parsed = JSON.parse(raw);
          spaceId = parsed?.state?.currentSpaceId ?? null;
        }
      } catch { /* ignore parse errors */ }
    }

    if (!spaceId) {
      router.push("/spaces");
      return;
    }

    // Create checkout with the selected variant ID
    setCheckoutLoading(plan.variantId);
    try {
      const { checkoutUrl } = await createSpaceCheckout(spaceId, plan.variantId);
      // Open checkout in a new tab so the user's auth state is preserved
      window.open(checkoutUrl, "_blank");
      setCheckoutLoading(null);

      // Show the "waiting for payment" state
      setPaymentState({ phase: "polling", planName: plan.name });

      // Poll for subscription activation
      pollIntervalRef.current = setInterval(async () => {
        try {
          const sub = await getSpaceSubscription(spaceId);
          if (sub && (sub.status === "active" || sub.isActive)) {
            if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
            if (pollTimeoutRef.current) clearTimeout(pollTimeoutRef.current);
            pollIntervalRef.current = null;
            pollTimeoutRef.current = null;

            // Show success briefly before redirecting
            setPaymentState({ phase: "success", planName: plan.name });
            setTimeout(() => {
              router.push("/spaces/settings");
            }, 2000);
          }
        } catch { /* ignore polling errors */ }
      }, 5000);

      // Stop polling after 5 minutes
      pollTimeoutRef.current = setTimeout(() => {
        if (pollIntervalRef.current) {
          clearInterval(pollIntervalRef.current);
          pollIntervalRef.current = null;
        }
        setPaymentState({ phase: "idle" });
      }, 300000);
    } catch (err: unknown) {
      setCheckoutLoading(null);
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 401) {
        alert(t("sessionExpiredReload") ?? "Your session expired. Please reload the page and try again.");
        window.location.reload();
      } else {
        alert(t("checkoutError"));
      }
    }
  }

  function formatPrice(priceInCents: number): string {
    return String(Math.round(priceInCents / 100));
  }

  if (loading) {
    return (
      <main className="min-h-screen bg-slate-50 dark:bg-slate-900 p-8 flex items-center justify-center">
        <div className="text-center text-slate-500 dark:text-slate-400">
          <div className="text-2xl mb-2">⏳</div>
          <p>{t("loading")}</p>
        </div>
      </main>
    );
  }

  // Payment waiting overlay
  if (paymentState.phase !== "idle") {
    return (
      <main className="min-h-screen bg-slate-50 dark:bg-slate-900 p-8 flex items-center justify-center">
        <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg p-8 max-w-sm w-full text-center">
          {paymentState.phase === "polling" && (
            <>
              {/* Spinner */}
              <div className="flex justify-center mb-5">
                <div className="w-10 h-10 border-3 border-sky-200 dark:border-sky-800 border-t-sky-500 rounded-full animate-spin" />
              </div>
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white mb-2">
                {t("waitingForPayment")}
              </h2>
              <p className="text-sm text-slate-500 dark:text-slate-400 mb-1">
                {t("selectedPlan", { name: paymentState.planName })}
              </p>
              <p className="text-xs text-slate-400 dark:text-slate-500 mb-6">
                {t("waitingHint")}
              </p>
              <button
                onClick={cancelPolling}
                className="px-5 py-2.5 rounded-lg border border-slate-200 dark:border-slate-600 text-sm font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
              >
                {t("cancelWaiting")}
              </button>
            </>
          )}
          {paymentState.phase === "success" && (
            <>
              {/* Success icon */}
              <div className="flex justify-center mb-5">
                <div className="w-14 h-14 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                  <svg width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="#22c55e" strokeWidth={2.5}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                  </svg>
                </div>
              </div>
              <h2 className="text-lg font-semibold text-green-700 dark:text-green-300 mb-2">
                {t("paymentSuccess")}
              </h2>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                {t("redirecting")}
              </p>
            </>
          )}
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-slate-50 dark:bg-slate-900 p-8">
      <div className="max-w-[900px] mx-auto">
        {/* Header */}
        <div className="text-center mb-10">
          <div className="flex justify-center items-center gap-2.5 mb-4">
            <ShifterLogo size={36} />
            <span className="text-2xl font-bold text-slate-900 dark:text-white">Shifter</span>
          </div>
          <h1 className="text-3xl font-bold text-slate-900 dark:text-white m-0">
            {t("title")}
          </h1>
          <p className="text-slate-500 dark:text-slate-400 text-sm mt-2">
            {t("subtitle")}
          </p>
        </div>

        {/* Plans grid */}
        <div className="grid grid-cols-[repeat(auto-fit,minmax(160px,1fr))] gap-4">
          {plans.map((plan, i) => {
            const isPopular = i === Math.floor(plans.length / 2);
            const isLoadingCheckout = checkoutLoading === plan.variantId;

            return (
              <div
                key={plan.variantId || `fallback-${i}`}
                className={`bg-white dark:bg-slate-800 rounded-2xl p-6 text-center relative ${
                  isPopular
                    ? "border-2 border-sky-500 shadow-[0_4px_24px_rgba(14,165,233,0.12)]"
                    : "border border-slate-200 dark:border-slate-700 shadow-sm"
                }`}
              >
                {isPopular && (
                  <span className="absolute -top-3 start-1/2 -translate-x-1/2 bg-sky-500 text-white text-[0.7rem] font-semibold px-2.5 py-0.5 rounded-full">
                    {t("popular")}
                  </span>
                )}
                <div className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-2">
                  {plan.name}
                </div>
                <div className="text-3xl font-bold text-slate-900 dark:text-white">
                  {t("price", { amount: formatPrice(plan.priceInCents) })}
                </div>
                <div className="text-xs text-slate-400 dark:text-slate-500 mb-4">
                  {t("perMonth")}
                </div>
                {plan.description && (
                  <div
                    className="text-xs text-slate-500 dark:text-slate-400 mb-3"
                    dangerouslySetInnerHTML={{ __html: plan.description }}
                  />
                )}
                <button
                  onClick={() => handleSelectPlan(plan)}
                  disabled={isLoadingCheckout}
                  className={`w-full py-2.5 rounded-xl text-sm font-semibold transition-colors disabled:opacity-60 disabled:cursor-wait ${
                    isPopular
                      ? "bg-sky-500 hover:bg-sky-600 text-white border-none"
                      : "bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 border border-slate-200 dark:border-slate-600"
                  }`}
                >
                  {isLoadingCheckout ? "..." : t("selectPlan")}
                </button>
              </div>
            );
          })}
        </div>

        {/* Features */}
        <div className="mt-10 text-center">
          <p className="text-slate-500 dark:text-slate-400 text-sm">
            {t("allPlansInclude")}
          </p>
        </div>

        {/* Error notice */}
        {error && (
          <div className="mt-4 text-center">
            <p className="text-amber-500 text-xs">
              {t("fetchError")}
            </p>
          </div>
        )}

        {/* Back + Language */}
        <div className="mt-8 text-center">
          <button
            onClick={handleBack}
            className="text-sky-500 text-sm bg-transparent border-none cursor-pointer p-0 hover:underline"
          >
            {backArrow} {t("back")}
          </button>
          <div className="mt-4">
            <LanguageSwitcher variant="auth" />
          </div>
        </div>
      </div>
    </main>
  );
}
