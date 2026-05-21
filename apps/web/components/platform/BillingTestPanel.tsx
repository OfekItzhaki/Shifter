"use client";

import { useState } from "react";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";

export default function BillingTestPanel() {
  const { currentSpaceId } = useSpaceStore();
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleTestCharge() {
    if (!currentSpaceId) {
      setError("לא נבחר מרחב עבודה");
      return;
    }
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const { data } = await apiClient.post(`/spaces/${currentSpaceId}/billing/test-charge`);
      if (data.checkoutUrl) {
        setResult(data.checkoutUrl);
        window.open(data.checkoutUrl, "_blank");
      } else {
        setError("לא התקבל קישור לתשלום");
      }
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string } }; message?: string };
      setError(axiosErr?.response?.data?.error ?? axiosErr?.message ?? "שגיאה בביצוע חיוב ניסיון");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={{ background: "white", borderRadius: 14, border: "1px solid #e2e8f0", padding: "1.5rem" }}>
      <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.5rem" }}>
        🧪 בדיקת חיוב (Test Charge)
      </h2>
      <p style={{ fontSize: "0.8rem", color: "#64748b", margin: "0 0 1rem" }}>
        יוצר checkout session עם המוצר הזול לבדיקה. השתמש בכרטיס 4242 4242 4242 4242.
        <br />
        החיוב מסומן כ-test-charge ולא ישפיע על מנויים.
      </p>

      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
        <button
          onClick={handleTestCharge}
          disabled={loading}
          style={{
            background: "#8b5cf6",
            color: "white",
            border: "none",
            borderRadius: 8,
            padding: "0.5rem 1.25rem",
            fontSize: "0.8rem",
            fontWeight: 600,
            cursor: loading ? "not-allowed" : "pointer",
            opacity: loading ? 0.6 : 1,
          }}
        >
          {loading ? "יוצר..." : "בצע חיוב ניסיון"}
        </button>

        {!currentSpaceId && (
          <span style={{ fontSize: "0.75rem", color: "#f59e0b" }}>
            ⚠️ בחר מרחב עבודה קודם
          </span>
        )}
      </div>

      {error && (
        <p style={{ color: "#dc2626", fontSize: "0.8rem", margin: "0.75rem 0 0" }}>{error}</p>
      )}

      {result && (
        <div style={{ marginTop: "0.75rem", padding: "0.5rem 0.75rem", background: "#f0fdf4", borderRadius: 8, border: "1px solid #bbf7d0" }}>
          <p style={{ fontSize: "0.75rem", color: "#15803d", margin: 0 }}>
            ✅ Checkout URL נוצר בהצלחה — נפתח בטאב חדש
          </p>
          <a
            href={result}
            target="_blank"
            rel="noopener noreferrer"
            style={{ fontSize: "0.7rem", color: "#3b82f6", wordBreak: "break-all" }}
          >
            {result}
          </a>
        </div>
      )}
    </div>
  );
}
