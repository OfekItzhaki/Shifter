"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import AppShell from "@/components/shell/AppShell";
import Modal from "@/components/Modal";
import { getMe, updateMe, MeDto } from "@/lib/api/auth";
import { apiClient } from "@/lib/api/client";
import ImageUpload from "@/components/ImageUpload";
import NotificationPreferences from "@/components/NotificationPreferences";
import PushNotificationSettings from "@/components/PushNotificationSettings";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
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
    return d.toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" });
  } catch {
    return dateStr;
  }
}

function formatMemberSince(dateStr: string): string {
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" });
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
  const t = useTranslations("profile");
  const [me, setMe] = useState<MeDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);

  const [editOpen, setEditOpen] = useState(false);
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
      .catch(() => setError("Error loading profile"))
      .finally(() => setLoading(false));
  }, []);

  function openEdit() {
    if (!me) return;
    setForm({
      displayName: me.displayName ?? "",
      phoneNumber: me.phoneNumber ?? "",
      profileImageUrl: me.profileImageUrl ?? "",
      birthday: me.birthday ? me.birthday.split("T")[0] : "",
    });
    setSaveError(null);
    setEditOpen(true);
  }

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
      setEditOpen(false);
    } catch (err: any) {
      setSaveError(err?.response?.data?.message ?? "Error saving profile");
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
          Loading...
        </div>
      </AppShell>
    );
  }

  if (error || !me) {
    return (
      <AppShell>
        <p style={{ color: "#dc2626", fontSize: "0.875rem" }}>{error ?? "Error loading profile"}</p>
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
          <div className="flex flex-col sm:flex-row items-center sm:items-center gap-4 sm:gap-6">
            {avatarContent}
            <div style={{ flex: 1 }} className="text-center sm:text-start">
              <h1 style={{ fontSize: "1.5rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.25rem" }}>
                {me.displayName}
              </h1>
              <p style={{ fontSize: "0.875rem", color: "#64748b", margin: 0 }}>{me.email}</p>
            </div>
            <button
              onClick={openEdit}
              style={{
                background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
                padding: "0.5rem 1rem", fontSize: "0.875rem", fontWeight: 500,
                color: "#374151", cursor: "pointer", flexShrink: 0,
              }}
            >
              {t("edit")}
            </button>
          </div>
        </div>

        {/* Info cards grid */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }} className="mobile-stack">
          {/* Contact info */}
          <div style={cardStyle}>
            <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: "0 0 1rem" }}>{t("contactInfo")}</h2>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
              <div>
                <p style={labelStyle}>{t("phone")}</p>
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="#94a3b8" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                  </svg>
                  <p style={{ ...valueStyle, direction: "ltr", textAlign: "left" }}>{me.phoneNumber ?? "—"}</p>
                </div>
              </div>
              <div>
                <p style={labelStyle}>{t("email")}</p>
                <p style={valueStyle}>{me.email}</p>
              </div>
            </div>
          </div>

          {/* Personal info */}
          <div style={cardStyle}>
            <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: "0 0 1rem" }}>{t("personalInfo")}</h2>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
              <div>
                <p style={labelStyle}>{t("birthday")}</p>
                <p style={valueStyle}>{formatBirthday(me.birthday)}</p>
              </div>
              <div>
                <p style={labelStyle}>{t("memberSince")}</p>
                <p style={valueStyle}>{formatMemberSince(me.createdAt)}</p>
              </div>
            </div>
          </div>
        </div>

        {/* Time Format Preference */}
        <TimeFormatToggle />

        {/* Notification Preferences */}
        <div style={{ ...cardStyle, marginTop: "1rem" }}>
          <NotificationPreferences />
        </div>

        {/* Push Notification Settings */}
        {currentSpaceId && (
          <div style={{ ...cardStyle, marginTop: "1rem" }}>
            <PushNotificationSettings spaceId={currentSpaceId} />
          </div>
        )}

        {/* Export My Data */}
        <ExportDataSection />

        {/* Delete Account */}
        <DeleteAccountSection />
      </div>

      {/* Edit Profile Modal */}
      <Modal open={editOpen} onClose={() => { setEditOpen(false); setSaveError(null); }} title={t("edit")} maxWidth={480}>
        <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <label style={labelStyle}>{t("displayName")}</label>
            <input
              type="text"
              value={form.displayName}
              onChange={e => setForm(f => ({ ...f, displayName: e.target.value }))}
              style={inputStyle}
              placeholder={t("displayName")}
            />
          </div>

          <div>
            <label style={labelStyle}>{t("phone")}</label>
            <input
              type="tel"
              value={form.phoneNumber}
              onChange={e => setForm(f => ({ ...f, phoneNumber: e.target.value }))}
              style={{ ...inputStyle, direction: "ltr", textAlign: "left" }}
              placeholder="+1 555 000 0000"
            />
          </div>

          <div>
            <label style={labelStyle}>{t("profileImage")}</label>
            <ImageUpload
              value={form.profileImageUrl || null}
              onChange={url => setForm(f => ({ ...f, profileImageUrl: url }))}
              shape="circle"
              size={80}
              disabled={saving}
            />
          </div>

          <div>
            <label style={labelStyle}>{t("birthday")}</label>
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

          <div style={{ display: "flex", gap: "0.75rem", paddingTop: "0.25rem" }}>
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
              {saving ? t("saving") : t("save")}
            </button>
            <button
              type="button"
              onClick={() => { setEditOpen(false); setSaveError(null); }}
              style={{
                background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
                padding: "0.625rem 1.25rem", fontSize: "0.875rem",
                color: "#64748b", cursor: "pointer",
              }}
            >
              {t("cancel")}
            </button>
          </div>
        </form>
      </Modal>
    </AppShell>
  );
}


