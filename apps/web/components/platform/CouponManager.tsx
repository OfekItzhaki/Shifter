"use client";

import { useEffect, useState } from "react";
import { apiClient } from "@/lib/api/client";

interface Coupon {
  id: string;
  code: string;
  discountPercent: number;
  maxUses: number | null;
  currentUses: number;
  validFrom: string;
  validUntil: string | null;
  isActive: boolean;
  description: string | null;
}

export default function CouponManager() {
  const [coupons, setCoupons] = useState<Coupon[]>([]);
  const [loading, setLoading] = useState(true);
  const [newCode, setNewCode] = useState("");
  const [newDiscount, setNewDiscount] = useState(20);
  const [newMaxUses, setNewMaxUses] = useState("");
  const [newDescription, setNewDescription] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

  async function loadCoupons() {
    try {
      const { data } = await apiClient.get("/platform/coupons");
      setCoupons(data);
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { loadCoupons(); }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!newCode.trim() || newDiscount < 1 || newDiscount > 100) return;
    setCreating(true);
    setError(null);
    try {
      await apiClient.post("/platform/coupons", {
        code: newCode.trim(),
        discountPercent: newDiscount,
        maxUses: newMaxUses ? parseInt(newMaxUses) : null,
        validUntil: null,
        description: newDescription || null,
      });
      setNewCode("");
      setNewDiscount(20);
      setNewMaxUses("");
      setNewDescription("");
      await loadCoupons();
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string } } };
      setError(axiosErr?.response?.data?.error || "שגיאה ביצירת קופון");
    } finally {
      setCreating(false);
    }
  }

  async function handleDeactivate(id: string) {
    try {
      await apiClient.delete(`/platform/coupons/${id}`);
      await loadCoupons();
    } catch {}
  }

  return (
    <div style={{ background: "white", borderRadius: 14, border: "1px solid #e2e8f0", padding: "1.5rem" }}>
      <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: "0 0 1rem" }}>
        ניהול קופונים
      </h2>

      {/* Create form */}
      <form onSubmit={handleCreate} style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", marginBottom: "1rem" }}>
        <input
          value={newCode}
          onChange={e => setNewCode(e.target.value.toUpperCase())}
          placeholder="קוד (למשל: WELCOME50)"
          style={{ flex: "1 1 140px", border: "1px solid #e2e8f0", borderRadius: 8, padding: "0.5rem 0.75rem", fontSize: "0.8rem" }}
        />
        <input
          type="number"
          value={newDiscount}
          onChange={e => setNewDiscount(Number(e.target.value))}
          min={1}
          max={100}
          placeholder="% הנחה"
          style={{ width: 70, border: "1px solid #e2e8f0", borderRadius: 8, padding: "0.5rem", fontSize: "0.8rem", textAlign: "center" }}
        />
        <input
          value={newMaxUses}
          onChange={e => setNewMaxUses(e.target.value)}
          placeholder="מקס שימושים (ריק=∞)"
          style={{ width: 130, border: "1px solid #e2e8f0", borderRadius: 8, padding: "0.5rem 0.75rem", fontSize: "0.8rem" }}
        />
        <input
          value={newDescription}
          onChange={e => setNewDescription(e.target.value)}
          placeholder="תיאור (אופציונלי)"
          style={{ flex: "1 1 140px", border: "1px solid #e2e8f0", borderRadius: 8, padding: "0.5rem 0.75rem", fontSize: "0.8rem" }}
        />
        <button
          type="submit"
          disabled={creating || !newCode.trim()}
          style={{
            background: "#3b82f6", color: "white", border: "none", borderRadius: 8,
            padding: "0.5rem 1rem", fontSize: "0.8rem", fontWeight: 600, cursor: "pointer",
            opacity: creating ? 0.5 : 1,
          }}
        >
          {creating ? "..." : "צור קופון"}
        </button>
      </form>

      {error && <p style={{ color: "#dc2626", fontSize: "0.8rem", margin: "0 0 0.75rem" }}>{error}</p>}

      {/* Coupons list */}
      {loading ? (
        <p style={{ color: "#94a3b8", fontSize: "0.8rem" }}>טוען...</p>
      ) : coupons.length === 0 ? (
        <p style={{ color: "#94a3b8", fontSize: "0.8rem" }}>אין קופונים</p>
      ) : (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", fontSize: "0.8rem", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid #f1f5f9" }}>
                <th style={{ textAlign: "right", padding: "0.5rem", color: "#64748b", fontWeight: 600 }}>קוד</th>
                <th style={{ textAlign: "center", padding: "0.5rem", color: "#64748b", fontWeight: 600 }}>הנחה</th>
                <th style={{ textAlign: "center", padding: "0.5rem", color: "#64748b", fontWeight: 600 }}>שימושים</th>
                <th style={{ textAlign: "right", padding: "0.5rem", color: "#64748b", fontWeight: 600 }}>תיאור</th>
                <th style={{ textAlign: "center", padding: "0.5rem", color: "#64748b", fontWeight: 600 }}>סטטוס</th>
                <th style={{ padding: "0.5rem" }}></th>
              </tr>
            </thead>
            <tbody>
              {coupons.map(c => (
                <tr key={c.id} style={{ borderBottom: "1px solid #f8fafc" }}>
                  <td style={{ padding: "0.5rem", fontWeight: 600, fontFamily: "monospace" }}>{c.code}</td>
                  <td style={{ padding: "0.5rem", textAlign: "center" }}>{c.discountPercent}%</td>
                  <td style={{ padding: "0.5rem", textAlign: "center" }}>
                    {c.currentUses}{c.maxUses ? `/${c.maxUses}` : ""}
                  </td>
                  <td style={{ padding: "0.5rem", color: "#64748b" }}>{c.description || "—"}</td>
                  <td style={{ padding: "0.5rem", textAlign: "center" }}>
                    <span style={{
                      display: "inline-block", padding: "2px 8px", borderRadius: 12, fontSize: "0.7rem", fontWeight: 600,
                      background: c.isActive ? "#f0fdf4" : "#fef2f2",
                      color: c.isActive ? "#15803d" : "#dc2626",
                      border: `1px solid ${c.isActive ? "#bbf7d0" : "#fecaca"}`,
                    }}>
                      {c.isActive ? "פעיל" : "מבוטל"}
                    </span>
                  </td>
                  <td style={{ padding: "0.5rem", textAlign: "center" }}>
                    {c.isActive && (
                      confirmDeleteId === c.id ? (
                        <span style={{ display: "flex", gap: 4, justifyContent: "center" }}>
                          <button
                            onClick={() => { handleDeactivate(c.id); setConfirmDeleteId(null); }}
                            style={{ background: "#dc2626", color: "white", border: "none", borderRadius: 6, padding: "2px 8px", fontSize: "0.7rem", cursor: "pointer" }}
                          >
                            אישור
                          </button>
                          <button
                            onClick={() => setConfirmDeleteId(null)}
                            style={{ background: "none", border: "1px solid #e2e8f0", borderRadius: 6, padding: "2px 8px", fontSize: "0.7rem", cursor: "pointer" }}
                          >
                            ביטול
                          </button>
                        </span>
                      ) : (
                        <button
                          onClick={() => setConfirmDeleteId(c.id)}
                          style={{ background: "none", border: "none", color: "#dc2626", cursor: "pointer", fontSize: "0.75rem" }}
                        >
                          בטל
                        </button>
                      )
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
