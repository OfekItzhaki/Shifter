"use client";

import { useState, useEffect } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import { getPlans, createSpaceCheckout, PlanDto } from "@/lib/api/billing";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";

// Fallback plans used when the API is unreachable
const FALLBACK_PLANS: PlanDto[] = [
  { variantId: "", name: "Starter", priceInCents: 5000, interval: "month", description: null, sortOrder: 1 },
  { variantId: "", name: "Growth", priceInCents: 9000, interval: "month", description: null, sortOrder: 2 },
  { variantId: "", name: "Team", priceInCents: 15000, interval: "month", description: null, sortOrder: 3 },
  { variantId: "", name: "Organization", priceInCents: 25000, interval: "month", description: null, sortOrder: 4 },
  { variantId: "", name: "Unlimited", priceInCents: 35000, interval: "month", description: null, sortOrder: 5 },
];

export default function PricingPage() {
  const t = useTranslations("pricing");
  const locale = useLocale();
  const router = useRouter();
  const { isAuthenticated } = useAuthStore();
  const { currentSpaceId } = useSpaceStore();
  const backArrow = locale === "he" ? "→" : "←";

  const [plans, setPlans] = useState<PlanDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [checkoutLoading, setCheckoutLoading] = useState<string | null>(null);

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

    // Check auth state at click time (not render time)
    const authState = useAuthStore.getState();
    const spaceState = useSpaceStore.getState();

    // Determine if user is authenticated: check both Zustand state AND localStorage tokens.
    // After returning from an external redirect (e.g., LemonSqueezy checkout), the Zustand
    // store may not have rehydrated yet, but tokens are still in localStorage.
    const hasTokens = !!localStorage.getItem("access_token") && !!localStorage.getItem("refresh_token");
    const isLoggedIn = authState.isAuthenticated || hasTokens;

    // If not logged in, redirect to login with return URL
    if (!isLoggedIn) {
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
      window.location.href = checkoutUrl;
    } catch (err: unknown) {
      console.error("[Pricing] Checkout failed:", err);
      alert(t("checkoutError"));
      setCheckoutLoading(null);
    }
  }

  function formatPrice(priceInCents: number): string {
    return String(Math.round(priceInCents / 100));
  }

  if (loading) {
    return (
      <main style={{ minHeight: "100vh", background: "#f8fafc", padding: "2rem 1rem", display: "flex", alignItems: "center", justifyContent: "center" }}>
        <div style={{ textAlign: "center", color: "#64748b" }}>
          <div style={{ fontSize: "1.5rem", marginBottom: "0.5rem" }}>⏳</div>
          <p>{t("loading")}</p>
        </div>
      </main>
    );
  }

  return (
    <main style={{ minHeight: "100vh", background: "#f8fafc", padding: "2rem 1rem" }}>
      <div style={{ maxWidth: 900, margin: "0 auto" }}>
        {/* Header */}
        <div style={{ textAlign: "center", marginBottom: "2.5rem" }}>
          <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 10, marginBottom: "1rem" }}>
            <ShifterLogo size={36} />
            <span style={{ fontSize: "1.5rem", fontWeight: 700, color: "#0f172a" }}>Shifter</span>
          </div>
          <h1 style={{ fontSize: "1.75rem", fontWeight: 700, color: "#0f172a", margin: 0 }}>
            {t("title")}
          </h1>
          <p style={{ color: "#64748b", fontSize: "0.95rem", marginTop: "0.5rem" }}>
            {t("subtitle")}
          </p>
        </div>

        {/* Plans grid */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: "1rem" }}>
          {plans.map((plan, i) => {
            const isPopular = i === Math.floor(plans.length / 2);
            const isLoadingCheckout = checkoutLoading === plan.variantId;

            return (
              <div
                key={plan.variantId || `fallback-${i}`}
                style={{
                  background: "white",
                  borderRadius: 16,
                  border: isPopular ? "2px solid #0ea5e9" : "1px solid #e2e8f0",
                  padding: "1.5rem 1.25rem",
                  textAlign: "center",
                  position: "relative",
                  boxShadow: isPopular ? "0 4px 24px rgba(14,165,233,0.12)" : "0 2px 8px rgba(0,0,0,0.04)",
                }}
              >
                {isPopular && (
                  <span style={{
                    position: "absolute", top: -12, left: "50%", transform: "translateX(-50%)",
                    background: "#0ea5e9", color: "white", fontSize: "0.7rem", fontWeight: 600,
                    padding: "2px 10px", borderRadius: 20,
                  }}>
                    {t("popular")}
                  </span>
                )}
                <div style={{ fontSize: "0.85rem", fontWeight: 600, color: "#374151", marginBottom: "0.5rem" }}>
                  {plan.name}
                </div>
                <div style={{ fontSize: "2rem", fontWeight: 700, color: "#0f172a" }}>
                  {t("price", { amount: formatPrice(plan.priceInCents) })}
                </div>
                <div style={{ fontSize: "0.8rem", color: "#94a3b8", marginBottom: "1rem" }}>
                  {t("perMonth")}
                </div>
                {plan.description && (
                  <div
                    style={{ fontSize: "0.75rem", color: "#64748b", marginBottom: "0.75rem" }}
                    dangerouslySetInnerHTML={{ __html: plan.description }}
                  />
                )}
                <button
                  onClick={() => handleSelectPlan(plan)}
                  disabled={isLoadingCheckout}
                  style={{
                    width: "100%",
                    padding: "0.6rem",
                    borderRadius: 10,
                    border: isPopular ? "none" : "1px solid #e2e8f0",
                    background: isPopular ? "#0ea5e9" : "white",
                    color: isPopular ? "white" : "#374151",
                    fontWeight: 600,
                    fontSize: "0.85rem",
                    cursor: isLoadingCheckout ? "wait" : "pointer",
                    opacity: isLoadingCheckout ? 0.6 : 1,
                  }}
                >
                  {isLoadingCheckout ? "..." : t("selectPlan")}
                </button>
              </div>
            );
          })}
        </div>

        {/* Features */}
        <div style={{ marginTop: "2.5rem", textAlign: "center" }}>
          <p style={{ color: "#64748b", fontSize: "0.875rem" }}>
            {t("allPlansInclude")}
          </p>
        </div>

        {/* Error notice */}
        {error && (
          <div style={{ marginTop: "1rem", textAlign: "center" }}>
            <p style={{ color: "#f59e0b", fontSize: "0.8rem" }}>
              {t("fetchError")}
            </p>
          </div>
        )}

        {/* Back + Language */}
        <div style={{ marginTop: "2rem", textAlign: "center" }}>
          <button
            onClick={handleBack}
            style={{
              color: "#0ea5e9",
              fontSize: "0.875rem",
              textDecoration: "none",
              background: "none",
              border: "none",
              cursor: "pointer",
              padding: 0,
            }}
          >
            {backArrow} {t("back")}
          </button>
          <div style={{ marginTop: "1rem" }}>
            <LanguageSwitcher variant="auth" />
          </div>
        </div>
      </div>
    </main>
  );
}
