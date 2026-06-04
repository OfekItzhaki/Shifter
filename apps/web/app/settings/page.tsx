"use client";

import { useState, useEffect, useRef, useCallback, useMemo } from "react";
import { useTranslations, useLocale } from "next-intl";
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
import { isRtl as isRtlLocale } from "@/lib/i18n/locales";

const cardStyle: React.CSSProperties = {
  borderRadius: 16,
  padding: "1.5rem",
};

const sectionHeaderStyle: React.CSSProperties = {
  fontSize: "0.875rem",
  fontWeight: 600,
  margin: "0 0 0.25rem",
};

const sectionDescStyle: React.CSSProperties = {
  fontSize: "0.75rem",
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
      <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">
        {label}
      </label>
      {!isOpen ? (
        <button
          type="button"
          onClick={handleOpen}
          disabled={disabled}
          className={`w-full px-3 py-2.5 rounded-xl border text-sm text-start transition-colors ${
            disabled
              ? "bg-slate-100 dark:bg-slate-700 border-slate-200 dark:border-slate-600 cursor-not-allowed"
              : "bg-white dark:bg-slate-700 border-slate-200 dark:border-slate-600 cursor-pointer hover:border-slate-300 dark:hover:border-slate-500"
          } ${value ? "text-slate-900 dark:text-white" : "text-slate-400 dark:text-slate-500"}`}
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
          className="w-full px-3 py-2.5 rounded-xl border border-sky-500 bg-white dark:bg-slate-700 text-sm text-slate-900 dark:text-white outline-none"
          role="combobox"
          aria-expanded={true}
          aria-autocomplete="list"
        />
      )}
      {isOpen && (
        <ul
          role="listbox"
          className="absolute top-full left-0 right-0 mt-1 max-h-[200px] overflow-y-auto bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-xl shadow-lg z-50 p-1"
          style={{ listStyle: "none" }}
        >
          {filtered.length === 0 ? (
            <li className="px-3 py-2 text-sm text-slate-400">—</li>
          ) : (
            filtered.map((opt) => (
              <li
                key={opt.value}
                role="option"
                aria-selected={opt.value === value}
                onClick={() => handleSelect(opt.value)}
                className={`px-3 py-2 text-sm rounded-lg cursor-pointer transition-colors ${
                  opt.value === value
                    ? "bg-sky-50 dark:bg-sky-900/30 text-slate-900 dark:text-white font-semibold"
                    : "text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-600"
                }`}
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
  const uiLocale = useLocale();
  const { timezoneId, setTimezone } = useAuthStore();

  const [selectedCountry, setSelectedCountry] = useState<string | null>(null);
  const [selectedState, setSelectedState] = useState<string | null>(null);
  const [resolvedTimezone, setResolvedTimezone] = useState<string | null>(timezoneId);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  // Use the active UI locale for country/state names
  const locale = uiLocale;

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
    <div style={cardStyle} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
      <h2 style={sectionHeaderStyle} className="text-slate-900 dark:text-white">{t("title")}</h2>
      <p style={sectionDescStyle} className="text-slate-500 dark:text-slate-400">{t("description")}</p>
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
          className="bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-300"
          style={{
            padding: "0.625rem 0.875rem",
            borderRadius: 10,
            fontSize: "0.8125rem",
          }}
        >
          <span style={{ fontWeight: 500 }}>{t("timezone")}:</span>{" "}
          <span className="text-slate-900 dark:text-white">{displayTimezone}</span>
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
            background: !selectedCountry || saving ? "#e2e8f0" : "#0ea5e9",
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
  const { timeFormat, setTimeFormat } = useAuthStore();

  return (
    <div style={cardStyle} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
      <h2 style={sectionHeaderStyle} className="text-slate-900 dark:text-white">{t("title")}</h2>
      <p style={sectionDescStyle} className="text-slate-500 dark:text-slate-400">{t("description")}</p>
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <button
          onClick={() => setTimeFormat("24h")}
          className={`flex-1 py-2.5 px-4 rounded-xl text-sm font-semibold transition-all ${
            timeFormat === "24h"
              ? "border-2 border-sky-500 bg-sky-50 dark:bg-sky-900/20 text-sky-700 dark:text-sky-300"
              : "border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-500 dark:text-slate-400"
          }`}
          aria-pressed={timeFormat === "24h"}
        >
          24h
          <span className="block text-xs font-normal mt-0.5">14:30</span>
        </button>
        <button
          onClick={() => setTimeFormat("12h")}
          className={`flex-1 py-2.5 px-4 rounded-xl text-sm font-semibold transition-all ${
            timeFormat === "12h"
              ? "border-2 border-sky-500 bg-sky-50 dark:bg-sky-900/20 text-sky-700 dark:text-sky-300"
              : "border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-500 dark:text-slate-400"
          }`}
          aria-pressed={timeFormat === "12h"}
        >
          AM/PM
          <span className="block text-xs font-normal mt-0.5">2:30 PM</span>
        </button>
      </div>
    </div>
  );
}

function NotificationSection() {
  const t = useTranslations("userSettings.notifications");

  return (
    <div style={cardStyle} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
      <h2 style={sectionHeaderStyle} className="text-slate-900 dark:text-white">{t("title")}</h2>
      <p style={sectionDescStyle} className="text-slate-500 dark:text-slate-400">{t("description")}</p>
      <NotificationPreferences />
    </div>
  );
}

function PushNotificationSection() {
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);

  if (!currentSpaceId) return null;

  return (
    <div style={cardStyle} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm">
      <PushNotificationSettings spaceId={currentSpaceId} />
    </div>
  );
}

export default function SettingsPage() {
  const t = useTranslations("userSettings");
  const locale = useLocale();
  const isRtl = isRtlLocale(locale);

  return (
    <AppShell>
      <div style={{ maxWidth: 720, direction: isRtl ? "rtl" : "ltr" }}>
        {/* Page header */}
        <div style={{ marginBottom: "1.5rem" }}>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white" style={{ margin: "0 0 0.25rem" }}>
            {t("title")}
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400" style={{ margin: 0 }}>
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
