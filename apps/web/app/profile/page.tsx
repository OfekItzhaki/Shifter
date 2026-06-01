"use client";

import { useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import AppShell from "@/components/shell/AppShell";
import Modal from "@/components/Modal";
import { getMe, updateMe, MeDto } from "@/lib/api/auth";
import { apiClient } from "@/lib/api/client";
import ImageUpload from "@/components/ImageUpload";
import ErrorState from "@/components/shared/ErrorState";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalDate } from "@/lib/utils/formatTime";
function getInitials(name: string): string {
  return name
    .split(" ")
    .map(p => p.charAt(0))
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

function formatBirthday(dateStr: string | null, timezoneId: string | null, locale: string): string {
  if (!dateStr) return "—";
  return formatLocalDate(dateStr, timezoneId, locale);
}

function formatMemberSince(dateStr: string, timezoneId: string | null, locale: string): string {
  return formatLocalDate(dateStr, timezoneId, locale);
}

const cardStyle: React.CSSProperties = {
  borderRadius: 16,
  padding: "1.5rem",
};

const labelStyle: React.CSSProperties = {
  fontSize: "0.75rem",
  fontWeight: 600,
  textTransform: "uppercase",
  letterSpacing: "0.05em",
  marginBottom: "0.25rem",
  cursor: "default",
  userSelect: "none",
};

const valueStyle: React.CSSProperties = {
  fontSize: "0.9375rem",
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
  const locale = useLocale();
  const isRtl = locale === "he";
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
        <ErrorState
          type="server"
          title={t("loadError")}
          description={t("loadErrorDesc")}
          onRetry={() => window.location.reload()}
          showHomeLink={false}
        />
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
      background: "linear-gradient(135deg, #0ea5e9, #6366f1)",
      display: "flex", alignItems: "center", justifyContent: "center",
      color: "white", fontSize: "2rem", fontWeight: 700,
    }}>
      {getInitials(me.displayName)}
    </div>
  );

  return (
    <AppShell>
      <div style={{ maxWidth: 720, direction: isRtl ? "rtl" : "ltr" }}>
        {/* Hero section */}
        <div style={cardStyle} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm mb-6">
          <div className="flex flex-col sm:flex-row items-center sm:items-center gap-4 sm:gap-6">
            {avatarContent}
            <div style={{ flex: 1 }} className="text-center sm:text-start">
              <h1 className="text-2xl font-bold text-slate-900 dark:text-white" style={{ margin: "0 0 0.25rem" }}>
                {me.displayName}
              </h1>
              <p className="text-sm text-slate-500 dark:text-slate-400" style={{ margin: 0 }}>{me.email}</p>
            </div>
            <button
              onClick={openEdit}
              className="border border-slate-200 dark:border-slate-600 rounded-xl px-4 py-2 text-sm font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors flex-shrink-0 bg-transparent cursor-pointer"
            >
              {t("edit")}
            </button>
          </div>
        </div>

        {/* Info cards grid */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }} className="mobile-stack">
          {/* Contact info */}
          <div style={cardStyle} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white" style={{ margin: "0 0 1rem" }}>{t("contactInfo")}</h2>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
              <div>
                <p style={labelStyle} className="text-slate-400 dark:text-slate-500">{t("phone")}</p>
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="text-slate-400">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                  </svg>
                  <p style={{ ...valueStyle, direction: "ltr", textAlign: "left" }} className="text-slate-900 dark:text-white">{me.phoneNumber ?? "—"}</p>
                </div>
              </div>
              <div>
                <p style={labelStyle} className="text-slate-400 dark:text-slate-500">{t("email")}</p>
                <p style={valueStyle} className="text-slate-900 dark:text-white">{me.email}</p>
              </div>
            </div>
          </div>

          {/* Personal info */}
          <div style={cardStyle} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white" style={{ margin: "0 0 1rem" }}>{t("personalInfo")}</h2>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
              <div>
                <p style={labelStyle} className="text-slate-400 dark:text-slate-500">{t("birthday")}</p>
                <p style={valueStyle} className="text-slate-900 dark:text-white">{formatBirthday(me.birthday, timezoneId, locale)}</p>
              </div>
              <div>
                <p style={labelStyle} className="text-slate-400 dark:text-slate-500">{t("memberSince")}</p>
                <p style={valueStyle} className="text-slate-900 dark:text-white">{formatMemberSince(me.createdAt, timezoneId, locale)}</p>
              </div>
            </div>
          </div>
        </div>


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
                background: saving ? "#7dd3fc" : "#0ea5e9",
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
    <div style={{ ...cardStyle, marginTop: "1rem" }} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900 dark:text-white" style={{ margin: "0 0 0.5rem" }}>
        {t("exportData")}
      </h2>
      <p className="text-xs text-slate-500 dark:text-slate-400" style={{ margin: "0 0 1rem" }}>
        {t("exportDataDesc")}
      </p>
      <button
        onClick={handleExport}
        disabled={exporting}
        className="border border-slate-200 dark:border-slate-600 rounded-xl px-4 py-2 text-sm text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors bg-transparent cursor-pointer disabled:opacity-60"
      >
        {exporting ? t("exporting") : t("exportButton")}
      </button>
    </div>
  );
}


