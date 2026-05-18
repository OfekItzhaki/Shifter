"use client";

import { useState, useEffect, useRef, useCallback, useMemo } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import NotificationPreferences from "@/components/NotificationPreferences";
import PushNotificationSettings from "@/components/PushNotificationSettings";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { updateUserLocation } from "@/lib/api/userSettings";
import {
  COUNTRIES,
  STATES,
  MULTI_TIMEZONE_COUNTRIES,
  getCountryName,
  getStateName,
} from "@/lib/data/countries";

const cardStyle: React.CSSProperties = {
  background: "white",
  borderRadius: 16,
  border: "1px solid #e2e8f0",
  boxShadow: "0 1px 4px rgba(0,0,0,0.06)",
  padding: "1.5rem",
};

const sectionHeaderStyle: React.CSSProperties = {
  fontSize: "0.875rem",
  fontWeight: 600,
  color: "#0f172a",
  margin: "0 0 0.25rem",
};

const sectionDescStyle: React.CSSProperties = {
  fontSize: "0.75rem",
  color: "#64748b",
  margin: "0 0 1rem",
};

// ─── Searchable Dropdown ─────────────────────────────────────────────────────

interface SearchableDropdownProps {
  label: string;
  placeholder: string;
  options: { value: string; label: string }[];
  value: string | null;
  onChange: (value: string | null) => void;
  disabled?: boolean;
}

