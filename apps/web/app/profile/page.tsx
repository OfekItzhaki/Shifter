"use client";

import { useEffect, useState } from "react";
import AppShell from "@/components/shell/AppShell";
import { getMe, updateMe, MeDto } from "@/lib/api/auth";
import ImageUpload from "@/components/ImageUpload";

function getInitials(name: string): string {
  return name
    .split(" ")
    .map(p => p.charAt(0))
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

function formatBirthday(dateStr: string | null): string {
  if (!dateStr) return "—";
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString("he-IL", { day: "numeric", month: "long", year: "numeric" });
  } catch {
    return dateStr;
  }
}

function formatMemberSince(dateStr: string): string {
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString("he-IL", { day: "numeric", month: "long", year: "numeric" });
  } catch {
    return dateStr;
  }
}

const cardStyle: React.CSSProperties = {
  background: "white",
  borderRadius: 16,
  border: "1px solid #e2e8f0",
  boxShadow: "0 1px 4px rgba(0,0,0,0.06)",
  padding: "1.5rem",
};

const labelStyle: React.CSSProperties = {
  fontSize: "0.75rem",
  fontWeight: 600,
  color: "#94a3b8",
  textTransform: "uppercase",
  letterSpacing: "0.05em",
  marginBottom: "0.25rem",
  cursor: "default",
  userSelect: "none",
};

const valueStyle: React.CSSProperties = {
  fontSize: "0.9375rem",
  color: "#0f172a",
  fontWeight: 500,
  cursor: "default",
};

const inputStyle: React.CSSProperties = {
  width: "100%",
  border: "1px solid #e2e8f0",
  borderRadius: 10,
  padding: "0.625rem 0.875rem",
  fontSize: "0.875rem",
  color: "#0f172a",
  outline: "none",
  boxSizing: "border-box",
  background: "white",
};

