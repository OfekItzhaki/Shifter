"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import AppShell from "@/components/shell/AppShell";
import Modal from "@/components/Modal";
import { getMe, updateMe, MeDto } from "@/lib/api/auth";
import { apiClient } from "@/lib/api/client";
import ImageUpload from "@/components/ImageUpload";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalDate } from "@/lib/utils/formatTime";
import {
  isWebAuthnSupported,
  registerCredential,
  listCredentials,
  deleteCredential,
  updateCredentialNickname,
  WebAuthnCredential,
} from "@/lib/webauthn";
function getInitials(name: string): string {
  return name
    .split(" ")
    .map(p => p.charAt(0))
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

function formatBirthday(dateStr: string | null, timezoneId: string | null): string {
  if (!dateStr) return "—";
  return formatLocalDate(dateStr, timezoneId);
}

function formatMemberSince(dateStr: string, timezoneId: string | null): string {
  return formatLocalDate(dateStr, timezoneId);
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
  const timezoneId = useAuthStore(s => s.timezoneId);
  const [me, setMe] = useState<MeDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
      .catch(() => setError("load"))
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
      setSaveError(err?.response?.data?.message ?? t("saveError"));
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
        </div>
      </AppShell>
    );
  }

  if (error || !me) {
    return (
      <AppShell>
        <div style={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          padding: "4rem 1rem",
          textAlign: "center",
          direction: "rtl",
        }}>
          {/* Error icon */}
          <div style={{
            width: 64,
            height: 64,
            borderRadius: "50%",
            background: "#fef2f2",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            marginBottom: "1.25rem",
          }}>
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="#dc2626" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="8" x2="12" y2="12" />
              <line x1="12" y1="16" x2="12.01" y2="16" />
            </svg>
          </div>
          <h2 style={{ fontSize: "1.125rem", fontWeight: 600, color: "#0f172a", margin: "0 0 0.5rem" }}>
            {t("loadError")}
          </h2>
          <p style={{ fontSize: "0.875rem", color: "#64748b", margin: "0 0 1.5rem", maxWidth: 320 }}>
            {t("loadErrorDesc")}
          </p>
          <button
            onClick={() => window.location.reload()}
            style={{
              padding: "0.625rem 1.5rem",
              borderRadius: 10,
              border: "none",
              background: "#3b82f6",
              color: "white",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer",
            }}
          >
            {t("retry")}
          </button>
        </div>
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
                <p style={valueStyle}>{formatBirthday(me.birthday, timezoneId)}</p>
              </div>
              <div>
                <p style={labelStyle}>{t("memberSince")}</p>
                <p style={valueStyle}>{formatMemberSince(me.createdAt, timezoneId)}</p>
              </div>
            </div>
          </div>
        </div>

        {/* Biometric Login Management */}
        <BiometricSection />

        {/* Export My Data */}
        <ExportDataSection />

        {/* Bug Report / Feedback */}
        <FeedbackSection />

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


function FeedbackSection() {
  const t = useTranslations("profile");

  return (
    <div style={{ ...cardStyle, marginTop: "1rem" }}>
      <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: "0 0 0.5rem" }}>
        {t("feedback")}
      </h2>
      <p style={{ fontSize: "0.75rem", color: "#64748b", margin: "0 0 1rem" }}>
        {t("feedbackDesc")}
      </p>
      <a
        href="mailto:support@shifter.app?subject=Bug Report / Feedback"
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: "0.5rem",
          background: "none",
          border: "1px solid #e2e8f0",
          borderRadius: 10,
          padding: "0.5rem 1rem",
          fontSize: "0.8125rem",
          color: "#374151",
          textDecoration: "none",
          cursor: "pointer",
        }}
      >
        <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
        </svg>
        {t("feedbackButton")}
      </a>
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
      setError(t("deleteError"));
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


