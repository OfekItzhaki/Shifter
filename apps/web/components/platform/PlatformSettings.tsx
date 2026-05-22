"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "@/lib/api/client";

const MIN_TIMEOUT = 5;
const MAX_TIMEOUT = 120;

export default function PlatformSettings() {
  const t = useTranslations("platform");

  const [timeoutMinutes, setTimeoutMinutes] = useState<number>(15);
  const [inputValue, setInputValue] = useState<string>("15");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    apiClient
      .get<{ platformTimeoutMinutes: number }>("/platform/settings")
      .then(({ data }) => {
        const value = data.platformTimeoutMinutes ?? 15;
        setTimeoutMinutes(value);
        setInputValue(String(value));
      })
      .catch(() => {
        // Use default if fetch fails
      })
      .finally(() => setLoading(false));
  }, []);

  function validateInput(value: string): string | null {
    const num = Number(value);
    if (value === "" || isNaN(num)) {
      return t("timeoutValidationRequired");
    }
    if (!Number.isInteger(num)) {
      return t("timeoutValidationInteger");
    }
    if (num < MIN_TIMEOUT || num > MAX_TIMEOUT) {
      return t("timeoutValidationRange", { min: MIN_TIMEOUT, max: MAX_TIMEOUT });
    }
    return null;
  }

  function handleInputChange(value: string) {
    setInputValue(value);
    setSaved(false);
    const err = validateInput(value);
    setValidationError(err);
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    const err = validateInput(inputValue);
    if (err) {
      setValidationError(err);
      return;
    }

    const newValue = Number(inputValue);
    setSaving(true);
    setError(null);
    setSaved(false);

    try {
      await apiClient.patch("/platform/settings", {
        platformTimeoutMinutes: newValue,
      });
      setTimeoutMinutes(newValue);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string } } };
      setError(axiosErr?.response?.data?.error || t("settingsSaveError"));
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div style={{ background: "white", borderRadius: 14, border: "1px solid #e2e8f0", padding: "1.5rem" }}>
        <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: "0 0 1rem" }}>
          {t("platformSettings")}
        </h2>
        <p style={{ color: "#94a3b8", fontSize: "0.8rem" }}>{t("loading")}</p>
      </div>
    );
  }

  return (
    <div style={{ background: "white", borderRadius: 14, border: "1px solid #e2e8f0", padding: "1.5rem" }}>
      <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: "0 0 1rem" }}>
        {t("platformSettings")}
      </h2>

      <form onSubmit={handleSave} style={{ display: "flex", flexDirection: "column", gap: "0.75rem", maxWidth: 400 }}>
        <div>
          <label
            htmlFor="platformTimeoutMinutes"
            style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "#475569", marginBottom: "0.375rem" }}
          >
            {t("sessionTimeoutLabel")}
          </label>
          <p style={{ fontSize: "0.75rem", color: "#94a3b8", margin: "0 0 0.5rem" }}>
            {t("sessionTimeoutDescription")}
          </p>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <input
              id="platformTimeoutMinutes"
              type="number"
              min={MIN_TIMEOUT}
              max={MAX_TIMEOUT}
              step={1}
              value={inputValue}
              onChange={(e) => handleInputChange(e.target.value)}
              aria-describedby="timeout-hint"
              aria-invalid={!!validationError}
              style={{
                width: 90,
                border: `1px solid ${validationError ? "#fca5a5" : "#e2e8f0"}`,
                borderRadius: 8,
                padding: "0.5rem 0.75rem",
                fontSize: "0.8rem",
                textAlign: "center",
              }}
            />
            <span style={{ fontSize: "0.8rem", color: "#64748b" }}>{t("minutes")}</span>
          </div>
          <p id="timeout-hint" style={{ fontSize: "0.7rem", color: "#94a3b8", margin: "0.25rem 0 0" }}>
            {t("timeoutRangeHint", { min: MIN_TIMEOUT, max: MAX_TIMEOUT })}
          </p>
          {validationError && (
            <p style={{ fontSize: "0.75rem", color: "#dc2626", margin: "0.25rem 0 0" }} role="alert">
              {validationError}
            </p>
          )}
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
          <button
            type="submit"
            disabled={saving || !!validationError}
            style={{
              background: "#0ea5e9",
              color: "white",
              border: "none",
              borderRadius: 8,
              padding: "0.5rem 1rem",
              fontSize: "0.8rem",
              fontWeight: 600,
              cursor: saving || !!validationError ? "not-allowed" : "pointer",
              opacity: saving || !!validationError ? 0.5 : 1,
            }}
          >
            {saving ? "..." : t("saveSettings")}
          </button>
          {saved && (
            <span style={{ fontSize: "0.8rem", color: "#10b981", fontWeight: 600 }}>
              ✓ {t("saved")}
            </span>
          )}
        </div>

        {error && (
          <p style={{ color: "#dc2626", fontSize: "0.8rem", margin: 0 }}>{error}</p>
        )}
      </form>
    </div>
  );
}
