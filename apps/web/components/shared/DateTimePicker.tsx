"use client";

import { useState, useRef, useEffect, useCallback } from "react";
import { useLocale } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";

interface Props {
  value: string; // ISO datetime-local format: "2026-05-23T14:00"
  onChange: (value: string) => void;
  onClear?: () => void;
  clearLabel?: string;
  className?: string;
}

/**
 * Custom date-time picker that respects the user's locale and time format preference.
 * - Hebrew: DD/MM/YYYY
 * - English: MM/DD/YYYY  
 * - Time: 24h or 12h based on user setting
 */
export default function DateTimePicker({ value, onChange, onClear, clearLabel, className }: Props) {
  const locale = useLocale();
  const timeFormat = useAuthStore(s => s.timeFormat);
  const is24h = timeFormat === "24h";
  const isHe = locale === "he";

  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Parse value
  const parsed = value ? new Date(value) : null;
  const [year, setYear] = useState(parsed?.getFullYear() ?? new Date().getFullYear());
  const [month, setMonth] = useState(parsed?.getMonth() ?? new Date().getMonth());
  const [day, setDay] = useState(parsed?.getDate() ?? new Date().getDate());
  const [hour, setHour] = useState(parsed?.getHours() ?? 0);
  const [minute, setMinute] = useState(parsed?.getMinutes() ?? 0);

  // Sync from prop
  useEffect(() => {
    if (value) {
      const d = new Date(value);
      if (!isNaN(d.getTime())) {
        setYear(d.getFullYear());
        setMonth(d.getMonth());
        setDay(d.getDate());
        setHour(d.getHours());
        setMinute(d.getMinutes());
      }
    }
  }, [value]);

  // Close on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  const emitChange = useCallback((y: number, m: number, d: number, h: number, min: number) => {
    const pad = (n: number) => String(n).padStart(2, "0");
    onChange(`${y}-${pad(m + 1)}-${pad(d)}T${pad(h)}:${pad(min)}`);
  }, [onChange]);

  function handleDayClick(d: number) {
    setDay(d);
    emitChange(year, month, d, hour, minute);
  }

  function handleHourChange(h: number) {
    setHour(h);
    emitChange(year, month, day, h, minute);
  }

  function handleMinuteChange(m: number) {
    setMinute(m);
    emitChange(year, month, day, hour, m);
  }

  function prevMonth() {
    if (month === 0) { setMonth(11); setYear(y => y - 1); }
    else setMonth(m => m - 1);
  }

  function nextMonth() {
    if (month === 11) { setMonth(0); setYear(y => y + 1); }
    else setMonth(m => m + 1);
  }

  // Format display value
  function formatDisplay(): string {
    if (!value) return "";
    const pad = (n: number) => String(n).padStart(2, "0");
    const dateStr = isHe
      ? `${pad(day)}/${pad(month + 1)}/${year}`
      : `${pad(month + 1)}/${pad(day)}/${year}`;
    const timeStr = is24h
      ? `${pad(hour)}:${pad(minute)}`
      : `${hour === 0 ? 12 : hour > 12 ? hour - 12 : hour}:${pad(minute)} ${hour >= 12 ? "PM" : "AM"}`;
    return `${dateStr}  ${timeStr}`;
  }

  // Calendar grid
  const firstDayOfMonth = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const monthNames = isHe
    ? ["ינואר", "פברואר", "מרץ", "אפריל", "מאי", "יוני", "יולי", "אוגוסט", "ספטמבר", "אוקטובר", "נובמבר", "דצמבר"]
    : ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  const dayNames = isHe ? ["א", "ב", "ג", "ד", "ה", "ו", "ש"] : ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];

  return (
    <div ref={containerRef} className={`relative ${className ?? ""}`}>
      {/* Display button */}
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm text-start focus:outline-none focus:border-sky-500 flex items-center justify-between"
      >
        <span className={value ? "" : "text-slate-400 dark:text-slate-500"}>
          {value ? formatDisplay() : (isHe ? "בחר תאריך ושעה..." : "Select date & time...")}
        </span>
        <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="text-slate-400 flex-shrink-0">
          <path strokeLinecap="round" strokeLinejoin="round" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
        </svg>
      </button>

      {/* Dropdown */}
      {isOpen && (
        <div className="absolute top-full left-0 right-0 mt-1 z-50 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl shadow-xl p-3 min-w-[280px]">
          {/* Month navigation */}
          <div className="flex items-center justify-between mb-2">
            <button type="button" onClick={prevMonth} className="p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-600 dark:text-slate-300">
              <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" /></svg>
            </button>
            <span className="text-sm font-semibold text-slate-900 dark:text-white">
              {monthNames[month]} {year}
            </span>
            <button type="button" onClick={nextMonth} className="p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-600 dark:text-slate-300">
              <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" /></svg>
            </button>
          </div>

          {/* Day names */}
          <div className="grid grid-cols-7 gap-0 mb-1">
            {dayNames.map(d => (
              <div key={d} className="text-center text-[10px] font-medium text-slate-400 dark:text-slate-500 py-1">{d}</div>
            ))}
          </div>

          {/* Days grid */}
          <div className="grid grid-cols-7 gap-0">
            {Array.from({ length: firstDayOfMonth }).map((_, i) => <div key={`e${i}`} />)}
            {Array.from({ length: daysInMonth }).map((_, i) => {
              const d = i + 1;
              const isSelected = d === day;
              const isToday = d === new Date().getDate() && month === new Date().getMonth() && year === new Date().getFullYear();
              return (
                <button
                  key={d}
                  type="button"
                  onClick={() => handleDayClick(d)}
                  className={`w-8 h-8 rounded-lg text-xs font-medium transition-colors ${
                    isSelected
                      ? "bg-sky-500 text-white"
                      : isToday
                        ? "bg-sky-100 dark:bg-sky-900/30 text-sky-700 dark:text-sky-300"
                        : "text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700"
                  }`}
                >
                  {d}
                </button>
              );
            })}
          </div>

          {/* Time picker */}
          <div className="mt-3 pt-3 border-t border-slate-100 dark:border-slate-700 flex items-center gap-2 justify-center">
            <select
              value={hour}
              onChange={e => handleHourChange(Number(e.target.value))}
              className="px-2 py-1.5 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-sky-500"
            >
              {Array.from({ length: 24 }).map((_, h) => (
                <option key={h} value={h}>
                  {is24h ? String(h).padStart(2, "0") : (h === 0 ? "12 AM" : h < 12 ? `${h} AM` : h === 12 ? "12 PM" : `${h - 12} PM`)}
                </option>
              ))}
            </select>
            <span className="text-slate-400 font-bold">:</span>
            <select
              value={minute}
              onChange={e => handleMinuteChange(Number(e.target.value))}
              className="px-2 py-1.5 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-sky-500"
            >
              {[0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55].map(m => (
                <option key={m} value={m}>{String(m).padStart(2, "0")}</option>
              ))}
            </select>
          </div>

          {/* Actions */}
          <div className="mt-3 flex items-center justify-between">
            {onClear && (
              <button type="button" onClick={() => { onClear(); setIsOpen(false); }} className="text-xs text-slate-500 dark:text-slate-400 hover:text-red-500 transition-colors">
                {clearLabel ?? "Clear"}
              </button>
            )}
            <button type="button" onClick={() => setIsOpen(false)} className="text-xs text-sky-600 dark:text-sky-400 font-medium hover:underline ms-auto">
              OK
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