function BiometricSection() {
  const timezoneId = useAuthStore(s => s.timezoneId);
  const [supported, setSupported] = useState(false);
  const [credentials, setCredentials] = useState<WebAuthnCredential[]>([]);
  const [loading, setLoading] = useState(true);
  const [registering, setRegistering] = useState(false);
  const [nicknameInput, setNicknameInput] = useState("");
  const [showNicknamePrompt, setShowNicknamePrompt] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editNickname, setEditNickname] = useState("");

  useEffect(() => {
    const webAuthnSupported = isWebAuthnSupported();
    setSupported(webAuthnSupported);
    if (webAuthnSupported) {
      loadCredentials();
    } else {
      setLoading(false);
    }
  }, []);

  async function loadCredentials() {
    try {
      const creds = await listCredentials();
      setCredentials(creds);
    } catch {
      // silently fail — section just won't show credentials
    } finally {
      setLoading(false);
    }
  }

  async function handleRegister() {
    setMessage(null);
    setRegistering(true);
    try {
      await registerCredential(nicknameInput || undefined);
      setMessage({ type: "success", text: "המכשיר נרשם בהצלחה!" });
      setShowNicknamePrompt(false);
      setNicknameInput("");
      await loadCredentials();
    } catch (err: any) {
      if (err?.message === "USER_CANCELLED") {
        setMessage({ type: "error", text: "הרישום בוטל. ניתן לנסות שוב." });
      } else {
        setMessage({ type: "error", text: "שגיאה ברישום המכשיר. נסה שוב." });
      }
    } finally {
      setRegistering(false);
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteCredential(id);
      setCredentials(prev => prev.filter(c => c.id !== id));
      setConfirmDeleteId(null);
      setMessage({ type: "success", text: "המכשיר הוסר בהצלחה." });
    } catch {
      setMessage({ type: "error", text: "שגיאה בהסרת המכשיר." });
    }
  }

  async function handleUpdateNickname(id: string) {
    try {
      await updateCredentialNickname(id, editNickname || null);
      setCredentials(prev =>
        prev.map(c => (c.id === id ? { ...c, nickname: editNickname || null } : c))
      );
      setEditingId(null);
      setEditNickname("");
    } catch {
      setMessage({ type: "error", text: "שגיאה בעדכון השם." });
    }
  }

  if (!supported) return null;

  return (
    <div style={{ ...cardStyle, marginTop: "1rem" }}>
      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.75rem" }}>
        {/* Fingerprint icon */}
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#7c3aed" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <path d="M2 12C2 6.5 6.5 2 12 2a10 10 0 0 1 8 4" />
          <path d="M5 19.5C5.5 18 6 15 6 12c0-3.5 2.5-6 6-6 3.5 0 6 2.5 6 6 0 1.5-.5 3-1 4" />
          <path d="M9 12c0-1.5 1.5-3 3-3s3 1.5 3 3-1 4-2 6" />
          <path d="M12 12v4" />
          <path d="M2 16c1 2 2.5 3.5 4.5 4.5" />
          <path d="M15 17c1 1.5 2 3 2.5 4.5" />
          <path d="M19.5 8c.5 1 .5 2 .5 4 0 2-.5 4-1 6" />
        </svg>
        <h2 style={{ fontSize: "0.875rem", fontWeight: 600, color: "#0f172a", margin: 0 }}>
          כניסה ביומטרית
        </h2>
      </div>

      {/* Status message */}
      {message && (
        <div style={{
          marginBottom: "0.75rem",
          padding: "0.5rem 0.75rem",
          borderRadius: 8,
          background: message.type === "success" ? "#f0fdf4" : "#fef2f2",
          border: `1px solid ${message.type === "success" ? "#bbf7d0" : "#fecaca"}`,
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
        }}>
          <p style={{ fontSize: "0.8125rem", color: message.type === "success" ? "#15803d" : "#dc2626", margin: 0 }}>
            {message.text}
          </p>
          <button
            onClick={() => setMessage(null)}
            style={{ background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 0 }}
            aria-label="סגור"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      )}

      {loading ? (
        <p style={{ fontSize: "0.8125rem", color: "#94a3b8" }}>טוען...</p>
      ) : credentials.length === 0 && !showNicknamePrompt ? (
        /* No credentials — show enable button */
        <div>
          <p style={{ fontSize: "0.8125rem", color: "#64748b", margin: "0 0 0.75rem" }}>
            הפעל כניסה ביומטרית כדי להתחבר מהר יותר עם טביעת אצבע או זיהוי פנים.
          </p>
          <button
            onClick={() => setShowNicknamePrompt(true)}
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "0.5rem",
              background: "linear-gradient(135deg, #7c3aed, #4f46e5)",
              color: "white",
              border: "none",
              borderRadius: 10,
              padding: "0.625rem 1.25rem",
              fontSize: "0.8125rem",
              fontWeight: 600,
              cursor: "pointer",
            }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הפעל כניסה ביומטרית
          </button>
        </div>
      ) : showNicknamePrompt ? (
        /* Nickname prompt before registration */
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          <p style={{ fontSize: "0.8125rem", color: "#64748b", margin: 0 }}>
            תן שם למכשיר (אופציונלי):
          </p>
          <input
            type="text"
            value={nicknameInput}
            onChange={e => setNicknameInput(e.target.value)}
            placeholder='לדוגמה: "האייפון שלי"'
            maxLength={100}
            style={{
              width: "100%",
              border: "1px solid #e2e8f0",
              borderRadius: 10,
              padding: "0.625rem 0.875rem",
              fontSize: "0.875rem",
              color: "#0f172a",
              outline: "none",
              boxSizing: "border-box",
            }}
          />
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button
              onClick={handleRegister}
              disabled={registering}
              style={{
                background: registering ? "#a78bfa" : "linear-gradient(135deg, #7c3aed, #4f46e5)",
                color: "white",
                border: "none",
                borderRadius: 8,
                padding: "0.5rem 1rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                cursor: registering ? "not-allowed" : "pointer",
              }}
            >
              {registering ? "רושם..." : "רשום מכשיר"}
            </button>
            <button
              onClick={() => { setShowNicknamePrompt(false); setNicknameInput(""); }}
              style={{
                background: "none",
                border: "1px solid #e2e8f0",
                borderRadius: 8,
                padding: "0.5rem 1rem",
                fontSize: "0.8125rem",
                color: "#64748b",
                cursor: "pointer",
              }}
            >
              ביטול
            </button>
          </div>
        </div>
      ) : (
        /* Credential list + management */
        <div>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}>
            {credentials.map(cred => (
              <div
                key={cred.id}
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  padding: "0.625rem 0.75rem",
                  borderRadius: 10,
                  border: "1px solid #e2e8f0",
                  background: cred.isDisabled ? "#fef2f2" : "#fafafa",
                }}
              >
                <div style={{ flex: 1, minWidth: 0 }}>
                  {editingId === cred.id ? (
                    <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
                      <input
                        type="text"
                        value={editNickname}
                        onChange={e => setEditNickname(e.target.value)}
                        maxLength={100}
                        style={{
                          flex: 1,
                          border: "1px solid #c7d2fe",
                          borderRadius: 6,
                          padding: "0.25rem 0.5rem",
                          fontSize: "0.8125rem",
                          outline: "none",
                        }}
                        onKeyDown={e => {
                          if (e.key === "Enter") handleUpdateNickname(cred.id);
                          if (e.key === "Escape") { setEditingId(null); setEditNickname(""); }
                        }}
                        autoFocus
                      />
                      <button
                        onClick={() => handleUpdateNickname(cred.id)}
                        style={{ background: "none", border: "none", cursor: "pointer", color: "#16a34a", padding: "2px" }}
                        aria-label="שמור"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                        </svg>
                      </button>
                      <button
                        onClick={() => { setEditingId(null); setEditNickname(""); }}
                        style={{ background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: "2px" }}
                        aria-label="ביטול"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                        </svg>
                      </button>
                    </div>
                  ) : (
                    <div>
                      <p
                        style={{
                          fontSize: "0.8125rem",
                          fontWeight: 600,
                          color: cred.isDisabled ? "#dc2626" : "#0f172a",
                          margin: 0,
                          cursor: "pointer",
                        }}
                        onClick={() => { setEditingId(cred.id); setEditNickname(cred.nickname || ""); }}
                        title="לחץ לעריכה"
                      >
                        {cred.nickname || "מכשיר ללא שם"}
                        {cred.isDisabled && " (מושבת)"}
                      </p>
                      <p style={{ fontSize: "0.6875rem", color: "#94a3b8", margin: "2px 0 0" }}>
                        נרשם: {formatLocalDate(cred.createdAt, timezoneId)}
                        {cred.lastUsedAt && ` · שימוש אחרון: ${formatLocalDate(cred.lastUsedAt, timezoneId)}`}
                      </p>
                    </div>
                  )}
                </div>

                {/* Delete button */}
                {editingId !== cred.id && (
                  confirmDeleteId === cred.id ? (
                    <div style={{ display: "flex", alignItems: "center", gap: "0.25rem" }}>
                      <button
                        onClick={() => handleDelete(cred.id)}
                        style={{ background: "#dc2626", color: "white", border: "none", borderRadius: 6, padding: "0.25rem 0.5rem", fontSize: "0.6875rem", fontWeight: 600, cursor: "pointer" }}
                      >
                        מחק
                      </button>
                      <button
                        onClick={() => setConfirmDeleteId(null)}
                        style={{ background: "none", border: "1px solid #e2e8f0", borderRadius: 6, padding: "0.25rem 0.5rem", fontSize: "0.6875rem", color: "#64748b", cursor: "pointer" }}
                      >
                        ביטול
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => setConfirmDeleteId(cred.id)}
                      style={{ background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: "4px" }}
                      aria-label="מחק מכשיר"
                    >
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                      </svg>
                    </button>
                  )
                )}
              </div>
            ))}
          </div>

          {/* Add another device button */}
          <button
            onClick={() => setShowNicknamePrompt(true)}
            style={{
              marginTop: "0.75rem",
              display: "inline-flex",
              alignItems: "center",
              gap: "0.375rem",
              background: "none",
              border: "1px solid #e2e8f0",
              borderRadius: 8,
              padding: "0.5rem 0.875rem",
              fontSize: "0.8125rem",
              color: "#374151",
              cursor: "pointer",
            }}
          >
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הוסף מכשיר נוסף
          </button>
        </div>
      )}
    </div>
  );
}