function SearchableDropdown({
  label,
  placeholder,
  options,
  value,
  onChange,
  disabled,
}: SearchableDropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const filtered = useMemo(() => {
    if (!search.trim()) return options;
    const q = search.toLowerCase();
    return options.filter((o) => o.label.toLowerCase().includes(q));
  }, [options, search]);

  const selectedLabel = useMemo(() => {
    if (!value) return "";
    const opt = options.find((o) => o.value === value);
    return opt?.label ?? value;
  }, [value, options]);

  // Close on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        setSearch("");
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  const handleSelect = useCallback(
    (val: string) => {
      onChange(val);
      setIsOpen(false);
      setSearch("");
    },
    [onChange]
  );

  const handleOpen = useCallback(() => {
    if (disabled) return;
    setIsOpen(true);
    setSearch("");
    setTimeout(() => inputRef.current?.focus(), 0);
  }, [disabled]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Escape") {
        setIsOpen(false);
        setSearch("");
      }
      if (e.key === "Enter" && filtered.length === 1) {
        handleSelect(filtered[0].value);
      }
    },
    [filtered, handleSelect]
  );

  return (
    <div ref={containerRef} style={{ position: "relative" }}>
      <label
        style={{
          display: "block",
          fontSize: "0.75rem",
          fontWeight: 500,
          color: "#64748b",
          marginBottom: 4,
        }}
      >
        {label}
      </label>
      {!isOpen ? (
        <button
          type="button"
          onClick={handleOpen}
          disabled={disabled}
          style={{
            width: "100%",
            padding: "0.625rem 0.875rem",
            borderRadius: 10,
            border: "1px solid #e2e8f0",
            background: disabled ? "#f1f5f9" : "white",
            fontSize: "0.8125rem",
            color: value ? "#0f172a" : "#94a3b8",
            textAlign: "start",
            cursor: disabled ? "not-allowed" : "pointer",
            transition: "border-color 0.15s",
          }}
          aria-haspopup="listbox"
          aria-expanded={false}
        >
          {selectedLabel || placeholder}
        </button>
      ) : (
        <input
          ref={inputRef}
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          style={{
            width: "100%",
            padding: "0.625rem 0.875rem",
            borderRadius: 10,
            border: "1px solid #3b82f6",
            background: "white",
            fontSize: "0.8125rem",
            color: "#0f172a",
            outline: "none",
          }}
          role="combobox"
          aria-expanded={true}
          aria-autocomplete="list"
        />
      )}
      {isOpen && (
        <ul
          role="listbox"
          style={{
            position: "absolute",
            top: "100%",
            left: 0,
            right: 0,
            marginTop: 4,
            maxHeight: 200,
            overflowY: "auto",
            background: "white",
            border: "1px solid #e2e8f0",
            borderRadius: 10,
            boxShadow: "0 4px 12px rgba(0,0,0,0.1)",
            zIndex: 50,
            padding: 4,
            listStyle: "none",
          }}
        >
          {filtered.length === 0 ? (
            <li
              style={{
                padding: "0.5rem 0.75rem",
                fontSize: "0.8125rem",
                color: "#94a3b8",
              }}
            >
              —
            </li>
          ) : (
            filtered.map((opt) => (
              <li
                key={opt.value}
                role="option"
                aria-selected={opt.value === value}
                onClick={() => handleSelect(opt.value)}
                style={{
                  padding: "0.5rem 0.75rem",
                  fontSize: "0.8125rem",
                  color: "#0f172a",
                  borderRadius: 6,
                  cursor: "pointer",
                  background: opt.value === value ? "#eff6ff" : "transparent",
                  fontWeight: opt.value === value ? 600 : 400,
                  transition: "background 0.1s",
                }}
                onMouseEnter={(e) => {
                  (e.currentTarget as HTMLElement).style.background = "#f1f5f9";
                }}
                onMouseLeave={(e) => {
                  (e.currentTarget as HTMLElement).style.background =
                    opt.value === value ? "#eff6ff" : "transparent";
                }}
              >
                {opt.label}
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}

// ─── Location Section ────────────────────────────────────────────────────────

function LocationSection() {
  const t = useTranslations("userSettings.location");
  const { timezoneId, preferredLocale, setTimezone } = useAuthStore();

  const [selectedCountry, setSelectedCountry] = useState<string | null>(null);
  const [selectedState, setSelectedState] = useState<string | null>(null);
  const [resolvedTimezone, setResolvedTimezone] = useState<string | null>(timezoneId);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  // Determine locale key for country/state names
  const locale = preferredLocale ?? "he";

  // Build country options with localized names
  const countryOptions = useMemo(
    () =>
      COUNTRIES.map((c) => ({
        value: c.code,
        label: getCountryName(c.code, locale),
      })).sort((a, b) => a.label.localeCompare(b.label, locale)),
    [locale]
  );

  // Build state options for the selected country
  const stateOptions = useMemo(() => {
    if (!selectedCountry || !MULTI_TIMEZONE_COUNTRIES.has(selectedCountry)) return [];
    const states = STATES[selectedCountry] ?? [];
    return states
      .map((s) => ({
        value: s.code,
        label: getStateName(selectedCountry, s.code, locale),
      }))
      .sort((a, b) => a.label.localeCompare(b.label, locale));
  }, [selectedCountry, locale]);

  const showStateDropdown = selectedCountry && MULTI_TIMEZONE_COUNTRIES.has(selectedCountry);

  // When country changes, clear state
  const handleCountryChange = useCallback((code: string | null) => {
    setSelectedCountry(code);
    setSelectedState(null);
    setResolvedTimezone(null);
    setSaved(false);
  }, []);

  const handleStateChange = useCallback((code: string | null) => {
    setSelectedState(code);
    setResolvedTimezone(null);
    setSaved(false);
  }, []);

  // Save location
  const handleSave = useCallback(async () => {
    if (!selectedCountry) return;
    setSaving(true);
    setSaved(false);
    try {
      const result = await updateUserLocation(selectedCountry, selectedState);
      setResolvedTimezone(result.ianaTimezoneId);
      // Update authStore immediately — no re-login needed
      setTimezone(result.ianaTimezoneId, result.offsetMinutes);
      setSaved(true);
    } catch {
      // Error handling is done by the axios interceptor
    } finally {
      setSaving(false);
    }
  }, [selectedCountry, selectedState, setTimezone]);

  // Show the current timezone from store as initial resolved value
  const displayTimezone = resolvedTimezone ?? timezoneId ?? "Asia/Jerusalem";

  return (
    <div style={cardStyle}>
      <h2 style={sectionHeaderStyle}>{t("title")}</h2>
      <p style={sectionDescStyle}>{t("description")}</p>
      <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
        {/* Country dropdown */}
        <SearchableDropdown
          label={t("country")}
          placeholder={t("countryPlaceholder")}
          options={countryOptions}
          value={selectedCountry}
          onChange={handleCountryChange}
        />

        {/* State dropdown — only for multi-timezone countries */}
        {showStateDropdown && (
          <SearchableDropdown
            label={t("state")}
            placeholder={t("statePlaceholder")}
            options={stateOptions}
            value={selectedState}
            onChange={handleStateChange}
          />
        )}

        {/* Resolved timezone display */}
        <div
          style={{
            padding: "0.625rem 0.875rem",
            borderRadius: 10,
            background: "#f8fafc",
            border: "1px solid #e2e8f0",
            fontSize: "0.8125rem",
            color: "#475569",
          }}
        >
          <span style={{ fontWeight: 500 }}>{t("timezone")}:</span>{" "}
          <span style={{ color: "#0f172a" }}>{displayTimezone}</span>
        </div>

        {/* Save button */}
        <button
          type="button"
          onClick={handleSave}
          disabled={!selectedCountry || saving}
          style={{
            padding: "0.625rem 1.25rem",
            borderRadius: 10,
            border: "none",
            background: !selectedCountry || saving ? "#e2e8f0" : "#3b82f6",
            color: !selectedCountry || saving ? "#94a3b8" : "white",
            fontWeight: 600,
            fontSize: "0.8125rem",
            cursor: !selectedCountry || saving ? "not-allowed" : "pointer",
            transition: "all 0.15s",
            alignSelf: "flex-start",
          }}
        >
          {saving ? t("saving") : saved ? t("saved") : t("save")}
        </button>
      </div>
    </div>
  );
}

function TimeFormatSection() {
  const t = useTranslations("userSettings.timeFormat");
  const tProfile = useTranslations("profile");
  const { timeFormat, setTimeFormat } = useAuthStore();

  return (
    <div style={cardStyle}>
      <h2 style={sectionHeaderStyle}>{t("title")}</h2>
      <p style={sectionDescStyle}>{t("description")}</p>
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

function NotificationSection() {
  const t = useTranslations("userSettings.notifications");

  return (
    <div style={cardStyle}>
      <h2 style={sectionHeaderStyle}>{t("title")}</h2>
      <p style={sectionDescStyle}>{t("description")}</p>
      <NotificationPreferences />
    </div>
  );
}

function PushNotificationSection() {
  const t = useTranslations("userSettings.push");
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);

  if (!currentSpaceId) return null;

  return (
    <div style={cardStyle}>
      <h2 style={sectionHeaderStyle}>{t("title")}</h2>
      <p style={sectionDescStyle}>{t("description")}</p>
      <PushNotificationSettings spaceId={currentSpaceId} />
    </div>
  );
}

export default function SettingsPage() {
  const t = useTranslations("userSettings");

  return (
    <AppShell>
      <div style={{ maxWidth: 720, direction: "rtl" }}>
        {/* Page header */}
        <div style={{ marginBottom: "1.5rem" }}>
          <h1 style={{ fontSize: "1.5rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.25rem" }}>
            {t("title")}
          </h1>
          <p style={{ fontSize: "0.875rem", color: "#64748b", margin: 0 }}>
            {t("subtitle")}
          </p>
        </div>

        {/* Settings sections */}
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <LocationSection />
          <TimeFormatSection />
          <NotificationSection />
          <PushNotificationSection />
        </div>
      </div>
    </AppShell>
  );
}
