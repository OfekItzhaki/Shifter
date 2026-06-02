"use client";

import { useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import ShifterLogo from "@/components/shell/ShifterLogo";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";

/**
 * Billing success page — shown after completing a LemonSqueezy checkout.
 * This page is intentionally lightweight and does NOT make authenticated API calls
 * on mount, avoiding token expiry issues from time spent on the external checkout.
 * After a short delay, it redirects the user back to the app.
 */
export default function BillingSuccessPage() {
  const t = useTranslations("billing");
  const locale = useLocale();
  const router = useRouter();
  const { isLoggedIn } = useEffectiveAuth();
  const [countdown, setCountdown] = useState(5);

  useEffect(() => {
    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          clearInterval(timer);
          // Redirect to home or spaces/settings
          if (isLoggedIn) {
            router.push("/spaces/settings");
          } else {
            router.push("/login");
          }
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [isLoggedIn, router]);

  return (
    <main style={{ minHeight: "100vh", background: "#f8fafc", display: "flex", alignItems: "center", justifyContent: "center", padding: "2rem 1rem" }}>
      <div style={{ maxWidth: 480, textAlign: "center" }}>
        <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 10, marginBottom: "2rem" }}>
          <ShifterLogo size={36} />
          <span style={{ fontSize: "1.5rem", fontWeight: 700, color: "#0f172a" }}>Shifter</span>
        </div>

        <div style={{ fontSize: "3rem", marginBottom: "1rem" }}>🎉</div>

        <h1 style={{ fontSize: "1.5rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.5rem" }}>
          {t("successTitle") ?? "Payment Successful!"}
        </h1>

        <p style={{ color: "#64748b", fontSize: "0.95rem", marginBottom: "1.5rem" }}>
          {t("successDescription") ?? "Thank you for your purchase. Your subscription is being activated."}
        </p>

        <p style={{ color: "#94a3b8", fontSize: "0.85rem", marginBottom: "2rem" }}>
          {t("redirecting") ?? "Redirecting in"} {countdown}...
        </p>

        <button
          onClick={() => router.push("/spaces/settings")}
          style={{
            padding: "0.75rem 2rem",
            borderRadius: 12,
            border: "none",
            background: "#0ea5e9",
            color: "white",
            fontWeight: 600,
            fontSize: "0.9rem",
            cursor: "pointer",
          }}
        >
          {t("goToSettings") ?? "Go to Settings"}
        </button>
      </div>
    </main>
  );
}