function TimeFormatToggle() {
  const t = useTranslations("profile");
  const { timeFormat, setTimeFormat } = useAuthStore();

  return (
    <div style={{ ...cardStyle, marginTop: "1rem" }}>
      <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: "0 0 0.75rem" }}>
        {t("timeFormat")}
      </h2>
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <button
          onClick={() => setTimeFormat("24h")}
          style={{
            flex: 1,
            padding: "0.625rem 1rem",
            borderRadius: 10,
            border: timeFormat === "24h" ? "2px solid #3b82f6" : "1px solid #e2e8f0",
            background: timeFormat === "24h" ? "#eff6ff" : "white",
            color: timeFormat === "24h" ? "#1d4ed8" : "#64748b",
            fontWeight: 600,
            fontSize: "0.875rem",
            cursor: "pointer",
            transition: "all 0.15s",
          }}
          aria-pressed={timeFormat === "24h"}
        >
          24h
          <span style={{ display: "block", fontSize: "0.75rem", fontWeight: 400, marginTop: 2 }}>
            14:30
          </span>
        </button>
        <button
          onClick={() => setTimeFormat("12h")}
          style={{
            flex: 1,
            padding: "0.625rem 1rem",
            borderRadius: 10,
            border: timeFormat === "12h" ? "2px solid #3b82f6" : "1px solid #e2e8f0",
            background: timeFormat === "12h" ? "#eff6ff" : "white",
            color: timeFormat === "12h" ? "#1d4ed8" : "#64748b",
            fontWeight: 600,
            fontSize: "0.875rem",
            cursor: "pointer",
            transition: "all 0.15s",
          }}
          aria-pressed={timeFormat === "12h"}
        >
          AM/PM
          <span style={{ display: "block", fontSize: "0.75rem", fontWeight: 400, marginTop: 2 }}>
            2:30 PM
          </span>
        </button>
      </div>
    </div>
  );
}


function ExportDataSection() {
  const t = useTranslations("profile");
  const [exporting, setExporting] = useState(false);

  async function handleExport() {
    setExporting(true);
    try {
      const { data } = await apiClient.get("/auth/me/export", { responseType: "blob" });
      const url = URL.createObjectURL(new Blob([data], { type: "application/json" }));
      const a = document.createElement("a");
      a.href = url;
      a.download = `shifter-data-export-${new Date().toISOString().split("T")[0]}.json`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      // silently fail
    } finally {
      setExporting(false);
    }
  }

  return (
    <div style={{ ...cardStyle, marginTop: "1rem" }}>
      <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: "0 0 0.5rem" }}>
        {t("exportData")}
      </h2>
      <p style={{ fontSize: "0.75rem", color: "#64748b", margin: "0 0 1rem" }}>
        {t("exportDataDesc")}
      </p>
      <button
        onClick={handleExport}
        disabled={exporting}
        style={{
          background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
          padding: "0.5rem 1rem", fontSize: "0.8125rem", color: "#374151",
          cursor: exporting ? "not-allowed" : "pointer", opacity: exporting ? 0.6 : 1,
        }}
      >
        {exporting ? t("exporting") : t("exportButton")}
      </button>
    </div>
  );
}


function DeleteAccountSection() {
  const t = useTranslations("profile");
  const router = useRouter();
  const [showConfirm, setShowConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleDelete() {
    setDeleting(true);
    setError(null);
    try {
      await apiClient.delete("/auth/me");
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      router.push("/login");
    } catch {
      setError("Error deleting account");
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div style={{ ...cardStyle, marginTop: "1.5rem", borderColor: "#e2a4a4" }}>
      <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#dc2626", margin: "0 0 0.5rem" }}>
        {t("deleteAccount") ?? "Delete Account"}
      </h2>
      <p style={{ fontSize: "0.75rem", color: "#64748b", margin: "0 0 1rem" }}>
        {t("deleteAccountDesc") ?? "Permanently delete your account and all associated data. This cannot be undone."}
      </p>
      {!showConfirm ? (
        <button
          onClick={() => setShowConfirm(true)}
          style={{
            background: "none", border: "1px solid #fecaca", borderRadius: 10,
            padding: "0.5rem 1rem", fontSize: "0.8125rem", color: "#dc2626",
            cursor: "pointer",
          }}
        >
          {t("deleteAccountButton") ?? "Delete My Account"}
        </button>
      ) : (
        <div style={{ background: "#fef2f2", borderRadius: 10, padding: "1rem", display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          <p style={{ fontSize: "0.8125rem", color: "#dc2626", fontWeight: 600, margin: 0 }}>
            {t("deleteConfirmText") ?? "Are you sure? This will permanently delete your account, all your data, and remove you from all groups."}
          </p>
          {error && <p style={{ fontSize: "0.75rem", color: "#dc2626", margin: 0 }}>{error}</p>}
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button
              onClick={handleDelete}
              disabled={deleting}
              style={{
                background: "#dc2626", color: "white", border: "none", borderRadius: 8,
                padding: "0.5rem 1rem", fontSize: "0.8125rem", fontWeight: 600,
                cursor: deleting ? "not-allowed" : "pointer", opacity: deleting ? 0.6 : 1,
              }}
            >
              {deleting ? "..." : (t("yesDelete") ?? "Yes, Delete Everything")}
            </button>
            <button
              onClick={() => setShowConfirm(false)}
              style={{
                background: "none", border: "1px solid #e2e8f0", borderRadius: 8,
                padding: "0.5rem 1rem", fontSize: "0.8125rem", color: "#64748b",
                cursor: "pointer",
              }}
            >
              {t("cancel")}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
