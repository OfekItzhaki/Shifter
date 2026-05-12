"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";

const PLANS = [
  { id: "starter", members: 15, price: 50 },
  { id: "growth", members: 30, price: 90 },
  { id: "team", members: 60, price: 150 },
  { id: "org", members: 90, price: 250 },
  { id: "unlimited", members: Infinity, price: 350 },
];

export default function PricingPage() {
  const t = useTranslations("pricing");
  const [selectedPlan, setSelectedPlan] = useState<string | null>(null);

  function handleSelectPlan(planId: string) {
    setSelectedPlan(planId);
    alert(t("comingSoon"));
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
          {PLANS.map((plan, i) => (
            <div
              key={plan.id}
              style={{
                background: "white",
                borderRadius: 16,
                border: i === 2 ? "2px solid #3b82f6" : "1px solid #e2e8f0",
                padding: "1.5rem 1.25rem",
                textAlign: "center",
                position: "relative",
                boxShadow: i === 2 ? "0 4px 24px rgba(59,130,246,0.12)" : "0 2px 8px rgba(0,0,0,0.04)",
              }}
            >
              {i === 2 && (
                <span style={{
                  position: "absolute", top: -12, left: "50%", transform: "translateX(-50%)",
                  background: "#3b82f6", color: "white", fontSize: "0.7rem", fontWeight: 600,
                  padding: "2px 10px", borderRadius: 20,
                }}>
                  {t("popular")}
                </span>
              )}
              <div style={{ fontSize: "0.8rem", fontWeight: 600, color: "#64748b", textTransform: "uppercase", marginBottom: "0.5rem" }}>
                {plan.members === Infinity ? t("unlimited") : t("upToMembers", { count: plan.members })}
              </div>
              <div style={{ fontSize: "2rem", fontWeight: 700, color: "#0f172a" }}>
                ₪{plan.price}
              </div>
              <div style={{ fontSize: "0.8rem", color: "#94a3b8", marginBottom: "1rem" }}>
                {t("perMonth")}
              </div>
              <button
                onClick={() => handleSelectPlan(plan.id)}
                style={{
                  width: "100%",
                  padding: "0.6rem",
                  borderRadius: 10,
                  border: i === 2 ? "none" : "1px solid #e2e8f0",
                  background: i === 2 ? "#3b82f6" : "white",
                  color: i === 2 ? "white" : "#374151",
                  fontWeight: 600,
                  fontSize: "0.85rem",
                  cursor: "pointer",
                }}
              >
                {t("selectPlan")}
              </button>
            </div>
          ))}
        </div>

        {/* Features */}
        <div style={{ marginTop: "2.5rem", textAlign: "center" }}>
          <p style={{ color: "#64748b", fontSize: "0.875rem" }}>
            {t("allPlansInclude")}
          </p>
        </div>

        {/* Back + Language */}
        <div style={{ marginTop: "2rem", textAlign: "center" }}>
          <Link href="/login" style={{ color: "#3b82f6", fontSize: "0.875rem", textDecoration: "none" }}>
            ← {t("back")}
          </Link>
          <div style={{ marginTop: "1rem" }}>
            <LanguageSwitcher variant="auth" />
          </div>
        </div>
      </div>
    </main>
  );
}