function FeedbackSection() {
  const t = useTranslations("profile");

  return (
    <div style={{ ...cardStyle, marginTop: "1rem" }} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900 dark:text-white" style={{ margin: "0 0 0.5rem" }}>
        {t("feedback")}
      </h2>
      <p className="text-xs text-slate-500 dark:text-slate-400" style={{ margin: "0 0 1rem" }}>
        {t("feedbackDesc")}
      </p>
      <a
        href="mailto:support@shifter.app?subject=Bug Report / Feedback"
        className="inline-flex items-center gap-2 border border-slate-200 dark:border-slate-600 rounded-xl px-4 py-2 text-sm text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors no-underline"
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
    <div style={{ ...cardStyle, marginTop: "1.5rem" }} className="bg-white dark:bg-slate-800 border border-red-200 dark:border-red-900/50 shadow-sm">
      <h2 className="text-sm font-semibold text-red-600 dark:text-red-400" style={{ margin: "0 0 0.5rem" }}>
        {t("deleteAccount") ?? "Delete Account"}
      </h2>
      <p className="text-xs text-slate-500 dark:text-slate-400" style={{ margin: "0 0 1rem" }}>
        {t("deleteAccountDesc") ?? "Permanently delete your account and all associated data. This cannot be undone."}
      </p>
      {!showConfirm ? (
        <button
          onClick={() => setShowConfirm(true)}
          className="border border-red-200 dark:border-red-800 rounded-xl px-4 py-2 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors bg-transparent cursor-pointer"
        >
          {t("deleteAccountButton") ?? "Delete My Account"}
        </button>
      ) : (
        <div className="bg-red-50 dark:bg-red-900/20 rounded-xl p-4 flex flex-col gap-3">
          <p className="text-sm text-red-700 dark:text-red-300 font-semibold" style={{ margin: 0 }}>
            {t("deleteConfirmText") ?? "Are you sure? This will permanently delete your account, all your data, and remove you from all groups."}
          </p>
          {error && <p className="text-xs text-red-600 dark:text-red-400" style={{ margin: 0 }}>{error}</p>}
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="bg-red-600 hover:bg-red-700 text-white rounded-lg px-4 py-2 text-sm font-semibold disabled:opacity-60 cursor-pointer border-none"
            >
              {deleting ? "..." : (t("yesDelete") ?? "Yes, Delete Everything")}
            </button>
            <button
              onClick={() => setShowConfirm(false)}
              className="border border-slate-200 dark:border-slate-600 rounded-lg px-4 py-2 text-sm text-slate-500 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors bg-transparent cursor-pointer"
            >
              {t("cancel")}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}