export default function ProfilePage() {
  const [me, setMe] = useState<MeDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({ displayName: "", phoneNumber: "", profileImageUrl: "", birthday: "" });
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    getMe()
      .then(data => {
        setMe(data);
        setForm({
          displayName: data.displayName ?? "",
          phoneNumber: data.phoneNumber ?? "",
          profileImageUrl: data.profileImageUrl ?? "",
          birthday: data.birthday ? data.birthday.split("T")[0] : "",
        });
      })
      .catch(() => setError("שגיאה בטעינת הפרופיל"))
      .finally(() => setLoading(false));
  }, []);

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    try {
      await updateMe({
        displayName: form.displayName || undefined,
        phoneNumber: form.phoneNumber || undefined,
        profileImageUrl: form.profileImageUrl || undefined,
        birthday: form.birthday || undefined,
      });
      setMe(prev => prev ? {
        ...prev,
        displayName: form.displayName || prev.displayName,
        phoneNumber: form.phoneNumber || null,
        profileImageUrl: form.profileImageUrl || null,
        birthday: form.birthday || null,
      } : prev);
      setEditing(false);
    } catch (err: any) {
      setSaveError(err?.response?.data?.message ?? "שגיאה בשמירת הפרופיל");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <AppShell>
        <div style={{ display: "flex", alignItems: "center", gap: 12, color: "#94a3b8", fontSize: "0.875rem", padding: "2rem 0" }}>
          <svg className="animate-spin" width="20" height="20" fill="none" viewBox="0 0 24 24">
            <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          טוען...
        </div>
      </AppShell>
    );
  }

  if (error || !me) {
    return (
      <AppShell>
        <p style={{ color: "#dc2626", fontSize: "0.875rem" }}>{error ?? "שגיאה בטעינת הפרופיל"}</p>
      </AppShell>
    );
  }

  const avatarContent = me.profileImageUrl ? (
    <img
      src={me.profileImageUrl}
      alt={me.displayName}
      style={{ width: 96, height: 96, borderRadius: "50%", objectFit: "cover" }}
    />
  ) : (
    <div style={{
      width: 96, height: 96, borderRadius: "50%",
      background: "linear-gradient(135deg, #3b82f6, #6366f1)",
      display: "flex", alignItems: "center", justifyContent: "center",
      color: "white", fontSize: "2rem", fontWeight: 700,
    }}>
      {getInitials(me.displayName)}
    </div>
  );

  return (
    <AppShell>
      <div style={{ maxWidth: 720, direction: "rtl" }}>
        {/* Hero section */}
        <div style={{ ...cardStyle, marginBottom: "1.5rem" }}>
          {editing ? (
            <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <h2 style={{ fontSize: "1rem", fontWeight: 600, color: "#0f172a", margin: 0 }}>עריכת פרופיל</h2>

              <div>
                <label style={labelStyle}>שם תצוגה</label>
                <input
                  type="text"
                  value={form.displayName}
                  onChange={e => setForm(f => ({ ...f, displayName: e.target.value }))}
                  style={inputStyle}
                  placeholder="שם תצוגה"
                />
              </div>

              <div>
                <label style={labelStyle}>מספר טלפון</label>
                <input
                  type="tel"
                  value={form.phoneNumber}
                  onChange={e => setForm(f => ({ ...f, phoneNumber: e.target.value }))}
                  style={{ ...inputStyle, direction: "ltr", textAlign: "left" }}
                  placeholder="050-0000000"
                />
              </div>

              <div>
                <label style={labelStyle}>תמונת פרופיל</label>
                <ImageUpload
                  value={form.profileImageUrl || null}
                  onChange={url => setForm(f => ({ ...f, profileImageUrl: url }))}
                  shape="circle"
                  size={80}
                  label="העלה תמונה"
                  disabled={saving}
                />
              </div>

              <div>
                <label style={labelStyle}>תאריך לידה</label>
                <input
                  type="date"
                  value={form.birthday}
                  onChange={e => setForm(f => ({ ...f, birthday: e.target.value }))}
                  style={inputStyle}
                />
              </div>

              {saveError && (
                <p style={{ fontSize: "0.875rem", color: "#dc2626", margin: 0 }}>{saveError}</p>
              )}

              <div style={{ display: "flex", gap: "0.75rem" }}>
                <button
                  type="submit"
                  disabled={saving}
                  style={{
                    background: saving ? "#93c5fd" : "#3b82f6",
                    color: "white", border: "none", borderRadius: 10,
                    padding: "0.625rem 1.25rem", fontSize: "0.875rem",
                    fontWeight: 600, cursor: saving ? "not-allowed" : "pointer",
                  }}
                >
                  {saving ? "שומר..." : "שמור"}
                </button>
                <button
                  type="button"
                  onClick={() => { setEditing(false); setSaveError(null); }}
                  style={{
                    background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
                    padding: "0.625rem 1.25rem", fontSize: "0.875rem",
                    color: "#64748b", cursor: "pointer",
                  }}
                >
                  ביטול
                </button>
              </div>
            </form>
          ) : (
            <div style={{ display: "flex", alignItems: "center", gap: "1.5rem" }}>
              {avatarContent}
              <div style={{ flex: 1 }}>
                <h1 style={{ fontSize: "1.5rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.25rem" }}>
                  {me.displayName}
                </h1>
                <p style={{ fontSize: "0.875rem", color: "#64748b", margin: 0 }}>{me.email}</p>
              </div>
              <button
                onClick={() => setEditing(true)}
                style={{
                  background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
                  padding: "0.5rem 1rem", fontSize: "0.875rem", fontWeight: 500,
                  color: "#374151", cursor: "pointer", flexShrink: 0,
                }}
              >
                עריכה
              </button>
            </div>
          )}
        </div>

        {/* Info cards grid */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          {/* Contact info */}
          <div style={cardStyle}>
            <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: "0 0 1rem" }}>פרטי קשר</h2>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
              <div>
                <p style={labelStyle}>טלפון</p>
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="#94a3b8" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                  </svg>
                  <p style={{ ...valueStyle, direction: "ltr", textAlign: "left" }}>{me.phoneNumber ?? "—"}</p>
                </div>
              </div>
              <div>
                <p style={labelStyle}>אימייל</p>
                <p style={valueStyle}>{me.email}</p>
              </div>
            </div>
          </div>

          {/* Personal info */}
          <div style={cardStyle}>
            <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: "0 0 1rem" }}>פרטים אישיים</h2>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
              <div>
                <p style={labelStyle}>תאריך לידה</p>
                <p style={valueStyle}>{formatBirthday(me.birthday)}</p>
              </div>
              <div>
                <p style={labelStyle}>חבר מאז</p>
                <p style={valueStyle}>{formatMemberSince(me.createdAt)}</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  );
}
